using System.Diagnostics;
using System.Text.Json;
using MortarSmapiBridge.Startup;
using Xunit;

namespace MortarSmapiBridge.Tests;

public class StartupTests
{
    private static void Spin(int ms)
    {
        Stopwatch sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < ms) { }
    }

    [Fact]
    public void NestedTimeIsChargedOnlyToTheInnerMod()
    {
        StartupClock.Start();
        StartupClock.Frame? outer = StartupClock.Begin("Pathoschild.ContentPatcher", "UpdateTicked");
        Spin(20);
        StartupClock.Frame? inner = StartupClock.Begin("Pathoschild.ContentPatcher", "asset", "Some.Pack");
        Spin(60);
        StartupClock.End(inner);
        StartupClock.End(outer);
        StartupClock.Stop();

        var ticks = StartupClock.Snapshot();
        long outerMs = StartupClock.Milliseconds(ticks[new StartupClock.Key("Pathoschild.ContentPatcher", "UpdateTicked", null)]);
        long innerMs = StartupClock.Milliseconds(ticks[new StartupClock.Key("Pathoschild.ContentPatcher", "asset", "Some.Pack")]);
        Assert.InRange(innerMs, 55, 200);
        Assert.InRange(outerMs, 15, 50);
    }

    [Fact]
    public void NothingIsRecordedWhileStopped()
    {
        StartupClock.Start();
        StartupClock.Stop();
        Assert.Null(StartupClock.Begin("a", "entry"));
        Assert.Empty(StartupClock.Snapshot());
    }

    [Fact]
    public void GroupPutsPackTimeUnderItsFrameworkAndSortsBySlowest()
    {
        long Ms(long ms) => ms * Stopwatch.Frequency / 1000;
        var ticks = new Dictionary<StartupClock.Key, long>
        {
            [new("CP", "asset", "Pack.A")] = Ms(300),
            [new("CP", "asset", "Pack.B")] = Ms(900),
            [new("CP", "UpdateTicked", null)] = Ms(100),
            [new("CP", "load", "Pack.A")] = Ms(1000),
            [new("FS", "entry", null)] = Ms(2000),
            [new("Quiet", "Rendered", null)] = 0,
        };
        List<StartupMod> mods = StartupReport.Group(ticks, _ => null);

        Assert.Equal(["CP", "FS"], mods.Select(m => m.Id));
        StartupMod cp = mods[0];
        Assert.Equal(1200, cp.AssetMs);
        Assert.Equal(1000, cp.LoadMs);
        Assert.Equal(["Pack.A", "Pack.B"], cp.Packs.Select(p => p.Id));
        Assert.Equal(1300, cp.Packs[0].Ms);
        Assert.Equal(100, cp.EventMs["UpdateTicked"]);
        Assert.Equal(2000, mods[1].EntryMs);
    }

    [Fact]
    public void WriteKeepsTheNewestReports()
    {
        string dir = Directory.CreateTempSubdirectory("startup").FullName;
        try
        {
            DateTime start = new(2026, 10, 4, 0, 0, 0, DateTimeKind.Utc);
            string last = "";
            for (int i = 0; i < StartupReport.Kept + 3; i++)
                last = StartupReport.Write(dir, new StartupReport { ProcessStart = start.AddMinutes(i) });
            string[] files = Directory.GetFiles(Path.Combine(dir, "startup"));
            Assert.Equal(StartupReport.Kept, files.Length);
            Assert.Contains(last, files);
            using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(last));
            Assert.Equal(1, doc.RootElement.GetProperty("schema").GetInt32());
            Assert.True(doc.RootElement.TryGetProperty("phases", out _));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
