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
}
