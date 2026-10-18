using System.Diagnostics;
using HarmonyLib;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using MortarSmapiBridge.Startup;

namespace MortarSmapiBridge.Perf;

/// <summary>Times each mod's handlers on the per-frame SMAPI events while Mortar is profiling. Nothing is wrapped until
/// the first <c>perf start</c>, and the wrappers come off again when the window ends, so play is not slowed otherwise.
/// A frame runs from one UpdateTicking to the next; the handlers' time is summed per mod per frame. SMAPI's own
/// performance monitor keeps only a rolling average and peak per mod and event, so it has no 95th percentile or
/// share of the frame; the handlers are timed here instead, by the same delegate swap the startup timing uses.</summary>
internal sealed class FrameProfiler
{
    // A window this long is plenty for a measurement; the wrappers come off after it and the summary stays.
    private static readonly long MaxWindow = Stopwatch.Frequency * 600;
    private static readonly HashSet<string> FrameEvents =
    [
        "UpdateTicking", "UpdateTicked", "OneSecondUpdateTicking", "OneSecondUpdateTicked",
        "Rendering", "Rendered", "RenderingWorld", "RenderedWorld", "RenderingActiveMenu", "RenderedActiveMenu",
        "RenderingHud", "RenderedHud", "RenderingStep", "RenderedStep",
    ];

    private readonly IMonitor monitor;
    private readonly string ownId;
    private readonly Harmony harmony;
    private readonly FrameStats stats = new();
    private readonly EventTiming events;
    private volatile bool restart;
    private volatile bool halt;
    private volatile string summary = EmptySummary;
    private bool attached;
    private bool on;
    private int mainThread;
    private long last;
    private long windowStart;
    private long nextSummary;
    private int collectionsAtStart;

    internal static readonly string EmptySummary = new FrameStats().Json(0, 0, 0);

    internal FrameProfiler(IModHelper helper, IMonitor monitor, string ownId)
    {
        this.monitor = monitor;
        this.ownId = ownId;
        this.harmony = new Harmony(ownId + ".frames");
        this.events = new EventTiming(name => FrameEvents.Contains(name), this.Record);
        helper.Events.GameLoop.UpdateTicking += this.OnUpdateTicking;
    }

    /// <summary>Answers a <c>perf</c> query on a socket thread. Start and stop only flag the game thread.</summary>
    internal string? TryAnswer(string line)
    {
        string? json = Query(line, this.summary, out bool start, out bool stop);
        if (start)
            this.restart = true;
        if (stop)
            this.halt = true;
        return json;
    }

    /// <summary><c>perf</c> returns <paramref name="summary"/>; <c>perf start</c> acks; <c>perf stop</c> returns the
    /// last summary and asks the game thread to unwrap. Anything else is a console command.</summary>
    internal static string? Query(string line, string summary, out bool start, out bool stop)
    {
        start = false;
        stop = false;
        switch (line)
        {
            case "perf":
                return summary;
            case "perf start":
                start = true;
                return "{\"measured\":true}";
            case "perf stop":
                stop = true;
                return summary;
            default:
                return null;
        }
    }

    private void Record(string mod, string eventName, long ticks)
    {
        if (this.on && Environment.CurrentManagedThreadId == this.mainThread && !string.Equals(mod, this.ownId, StringComparison.OrdinalIgnoreCase))
            this.stats.Record(mod, ticks * 1000.0 / Stopwatch.Frequency);
    }

    [EventPriority((EventPriority)1001)]
    private void OnUpdateTicking(object? sender, UpdateTickingEventArgs e)
    {
        if (!this.restart && !this.on && !this.halt)
            return;
        try
        {
            this.Tick();
        }
        catch (Exception ex)
        {
            this.Stop();
            this.monitor.Log($"Frame profiling is off for this launch: {ex.Message}", LogLevel.Trace);
        }
    }

    private void Tick()
    {
        if (this.halt)
        {
            this.halt = false;
            this.restart = false;
            this.Stop();
            return;
        }

        long now = Stopwatch.GetTimestamp();
        if (this.restart)
        {
            this.restart = false;
            if (!this.attached && !this.Attach())
                return;
            this.stats.Reset();
            this.mainThread = Environment.CurrentManagedThreadId;
            this.collectionsAtStart = GC.CollectionCount(0);
            this.windowStart = now;
            this.last = 0;
            this.nextSummary = now;
            this.on = true;
        }
        if (this.last != 0)
            this.stats.EndFrame((now - this.last) * 1000.0 / Stopwatch.Frequency);
        this.last = now;
        this.events.WrapNew();
        if (now >= this.nextSummary)
        {
            this.nextSummary = now + Stopwatch.Frequency;
            this.summary = this.Summarise();
        }
        if (now - this.windowStart > MaxWindow)
            this.Stop();
    }

    private bool Attach()
    {
        if (!this.events.Attach() || !this.events.PatchRemove(this.harmony))
        {
            this.monitor.Log("Frame profiling: SMAPI's event manager has an unexpected shape; per-mod frame times are off.", LogLevel.Trace);
            return false;
        }
        this.attached = true;
        return true;
    }

    private void Stop()
    {
        if (!this.on)
            return;
        this.on = false;
        this.summary = this.Summarise();
        this.events.Restore();
    }

    private string Summarise() =>
        this.stats.Json(GC.GetTotalMemory(false), GC.GetGCMemoryInfo().HeapSizeBytes, GC.CollectionCount(0) - this.collectionsAtStart);
}
