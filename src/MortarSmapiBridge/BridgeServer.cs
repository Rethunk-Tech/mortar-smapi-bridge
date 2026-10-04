using System.Net;
using System.Net.Sockets;
using System.Text;

namespace MortarSmapiBridge;

/// <summary>Loopback-only TCP server: one connection per command, <c>token\ncommand\n</c> in, <c>ok\n</c> or <c>error: msg\n</c> out.</summary>
internal sealed class BridgeServer : IDisposable
{
    public const int MaxCommandBytes = 4096;
    private const int MaxTokenBytes = 128;
    private static readonly TimeSpan ClientTimeout = TimeSpan.FromSeconds(5);

    private readonly TcpListener Listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource Cts = new();
    private readonly string Token;
    private readonly Func<string, string?> Submit;
    private readonly Action<string> Trace;

    /// <param name="token">The shared secret clients must present.</param>
    /// <param name="submit">Queues a validated command line; returns an error message, or null on success.</param>
    /// <param name="trace">Logs a received command.</param>
    public BridgeServer(string token, Func<string, string?> submit, Action<string> trace)
    {
        this.Token = token;
        this.Submit = submit;
        this.Trace = trace;
    }

    public int Port => ((IPEndPoint)this.Listener.LocalEndpoint).Port;

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
            string reply;
            try
            {
                var stream = client.GetStream();
                reply = await this.Process(stream, timeout.Token);
                await stream.WriteAsync(Encoding.UTF8.GetBytes(reply + "\n"), timeout.Token);
            }
            catch (Exception ex) when (ex is OperationCanceledException or IOException or SocketException or ObjectDisposedException)
            {
            }
        }
    }

    private async Task<string> Process(NetworkStream stream, CancellationToken cancel)
    {
        string? token = await CommandLine.ReadLine(stream, MaxTokenBytes, cancel);
        if (!CommandLine.TokenMatches(this.Token, token))
            return "error: unauthorized";

        string? line = await CommandLine.ReadLine(stream, MaxCommandBytes, cancel);
        if (line == null)
            return "error: missing or oversized command";
        if (!CommandLine.TryParse(line, out _, out _, out string error))
            return "error: " + error;

        this.Trace(line);
        string? failure = this.Submit(line);
        return failure == null ? "ok" : "error: " + failure;
    }

    /// <summary>Read one LF-terminated line of at most <paramref name="max"/> bytes; null if it is missing or longer.</summary>
}
