using System.Text.Json;
using MortarSmapiBridge.Perf;
using Xunit;

namespace MortarSmapiBridge.Tests;

public class FrameProfilerTests
{
    private const string Summary = "{\"measured\":true,\"frames\":3}";

    [Fact]
    public void PerfAndPerfStartStayTheExistingReplies()
    {
        Assert.Equal(Summary, FrameProfiler.Query("perf", Summary, out bool perfStart, out bool perfStop));
        Assert.False(perfStart);
        Assert.False(perfStop);

        Assert.Equal("{\"measured\":true}", FrameProfiler.Query("perf start", Summary, out bool start, out bool startStop));
        Assert.True(start);
        Assert.False(startStop);
    }

    [Fact]
    public void PerfStopReturnsTheCurrentSummaryAndIsNotAConsoleLine()
    {
        string? json = FrameProfiler.Query("perf stop", Summary, out bool start, out bool stop);
        Assert.Equal(Summary, json);
        Assert.False(start);
        Assert.True(stop);
        Assert.Null(FrameProfiler.Query("help", Summary, out _, out _));
        Assert.Null(FrameProfiler.Query("perf restart", Summary, out _, out _));
    }

    [Fact]
    public void StopThenStartOnTheSameTickOpensANewWindow()
    {
        var profiler = new FrameProfiler();
        profiler.TryAnswer("perf stop");
        profiler.TryAnswer("perf start");
        profiler.Tick();
        Assert.True(profiler.On);
        using JsonDocument json = JsonDocument.Parse(profiler.TryAnswer("perf")!);
        Assert.True(json.RootElement.GetProperty("measured").GetBoolean());
        Assert.True(json.RootElement.GetProperty("active").GetBoolean());
    }

    [Fact]
    public void EmptyAndStoppedSummariesKeepMeasuredAndAreInactive()
    {
        using JsonDocument empty = JsonDocument.Parse(FrameProfiler.EmptySummary);
        Assert.True(empty.RootElement.GetProperty("measured").GetBoolean());
        Assert.False(empty.RootElement.GetProperty("active").GetBoolean());

        var profiler = new FrameProfiler();
        profiler.TryAnswer("perf start");
        profiler.Tick();
        profiler.TryAnswer("perf stop");
        profiler.Tick();
        using JsonDocument stopped = JsonDocument.Parse(profiler.TryAnswer("perf")!);
        Assert.True(stopped.RootElement.GetProperty("measured").GetBoolean());
        Assert.False(stopped.RootElement.GetProperty("active").GetBoolean());
        Assert.False(profiler.On);
    }
}
