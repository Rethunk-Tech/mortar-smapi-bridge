using System.Net;
using System.Net.Sockets;
using System.Text;

namespace MortarSmapiBridge;

/// <summary>Loopback-only TCP server: one connection per command, <c>token\ncommand\n</c> in, <c>ok\n</c>, <c>ok {json}\n</c> for a query or <c>error: msg\n</c> out.</summary>
/// <param name="token">The shared secret clients must present.</param>
/// <param name="submit">Queues a validated command line.</param>
/// <param name="trace">Logs a received command.</param>
/// <param name="query">Answers a line that asks a question instead of running a command with JSON, or null when it is not one.</param>
internal sealed class BridgeServer(string token, Action<string> submit, Action<string> trace, Func<string, string?> query) : IDisposable
{
    private const int MaxCommandBytes = 4096;
    private const int MaxTokenBytes = 128;
    internal const int MaxInFlight = 8;
    private static readonly TimeSpan ClientTimeout = TimeSpan.FromSeconds(5);

    private readonly TcpListener Listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource Cts = new();
    private int disposed;
    private int inFlight;
    private readonly string Token = token;
    private readonly Action<string> Submit = submit;
    private readonly Action<string> Trace = trace;
    private readonly Func<string, string?> Query = query;

    public int Port => ((IPEndPoint)this.Listener.LocalEndpoint).Port;

    public void Start()
    {
        this.Listener.Start();
        _ = Task.Run(this.AcceptLoop);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref this.disposed, 1) != 0)
            return;
        try
        {
            this.Cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
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
            if (Interlocked.Increment(ref this.inFlight) > MaxInFlight)
            {
                Interlocked.Decrement(ref this.inFlight);
                client.Dispose();
                continue;
            }
            _ = Task.Run(() => this.Handle(client));
        }
    }

    private async Task Handle(TcpClient client)
    {
        try
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
        finally
        {
            Interlocked.Decrement(ref this.inFlight);
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
        if (!CommandLine.TryParse(line, out string name, out _, out string error))
            return "error: " + error;

        if (this.Query(line) is { } answer)
            return "ok " + answer;
        this.Trace(name);
        this.Submit(line);
        return "ok";
    }
}
