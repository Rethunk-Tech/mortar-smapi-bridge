using System.Text.Json;
using MortarSmapiBridge.Perf;
using Xunit;

namespace MortarSmapiBridge.Tests;

public class FrameStatsTests
{
    private static JsonElement Parse(FrameStats stats) => JsonDocument.Parse(stats.Json(0, 0, 0)).RootElement;

    private static JsonElement Mod(JsonElement root, string id) =>
        root.GetProperty("plugins").EnumerateArray().Single(p => p.GetProperty("guid").GetString() == id);

    [Fact]
    public void AveragesCountEveryFrameNotOnlyTheFramesAModRan()
    {
        var stats = new FrameStats();
        for (int i = 0; i < 10; i++)
        {
            if (i % 2 == 0)
                stats.Record("A.Mod", 4);
            stats.EndFrame(16);
        }
        JsonElement root = Parse(stats);
        JsonElement mod = Mod(root, "A.Mod");
        Assert.Equal(10, root.GetProperty("frames").GetInt64());
        Assert.Equal(2, mod.GetProperty("msPerFrame").GetDouble(), 3);
        Assert.Equal(4, mod.GetProperty("peakMs").GetDouble(), 3);
        Assert.Equal(0.5, mod.GetProperty("callsPerFrame").GetDouble(), 3);
        Assert.Equal(0.125, mod.GetProperty("share").GetDouble(), 4);
    }

    [Fact]
    public void ACallsInTheSameFrameAreSummed()
    {
        var stats = new FrameStats();
        stats.Record("A.Mod", 1);
        stats.Record("A.Mod", 2);
        stats.EndFrame(10);
        JsonElement mod = Mod(Parse(stats), "A.Mod");
        Assert.Equal(3, mod.GetProperty("msPerFrame").GetDouble(), 3);
        Assert.Equal(3, mod.GetProperty("peakMs").GetDouble(), 3);
        Assert.Equal(2, mod.GetProperty("callsPerFrame").GetDouble(), 3);
    }

    [Fact]
    public void P95SeesASpikeOnlyWhenItIsMoreThanFivePercentOfFrames()
    {
        var rare = new FrameStats();
        var common = new FrameStats();
        for (int i = 0; i < 100; i++)
        {
            rare.Record("A.Mod", i < 3 ? 50 : 1);
            common.Record("A.Mod", i < 10 ? 50 : 1);
            rare.EndFrame(16);
            common.EndFrame(16);
        }
        double rareP95 = Mod(Parse(rare), "A.Mod").GetProperty("p95Ms").GetDouble();
        double commonP95 = Mod(Parse(common), "A.Mod").GetProperty("p95Ms").GetDouble();
        Assert.InRange(rareP95, 0.9, 1.2);
        Assert.InRange(commonP95, 45, 50);
        Assert.Equal(50, Mod(Parse(rare), "A.Mod").GetProperty("peakMs").GetDouble(), 3);
    }

    [Fact]
    public void AModThatRanInFewFramesHasAZeroP95()
    {
        var stats = new FrameStats();
        for (int i = 0; i < 100; i++)
        {
            if (i == 0)
                stats.Record("A.Mod", 30);
            stats.EndFrame(16);
        }
        Assert.Equal(0, Mod(Parse(stats), "A.Mod").GetProperty("p95Ms").GetDouble());
    }

    [Fact]
    public void TheBaselineIsTheFrameLessTheModsAndModsAreSortedByCost()
    {
        var stats = new FrameStats();
        for (int i = 0; i < 4; i++)
        {
            stats.Record("Small", 1);
            stats.Record("Big", 3);
            stats.EndFrame(20);
        }
        JsonElement root = Parse(stats);
        JsonElement baseline = root.GetProperty("baseline");
        Assert.Equal(20, baseline.GetProperty("frameMs").GetDouble(), 3);
        Assert.Equal(4, baseline.GetProperty("modsMs").GetDouble(), 3);
        Assert.Equal(16, baseline.GetProperty("withoutModsMs").GetDouble(), 3);
        var ids = root.GetProperty("plugins").EnumerateArray().Select(p => p.GetProperty("guid").GetString()).ToList();
        Assert.Equal("Big", ids[0]);
        Assert.Equal("Small", ids[1]);
        Assert.Equal(50, root.GetProperty("fps").GetDouble(), 3);
    }

    [Fact]
    public void ResetStartsANewWindowAndAnEmptyOneIsValid()
    {
        var stats = new FrameStats();
        stats.Record("A.Mod", 5);
        stats.EndFrame(16);
        stats.Reset();
        JsonElement root = Parse(stats);
        Assert.True(root.GetProperty("measured").GetBoolean());
        Assert.Equal(0, root.GetProperty("frames").GetInt64());
        Assert.Empty(root.GetProperty("plugins").EnumerateArray());
        Assert.Equal(FrameProfiler.EmptySummary, stats.Json(0, 0, 0));
    }

    [Fact]
    public void ABadFrameTimeIsDroppedWithItsHandlerTime()
    {
        var stats = new FrameStats();
        stats.Record("A.Mod", 5);
        stats.EndFrame(double.NaN);
        Assert.Equal(0, stats.Frames);
        Assert.Empty(Parse(stats).GetProperty("plugins").EnumerateArray());
    }
}
