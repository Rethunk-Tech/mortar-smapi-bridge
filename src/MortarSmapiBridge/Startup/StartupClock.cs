using System.Collections.Concurrent;
using System.Diagnostics;

namespace MortarSmapiBridge.Startup;

/// <summary>Per-mod startup time, counted exclusively: a measurement excludes the measurements nested inside it, so a
/// mod whose update tick loads assets is not also charged for other mods' asset edits.</summary>
internal static class StartupClock
{
    internal sealed class Frame(string mod, string kind, string? pack, Frame? parent)
    {
        internal readonly string Mod = mod;
        internal readonly string Kind = kind;
        internal readonly string? Pack = pack;
        internal readonly Frame? Parent = parent;
        internal readonly long Start = Stopwatch.GetTimestamp();
        internal long Children;
    }

    internal readonly record struct Key(string Mod, string Kind, string? Pack);

    [ThreadStatic] private static Frame? current;
    private static volatile bool on;
    private static readonly ConcurrentDictionary<Key, long> ticks = new();

    internal static bool On => on;

    internal static void Start()
    {
        ticks.Clear();
        on = true;
    }

    internal static void Stop() => on = false;

    internal static Frame? Begin(string mod, string kind, string? pack = null)
    {
        if (!on)
            return null;
        return current = new Frame(mod, kind, pack, current);
    }

    internal static void End(Frame? frame)
    {
        if (frame == null)
            return;
        long total = Stopwatch.GetTimestamp() - frame.Start;
        current = frame.Parent;
        if (frame.Parent != null)
            frame.Parent.Children += total;
        long own = Math.Max(0, total - frame.Children);
        ticks.AddOrUpdate(new Key(frame.Mod, frame.Kind, frame.Pack), own, (_, old) => old + own);
    }

    internal static IReadOnlyDictionary<Key, long> Snapshot() => new Dictionary<Key, long>(ticks);

    internal static long Milliseconds(long stopwatchTicks) => stopwatchTicks * 1000 / Stopwatch.Frequency;
}
