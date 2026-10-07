using System.Globalization;
using System.Text;

namespace MortarSmapiBridge.Perf;

/// <summary>Per-frame costs as a histogram of geometric buckets: a percentile costs one scan of a fixed array instead of
/// a sort of every frame, which would itself show up in the frames being measured. Values are milliseconds.</summary>
internal sealed class Histogram
{
    private const double FirstMs = 0.001;
    private const double Ratio = 1.1;
    private readonly long[] buckets = new long[160];
    private readonly double logRatio = Math.Log(Ratio);

    public double Max { get; private set; }

    public void Reset()
    {
        Array.Clear(this.buckets);
        this.Max = 0;
    }

    public void Add(double ms)
    {
        if (ms <= 0 || double.IsNaN(ms))
            return;
        int bucket = ms <= FirstMs ? 0 : (int)Math.Ceiling(Math.Log(ms / FirstMs) / this.logRatio);
        this.buckets[Math.Min(this.buckets.Length - 1, bucket)]++;
        this.Max = Math.Max(this.Max, ms);
    }

    /// <summary>The value at fraction p of <paramref name="total"/> samples, of which the ones not added are zeros; the
    /// upper edge of its bucket, never above the largest value seen (about 10% resolution).</summary>
    public double Percentile(double p, long total)
    {
        if (total <= 0)
            return 0;
        long rank = (long)Math.Ceiling(p * total);
        long zeros = total - this.buckets.Sum();
        if (rank <= zeros)
            return 0;
        long seen = zeros;
        for (int i = 0; i < this.buckets.Length; i++)
        {
            seen += this.buckets[i];
            if (seen >= rank)
                return Math.Min(this.Max, FirstMs * Math.Pow(Ratio, i));
        }
        return this.Max;
    }
}

/// <summary>Frame times and each mod's handler time per frame over a window. A frame's per-mod time is summed over
/// every timed handler call in it, so a mod that handles UpdateTicked and Rendered counts both in the same frame.</summary>
internal sealed class FrameStats
{
    private sealed class ModStats
    {
        public readonly Histogram Frames = new();
        public double TotalMs;
        public long FramesWithCalls;
        public long Calls;
    }

    private readonly Dictionary<string, ModStats> mods = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, (double Ms, long Calls)> current = new(StringComparer.OrdinalIgnoreCase);
    private readonly Histogram frames = new();
    private long frameCount;
    private double frameTotalMs;
    private double modsTotalMs;

    /// <summary>A timed handler of <paramref name="mod"/> took <paramref name="ms"/> in the frame being measured.</summary>
    public void Record(string mod, double ms)
    {
        current.TryGetValue(mod, out (double Ms, long Calls) now);
        current[mod] = (now.Ms + ms, now.Calls + 1);
    }

    /// <summary>The frame ended after <paramref name="frameMs"/>: fold the handler time recorded since the last one in.</summary>
    public void EndFrame(double frameMs)
    {
        if (frameMs < 0 || double.IsNaN(frameMs))
        {
            this.current.Clear();
            return;
        }
        this.frameCount++;
        this.frameTotalMs += frameMs;
        this.frames.Add(frameMs);
        foreach ((string mod, (double ms, long calls)) in this.current)
        {
            if (!this.mods.TryGetValue(mod, out ModStats? stats))
                this.mods[mod] = stats = new ModStats();
            stats.TotalMs += ms;
            stats.Calls += calls;
            stats.FramesWithCalls++;
            stats.Frames.Add(ms);
            this.modsTotalMs += ms;
        }
        this.current.Clear();
    }

    public long Frames => this.frameCount;

    public void Reset()
    {
        this.mods.Clear();
        this.current.Clear();
        this.frameCount = 0;
        this.frameTotalMs = 0;
        this.modsTotalMs = 0;
        this.frames.Reset();
    }

    /// <summary>The <c>perf</c> reply: frame times, each mod's cost per frame (average over every frame, 95th
    /// percentile, peak, share of the frame and calls per frame) and the baseline, the frame time less the mods'
    /// timed handlers.</summary>
    public string Json(long managedBytes, long heapBytes, int collections)
    {
        double perFrame = this.frameCount == 0 ? 0 : 1.0 / this.frameCount;
        double avg = this.frameTotalMs * perFrame;
        double modsAvg = this.modsTotalMs * perFrame;
        var sb = new StringBuilder("{\"measured\":true,\"seconds\":").Append(Num(this.frameTotalMs / 1000))
            .Append(",\"frames\":").Append(this.frameCount)
            .Append(",\"fps\":").Append(Num(this.frameTotalMs > 0 ? this.frameCount * 1000 / this.frameTotalMs : 0))
            .Append(",\"frameMs\":{\"avg\":").Append(Num(avg))
            .Append(",\"p50\":").Append(Num(this.frames.Percentile(0.5, this.frameCount)))
            .Append(",\"p95\":").Append(Num(this.frames.Percentile(0.95, this.frameCount)))
            .Append(",\"p99\":").Append(Num(this.frames.Percentile(0.99, this.frameCount)))
            .Append(",\"max\":").Append(Num(this.frames.Max))
            .Append("},\"monoUsedBytes\":").Append(managedBytes)
            .Append(",\"monoHeapBytes\":").Append(heapBytes)
            .Append(",\"gcCollections\":").Append(collections)
            .Append(",\"baseline\":{\"frameMs\":").Append(Num(avg))
            .Append(",\"modsMs\":").Append(Num(modsAvg))
            .Append(",\"withoutModsMs\":").Append(Num(Math.Max(0, avg - modsAvg)))
            .Append("},\"plugins\":[");
        bool first = true;
        foreach ((string mod, ModStats stats) in this.mods.OrderByDescending(m => m.Value.TotalMs))
        {
            if (!first)
                sb.Append(',');
            first = false;
            double modAvg = stats.TotalMs * perFrame;
            sb.Append("{\"guid\":").Append(Quote(mod))
                .Append(",\"msPerFrame\":").Append(Num(modAvg))
                .Append(",\"p95Ms\":").Append(Num(stats.Frames.Percentile(0.95, this.frameCount)))
                .Append(",\"peakMs\":").Append(Num(stats.Frames.Max))
                .Append(",\"share\":").Append(Num(avg > 0 ? modAvg / avg : 0, 4))
                .Append(",\"callsPerFrame\":").Append(Num(stats.Calls * perFrame)).Append('}');
        }
        return sb.Append("]}").ToString();
    }

    private static string Num(double value, int digits = 3) =>
        Math.Round(value, digits).ToString("0.####", CultureInfo.InvariantCulture);

    private static string Quote(string s) => System.Text.Json.JsonSerializer.Serialize(s);
}
