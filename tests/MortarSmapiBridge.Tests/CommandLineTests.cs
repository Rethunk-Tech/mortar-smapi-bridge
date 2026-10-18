using System.Net;
using System.Net.Sockets;
using Xunit;

namespace MortarSmapiBridge.Tests;

public class CommandLineTests
{
    [Fact]
    public void SplitsNameAndArgs()
    {
        Assert.True(CommandLine.TryParse("player_add name  Abigail", out var name, out var args, out _));
        Assert.Equal("player_add", name);
        Assert.Equal(["name", "Abigail"], args);
    }

    [Fact]
    public void QuotesGroupAndEscapesApply()
    {
        Assert.True(CommandLine.TryParse("say \"hello world\" a\\ b \"q\\\"x\" \"\"", out var name, out var args, out _));
        Assert.Equal("say", name);
        Assert.Equal(["hello world", "a b", "q\"x", ""], args);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("\"\"")]
    public void EmptyIsRejected(string? line)
    {
        Assert.False(CommandLine.TryParse(line, out _, out _, out string error));
        Assert.Equal("empty command", error);
    }

    [Theory]
    [InlineData("say \"open")]
    [InlineData("say trailing\\")]
    public void UnterminatedIsRejected(string line)
    {
        Assert.False(CommandLine.TryParse(line, out _, out _, out string error));
        Assert.Equal("unterminated quote or escape", error);
    }

    [Fact]
    public void TokenCheck()
    {
        Assert.True(CommandLine.TokenMatches("abc123", "abc123"));
        Assert.False(CommandLine.TokenMatches("abc123", "abc124"));
        Assert.False(CommandLine.TokenMatches("abc123", "abc12"));
        Assert.False(CommandLine.TokenMatches("abc123", ""));
        Assert.False(CommandLine.TokenMatches("abc123", null));
    }

    public static TheoryData<byte[], int, string?> ReadLineCases() => new()
    {
        { "hello\n"u8.ToArray(), 16, "hello" },
        { "hello\r\n"u8.ToArray(), 16, "hello" },
        { "\n"u8.ToArray(), 16, "" },
        { "ab\ncd\n"u8.ToArray(), 16, "ab" },
        { "abcd"u8.ToArray(), 16, null },
        { "abcd\n"u8.ToArray(), 3, null },
        { [] , 16, null },
    };

    [Theory]
    [MemberData(nameof(ReadLineCases))]
    public async Task ReadLineStopsAtNewlineOrRejectsOversizeAndEof(byte[] payload, int max, string? expected)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        Task<TcpClient> accept = listener.AcceptTcpClientAsync();
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, ((IPEndPoint)listener.LocalEndpoint).Port);
        using TcpClient peer = await accept;
        await client.GetStream().WriteAsync(payload);
        client.Close();

        string? line = await CommandLine.ReadLine(peer.GetStream(), max, CancellationToken.None);
        Assert.Equal(expected, line);
    }
}
