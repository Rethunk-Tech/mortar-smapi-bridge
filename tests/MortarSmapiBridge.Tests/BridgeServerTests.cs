using System.Net.Sockets;
using System.Text;
using Xunit;

namespace MortarSmapiBridge.Tests;

public class BridgeServerTests
{
    private static async Task<string> Ask(BridgeServer server, string token, string line)
    {
        using var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", server.Port);
        var stream = client.GetStream();
        await stream.WriteAsync(Encoding.UTF8.GetBytes($"{token}\n{line}\n"));
        using var reader = new StreamReader(stream);
        return (await reader.ReadLineAsync())!;
    }

    [Fact]
    public async Task AQueryIsAnsweredWithJsonAndNeverQueuedAsACommand()
    {
        var queued = new List<string>();
        var traced = new List<string>();
        using var server = new BridgeServer("secret", queued.Add, traced.Add,
            line => line == "perf" ? "{\"measured\":true}" : null);
        server.Start();
        Assert.Equal("ok {\"measured\":true}", await Ask(server, "secret", "perf"));
        Assert.Equal("ok", await Ask(server, "secret", "player_add name Abigail"));
        Assert.Equal("error: unauthorized", await Ask(server, "wrong", "perf"));
        Assert.Equal(["player_add name Abigail"], queued);
        Assert.Equal(["player_add"], traced);
    }

    [Fact]
    public async Task AThrowingSubmitAnswersAnErrorAndTheServerKeepsServing()
    {
        using var server = new BridgeServer("secret", line => throw new InvalidOperationException("queue gone"), _ => { }, _ => null);
        server.Start();
        Assert.Equal("error: queue gone", await Ask(server, "secret", "help"));
        Assert.Equal("error: queue gone", await Ask(server, "secret", "help"));
    }

    [Fact]
    public void StatusListsGameVersionAndModsAndOmitsWhatSmapiCannotGive()
    {
        Assert.Equal(
            "{\"gameVersion\":\"1.6.15\",\"gameVersionSource\":\"game\",\"plugins\":[{\"guid\":\"a.b\",\"version\":\"1.2.3\"}]}",
            StatusReply.Build("1.6.15", [("a.b", "1.2.3")]));
        Assert.Equal("{\"plugins\":[]}", StatusReply.Build(null, []));
    }

    [Fact]
    public async Task ExcessConnectionsAreClosed()
    {
        using var server = new BridgeServer("secret", _ => { }, _ => { }, _ => null);
        server.Start();
        List<TcpClient> held = [];
        using var heldClients = new HeldClients(held);
        for (int i = 0; i < BridgeServer.MaxInFlight; i++)
        {
            TcpClient client = new();
            await client.ConnectAsync("127.0.0.1", server.Port);
            held.Add(client);
        }

        using var extra = new TcpClient();
        await extra.ConnectAsync("127.0.0.1", server.Port);
        using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(250));
        Assert.Equal(0, await extra.GetStream().ReadAsync(new byte[1], timeout.Token));
    }

    private sealed class HeldClients(List<TcpClient> clients) : IDisposable
    {
        public void Dispose()
        {
            foreach (TcpClient client in clients)
                client.Dispose();
        }
    }

    [Fact]
    public void DisposeIsIdempotent()
    {
        using var server = new BridgeServer("secret", _ => { }, _ => { }, _ => null);
        server.Start();
        server.Dispose();
    }
}
