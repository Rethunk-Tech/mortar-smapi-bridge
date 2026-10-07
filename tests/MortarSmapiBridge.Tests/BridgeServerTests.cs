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
        using var server = new BridgeServer("secret", line => { queued.Add(line); return null; }, _ => { },
            line => line == "perf" ? "{\"measured\":true}" : null);
        server.Start();
        Assert.Equal("ok {\"measured\":true}", await Ask(server, "secret", "perf"));
        Assert.Equal("ok", await Ask(server, "secret", "player_add name Abigail"));
        Assert.Equal("error: unauthorized", await Ask(server, "wrong", "perf"));
        Assert.Equal(["player_add name Abigail"], queued);
    }
}
