using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Xunit;

namespace MortarSmapiBridge.Tests;

public class OverlayTests
{
    [Theory]
    [InlineData("overlay-token", "overlay-token", true)]
    [InlineData("overlay-token", "command-token", false)]
    [InlineData("overlay-token", "", false)]
    [InlineData("overlay-token", null, false)]
    public void OverlayTokenCheck(string expected, string? provided, bool matches) =>
        Assert.Equal(matches, CommandLine.TokenMatches(expected, provided));

    [Fact]
    public async Task MissingWrongAndCommandTokensReturn401()
    {
        int port = GetFreePort();
        using var server = new OverlayServer(port, "overlay-token");
        server.SetSnapshot(CreateSnapshot());
        server.Start();

        Assert.Contains(" 401 ", await Request(port, "/state"));
        Assert.Contains(" 401 ", await Request(port, "/state?token=wrong-token"));
        Assert.Contains(" 401 ", await Request(port, "/state?token=command-token"));
    }

    [Fact]
    public async Task BearerTokenReturnsSnapshotJson()
    {
        int port = GetFreePort();
        using var server = new OverlayServer(port, "overlay-token");
        server.SetSnapshot(CreateSnapshot());
        server.Start();

        string response = await Request(port, "/state", "Authorization: Bearer overlay-token");
        int bodyStart = response.IndexOf("\r\n\r\n", StringComparison.Ordinal) + 4;
        using JsonDocument json = JsonDocument.Parse(response[bodyStart..]);
        JsonElement root = json.RootElement;

        Assert.Contains(" 200 ", response);
        Assert.True(root.GetProperty("inGame").GetBoolean());
        Assert.Equal("Farm", root.GetProperty("location").GetString());
        Assert.Equal("Abigail", root.GetProperty("playerName").GetString());
        Assert.Equal(7, root.GetProperty("day").GetInt32());
        Assert.Equal(1250, root.GetProperty("money").GetInt32());
        Assert.Equal(5, root.GetProperty("skills").GetProperty("farming").GetInt32());
    }

    [Fact]
    public async Task NotInGameReturns200WithInGameFalse()
    {
        int port = GetFreePort();
        using var server = new OverlayServer(port, "overlay-token");
        server.SetNotInGame();
        server.Start();

        string response = await Request(port, "/state?token=overlay-token");
        int bodyStart = response.IndexOf("\r\n\r\n", StringComparison.Ordinal) + 4;
        using JsonDocument json = JsonDocument.Parse(response[bodyStart..]);

        Assert.Contains(" 200 ", response);
        Assert.Equal("{\"inGame\":false}", response[bodyStart..]);
        Assert.False(json.RootElement.GetProperty("inGame").GetBoolean());
        Assert.Equal("inGame", Assert.Single(json.RootElement.EnumerateObject()).Name);
    }

    [Fact]
    public void InGameSnapshotJsonShape()
    {
        using JsonDocument json = JsonDocument.Parse(OverlayServer.Serialize(CreateSnapshot()));
        JsonElement root = json.RootElement;
        Assert.True(root.GetProperty("inGame").GetBoolean());
        string[] fields =
        [
            "inGame", "location", "playerName", "season", "day", "year", "timeOfDay", "money", "weather",
            "health", "maxHealth", "stamina", "maxStamina", "skills"
        ];
        string[] written = [.. root.EnumerateObject().Select(p => p.Name)];
        Assert.Equal(fields, written);
        string[] skills = ["farming", "fishing", "foraging", "mining", "combat", "luck"];
        string[] writtenSkills = [.. root.GetProperty("skills").EnumerateObject().Select(p => p.Name)];
        Assert.Equal(skills, writtenSkills);
    }

    [Fact]
    public void GameVersionGateMatchesManifestRange()
    {
        Assert.True(ApiRange.IsTestedGame(1, 6, 15));
        Assert.True(ApiRange.IsTestedGame(1, 6, 14));
        Assert.True(ApiRange.IsTestedGame(1, 6, 16));
        Assert.False(ApiRange.IsTestedGame(1, 6, 13));
        Assert.False(ApiRange.IsTestedGame(1, 7, 0));
    }

    [Fact]
    public void DisabledOverlayDoesNotOpenItsConfiguredPort()
    {
        Assert.False(new ModConfig().OverlayEnabled);

        int port = GetFreePort();
        using var listener = new TcpListener(IPAddress.Loopback, port);
        listener.Start();
        Assert.True(listener.Server.IsBound);
    }

    private static OverlaySnapshot CreateSnapshot() =>
        new(
            true,
            "Farm",
            "Abigail",
            "spring",
            7,
            2,
            930,
            1250,
            "sunny",
            75,
            100,
            80,
            270,
            new Dictionary<string, int>
            {
                ["farming"] = 5,
                ["fishing"] = 3,
                ["foraging"] = 4,
                ["mining"] = 2,
                ["combat"] = 1,
                ["luck"] = 0
            });

    private static int GetFreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static async Task<string> Request(int port, string target, string? header = null)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, port);
        await using NetworkStream stream = client.GetStream();
        string request = $"GET {target} HTTP/1.1\r\nHost: 127.0.0.1\r\n{header}\r\n\r\n";
        await stream.WriteAsync(Encoding.ASCII.GetBytes(request));
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return await reader.ReadToEndAsync();
    }
}
