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
}
