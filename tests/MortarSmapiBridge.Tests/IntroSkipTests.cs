using MortarSmapiBridge.Startup;
using Xunit;

namespace MortarSmapiBridge.Tests;

public class IntroSkipTests
{
    private sealed class FakeTitleMenu
    {
        public int Skips;

        public void skipToTitleButtons() => this.Skips++;
    }

    [Fact]
    public void ARequestIsTheStartupFileInTheProfile()
    {
        string root = Directory.CreateTempSubdirectory("intro-skip").FullName;
        try
        {
            Assert.False(IntroSkip.Requested(root));
            Directory.CreateDirectory(Path.Combine(root, "startup"));
            File.WriteAllText(Path.Combine(root, "startup", "skip-intro"), "intro");
            Assert.True(IntroSkip.Requested(root));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void ATitleMenuIsSkippedOnce()
    {
        FakeTitleMenu menu = new();
        IntroSkip.Skip(menu);
        IntroSkip.Skip(menu);
        Assert.Equal(1, menu.Skips);

        FakeTitleMenu next = new();
        IntroSkip.Skip(next);
        Assert.Equal(1, next.Skips);
    }
}
