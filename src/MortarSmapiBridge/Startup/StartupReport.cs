using System.Text.Json;
using System.Text.Json.Serialization;

namespace MortarSmapiBridge.Startup;

/// <summary>One launch's startup timings, read by Mortar's Performance › Startup. Times in phases are milliseconds from process start.</summary>
internal sealed class StartupReport
{
    internal const int Kept = 10;

    public int Schema { get; init; } = 1;
    public string Smapi { get; init; } = "";
    public string Game { get; init; } = "";
    public DateTime ProcessStart { get; init; }
    public StartupPhases Phases { get; init; } = new();
    public bool EntryTimed { get; init; }
    public int EntryMissed { get; init; }
    public List<StartupMod> Mods { get; init; } = [];
    public long OtherMs { get; init; }
    /// <summary>Harmony owner to the methods it can replace; see <see cref="ReplacedMethods"/>.</summary>
    public Dictionary<string, List<string>> Replaces { get; init; } = [];

    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    internal static string Write(string profileDirectory, StartupReport report)
    {
        string directory = Path.Combine(profileDirectory, "startup");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, report.ProcessStart.ToUniversalTime().ToString("yyyyMMdd'T'HHmmss'Z'", System.Globalization.CultureInfo.InvariantCulture) + ".json");
        Gmcm.GmcmSession.AtomicWrite(path, JsonSerializer.Serialize(report, WebJson));
        foreach (string old in Directory.EnumerateFiles(directory, "*.json").OrderByDescending(f => f, StringComparer.Ordinal).Skip(Kept))
            File.Delete(old);
        return path;
    }

    /// <summary>Groups exclusive times by mod; content-pack asset time goes under the framework that applied it.</summary>
    internal static List<StartupMod> Group(IReadOnlyDictionary<StartupClock.Key, long> ticks, Func<string, (string Name, string Version)?> manifestOf)
    {
        Dictionary<string, StartupMod> mods = new(StringComparer.OrdinalIgnoreCase);
        StartupMod Mod(string id) => mods.TryGetValue(id, out StartupMod? m) ? m : mods[id] = new StartupMod { Id = id, Name = manifestOf(id)?.Name ?? id, Version = manifestOf(id)?.Version ?? "" };
        foreach ((StartupClock.Key key, long t) in ticks)
        {
            long ms = StartupClock.Milliseconds(t);
            StartupMod mod = Mod(key.Mod);
            if (key.Kind == "entry")
                mod.EntryMs += ms;
            else if (key.Kind == "asset" && key.Pack == null)
                mod.AssetMs += ms;
            else if (key.Pack != null)
            {
                StartupPack pack = mod.Packs.Find(p => p.Id == key.Pack) ?? Add(mod, new StartupPack { Id = key.Pack, Name = manifestOf(key.Pack)?.Name ?? key.Pack });
                if (key.Kind == "load")
                    pack.LoadMs += ms;
                else
                    pack.AssetMs += ms;
            }
            else
                mod.EventMs[key.Kind] = mod.EventMs.GetValueOrDefault(key.Kind) + ms;
        }
        foreach (StartupMod mod in mods.Values)
        {
            mod.AssetMs += mod.Packs.Sum(p => p.AssetMs);
            mod.LoadMs = mod.Packs.Sum(p => p.LoadMs);
            mod.Packs.Sort((a, b) => b.Ms.CompareTo(a.Ms));
            foreach (string quiet in mod.EventMs.Where(p => p.Value == 0).Select(p => p.Key).ToList())
                mod.EventMs.Remove(quiet);
        }
        return mods.Values.Where(m => m.TotalMs > 0).OrderByDescending(m => m.TotalMs).ToList();
    }

    private static StartupPack Add(StartupMod mod, StartupPack pack)
    {
        mod.Packs.Add(pack);
        return pack;
    }
}

internal sealed class StartupPhases
{
    public long BridgeEntry { get; set; }
    public long EntryDone { get; set; }
    public long GameLaunched { get; set; }
    public long TitleMenu { get; set; }
    public long TitleScreen { get; set; }
}

internal sealed class StartupMod
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Version { get; init; } = "";
    public long EntryMs { get; set; }
    public Dictionary<string, long> EventMs { get; init; } = [];
    /// <summary>Asset edits and loads, including the content packs listed in Packs.</summary>
    public long AssetMs { get; set; }
    /// <summary>Time spent loading its content packs (Content Patcher), the sum of the packs' loadMs.</summary>
    public long LoadMs { get; set; }
    public List<StartupPack> Packs { get; init; } = [];

    [JsonIgnore] internal long TotalMs => this.EntryMs + this.AssetMs + this.LoadMs + this.EventMs.Values.Sum();
}

internal sealed class StartupPack
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public long AssetMs { get; set; }
    public long LoadMs { get; set; }
    public long Ms => this.AssetMs + this.LoadMs;
}
