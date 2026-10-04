using System.Text.Json;
using System.Text.Json.Serialization;

namespace MortarSmapiBridge.Startup;

/// <summary>One launch's startup timings, read by Mortar's Performance › Startup. Times in phases are milliseconds from process start.</summary>
internal sealed class StartupReport
{
    internal const int Kept = 10;

    [JsonPropertyName("schema")] public int Schema { get; init; } = 1;
    [JsonPropertyName("smapi")] public string Smapi { get; init; } = "";
    [JsonPropertyName("game")] public string Game { get; init; } = "";
    [JsonPropertyName("processStart")] public DateTime ProcessStart { get; init; }
    [JsonPropertyName("phases")] public StartupPhases Phases { get; init; } = new();
    [JsonPropertyName("entryTimed")] public bool EntryTimed { get; init; }
    [JsonPropertyName("entryMissed")] public int EntryMissed { get; init; }
    [JsonPropertyName("mods")] public List<StartupMod> Mods { get; init; } = [];
    [JsonPropertyName("otherMs")] public long OtherMs { get; init; }

    internal static string Write(string profileDirectory, StartupReport report)
    {
        string directory = Path.Combine(profileDirectory, "startup");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, report.ProcessStart.ToUniversalTime().ToString("yyyyMMdd'T'HHmmss'Z'", System.Globalization.CultureInfo.InvariantCulture) + ".json");
        string temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(report));
        File.Move(temp, path, true);
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
    [JsonPropertyName("bridgeEntry")] public long BridgeEntry { get; set; }
    [JsonPropertyName("entryDone")] public long EntryDone { get; set; }
    [JsonPropertyName("gameLaunched")] public long GameLaunched { get; set; }
    [JsonPropertyName("titleMenu")] public long TitleMenu { get; set; }
    [JsonPropertyName("titleScreen")] public long TitleScreen { get; set; }
}

internal sealed class StartupMod
{
    [JsonPropertyName("id")] public string Id { get; init; } = "";
    [JsonPropertyName("name")] public string Name { get; init; } = "";
    [JsonPropertyName("version")] public string Version { get; init; } = "";
    [JsonPropertyName("entryMs")] public long EntryMs { get; set; }
    [JsonPropertyName("eventMs")] public Dictionary<string, long> EventMs { get; init; } = [];
    /// <summary>Asset edits and loads, including the content packs listed in Packs.</summary>
    [JsonPropertyName("assetMs")] public long AssetMs { get; set; }
    /// <summary>Time spent loading its content packs (Content Patcher), the sum of the packs' loadMs.</summary>
    [JsonPropertyName("loadMs")] public long LoadMs { get; set; }
    [JsonPropertyName("packs")] public List<StartupPack> Packs { get; init; } = [];

    [JsonIgnore] internal long TotalMs => this.EntryMs + this.AssetMs + this.LoadMs + this.EventMs.Values.Sum();
}

internal sealed class StartupPack
{
    [JsonPropertyName("id")] public string Id { get; init; } = "";
    [JsonPropertyName("name")] public string Name { get; init; } = "";
    [JsonPropertyName("assetMs")] public long AssetMs { get; set; }
    [JsonPropertyName("loadMs")] public long LoadMs { get; set; }
    [JsonPropertyName("ms")] public long Ms => this.AssetMs + this.LoadMs;
}
