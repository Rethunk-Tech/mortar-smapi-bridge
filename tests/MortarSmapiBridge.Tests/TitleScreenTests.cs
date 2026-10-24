using MortarSmapiBridge.Startup;
using Xunit;

namespace MortarSmapiBridge.Tests;

public class TitleScreenTests
{
    private sealed class TitleMenu
    {
        public bool titleInPosition;
    }

    private sealed class GameMenu;

    [Fact]
    public void OnlyASettledTitleScreenCounts()
    {
        Assert.False(TitleScreen.Settled(null));
        Assert.False(TitleScreen.Settled(new GameMenu()));
        Assert.False(TitleScreen.Settled(new TitleMenu()));
        Assert.True(TitleScreen.Settled(new TitleMenu { titleInPosition = true }));
    }
}
