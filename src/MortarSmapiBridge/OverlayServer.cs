using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MortarSmapiBridge;

internal sealed class OverlayServer : IDisposable
{
    private const int MaxRequestLineBytes = 4096;
    private const int MaxHeaderLineBytes = 4096;
    private static readonly TimeSpan ClientTimeout = TimeSpan.FromSeconds(5);

    private readonly TcpListener Listener;
    private readonly CancellationTokenSource Cts = new();
    private readonly string Token;
    internal const string NotInGameJson = "{\"inGame\":false}";

    private string? snapshot;

    public OverlayServer(int port, string token)
    {
        if (!IsValidPort(port))
            throw new ArgumentOutOfRangeException(nameof(port));
        if (string.IsNullOrWhiteSpace(token))
            throw new ArgumentException("The overlay token is required.", nameof(token));

        this.Listener = new TcpListener(IPAddress.Loopback, port);
        this.Token = token;
    }

    public int Port => ((IPEndPoint)this.Listener.LocalEndpoint).Port;

    public static bool IsValidPort(int port) => port is >= 1 and <= 65535;

    public static bool ShouldStart(ModConfig config, bool gameVersionTested) =>
        config.OverlayEnabled && gameVersionTested;

    public void SetNotInGame() => Volatile.Write(ref this.snapshot, NotInGameJson);

    public void SetSnapshot(OverlaySnapshot? value) =>
        Volatile.Write(ref this.snapshot, value == null ? null : Serialize(value));

    internal static bool ShouldLogReadError(ISet<string> seen, string message) => seen.Add(message);

    public void Start()
    {
        this.Listener.Start();
        _ = Task.Run(this.AcceptLoop);
    }

    public void Dispose()
    {
        this.Cts.Cancel();
        this.Listener.Stop();
        this.Cts.Dispose();
    }

    internal static bool TokenMatches(string expected, string? provided) =>
        provided != null
        && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(provided));

    internal static string Serialize(OverlaySnapshot value) => JsonSerializer.Serialize(value);

    private async Task AcceptLoop()
    {
        while (!this.Cts.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await this.Listener.AcceptTcpClientAsync(this.Cts.Token);
            }
            catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException or SocketException)
            {
                return;
            }

            _ = Task.Run(() => this.Handle(client));
        }
    }

    private async Task Handle(TcpClient client)
    {
        using (client)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(this.Cts.Token);
            timeout.CancelAfter(ClientTimeout);

            try
            {
                NetworkStream stream = client.GetStream();
                string response = await this.Process(stream, timeout.Token);
                await stream.WriteAsync(Encoding.UTF8.GetBytes(response), timeout.Token);
            }
            catch (Exception ex) when (ex is OperationCanceledException or IOException or SocketException or ObjectDisposedException)
            {
            }
        }
    }

    private async Task<string> Process(NetworkStream stream, CancellationToken cancel)
    {
        string? requestLine = await ReadLine(stream, MaxRequestLineBytes, cancel);
        if (requestLine == null)
            return Response(400, "{\"error\":\"bad request\"}");

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        while (true)
        {
            string? line = await ReadLine(stream, MaxHeaderLineBytes, cancel);
            if (line == null)
                return Response(400, "{\"error\":\"bad request\"}");
            if (line.Length == 0)
                break;

            int separator = line.IndexOf(':');
            if (separator <= 0)
                return Response(400, "{\"error\":\"bad request\"}");
            headers[line[..separator].Trim()] = line[(separator + 1)..].Trim();
        }

        string[] parts = requestLine.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3)
            return Response(400, "{\"error\":\"bad request\"}");
        if (!string.Equals(parts[0], "GET", StringComparison.Ordinal))
            return Response(405, "{\"error\":\"method not allowed\"}");
        if (!string.Equals(GetPath(parts[1]), "/state", StringComparison.Ordinal))
            return Response(404, "{\"error\":\"not found\"}");

        if (!TokenMatches(this.Token, GetToken(parts[1], headers)))
            return Response(401, "{\"error\":\"unauthorized\"}", "WWW-Authenticate: Bearer\r\n");

        string? current = Volatile.Read(ref this.snapshot);
        return current == null
            ? Response(503, "{\"error\":\"state unavailable\"}")
            : Response(200, current);
    }

    private static string? GetToken(string target, IReadOnlyDictionary<string, string> headers)
    {
        if (headers.TryGetValue("Authorization", out string? authorization)
            && authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return authorization["Bearer ".Length..].Trim();
        }

        int queryStart = target.IndexOf('?');
        if (queryStart < 0)
            return null;

        foreach (string pair in target[(queryStart + 1)..].Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            int separator = pair.IndexOf('=');
            string key = separator < 0 ? pair : pair[..separator];
            if (!string.Equals(Uri.UnescapeDataString(key), "token", StringComparison.Ordinal))
                continue;

            string value = separator < 0 ? "" : pair[(separator + 1)..];
            return Uri.UnescapeDataString(value.Replace('+', ' '));
        }

        return null;
    }

    private static string GetPath(string target)
    {
        int queryStart = target.IndexOf('?');
        return queryStart < 0 ? target : target[..queryStart];
    }

    private static async Task<string?> ReadLine(NetworkStream stream, int max, CancellationToken cancel)
    {
        var bytes = new List<byte>();
        var one = new byte[1];
        while (await stream.ReadAsync(one, cancel) == 1)
        {
            if (one[0] == (byte)'\n')
                return Encoding.UTF8.GetString(bytes.ToArray()).TrimEnd('\r');
            if (bytes.Count >= max)
                return null;
            bytes.Add(one[0]);
        }

        return null;
    }

    private static string Response(int status, string body, string extraHeaders = "")
    {
        byte[] bytes = Encoding.UTF8.GetBytes(body);
        string reason = status switch
        {
            200 => "OK",
            400 => "Bad Request",
            401 => "Unauthorized",
            404 => "Not Found",
            405 => "Method Not Allowed",
            503 => "Service Unavailable",
            _ => "Error"
        };
        return $"HTTP/1.1 {status} {reason}\r\nContent-Type: application/json\r\nContent-Length: {bytes.Length}\r\nCache-Control: no-store\r\nAccess-Control-Allow-Origin: *\r\n{extraHeaders}Connection: close\r\n\r\n{body}";
    }
}
