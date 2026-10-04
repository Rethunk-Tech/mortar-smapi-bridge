using System.Diagnostics;
using System.Reflection;
using HarmonyLib;
using StardewModdingAPI;
using StardewModdingAPI.Events;

namespace MortarSmapiBridge.Startup;

/// <summary>Measures each mod's share of the time from the bridge's Entry to the title screen and writes a report
/// into the Mortar profile. Every hook is removed at the title screen, so play is not slowed. On a measured launch
/// Mortar samples the process from outside; the report's phases tell it where startup ended.</summary>
internal sealed class StartupSession
{
    // Long enough for any real startup; stops timing if the title screen is never seen (e.g. a mod skips it).
    private static readonly TimeSpan GiveUp = TimeSpan.FromMinutes(15);

    private readonly IModHelper helper;
    private readonly IMonitor monitor;
    private readonly string ownId;
    private readonly string profileDir;
    private readonly DateTime processStart;
    private readonly StartupPhases phases = new();
    private readonly Harmony harmony;
    private EventTiming? events;
    private readonly AssetTiming assets = new();
    private bool entryTimed;
    private int entryMissed;
    private bool done;
    private bool skipIntro;

    private StartupSession(IModHelper helper, IMonitor monitor, string ownId, string profileDir)
    {
        this.helper = helper;
        this.monitor = monitor;
        this.ownId = ownId;
        this.profileDir = profileDir;
        this.processStart = Process.GetCurrentProcess().StartTime.ToUniversalTime();
        this.harmony = new Harmony(ownId + ".startup");
    }

    /// <summary>Starts timing; does nothing outside a Mortar profile (there is nowhere to put the report).</summary>
    internal static void Start(IModHelper helper, IMonitor monitor, IManifest manifest, ModConfig config)
    {
        if (!config.StartupTimings || ProfileDirectory.Find(helper.DirectoryPath) is not { } profileDir)
            return;
        StartupSession session = new(helper, monitor, manifest.UniqueID, profileDir);
        try
        {
            session.Begin(config.StartupProfile);
        }
        catch (Exception ex)
        {
            session.Abandon(ex);
        }
    }

    private void Begin(bool profile)
    {
        this.phases.BridgeEntry = this.Now();
        this.skipIntro = profile;
        StartupClock.Start();
        ContentPatcherTiming.Patch(this.harmony, this.helper);
        this.events = new EventTiming();
        if (!this.events.Attach())
        {
            this.monitor.Log("Startup timing: SMAPI's event manager has an unexpected shape; event times are off.", LogLevel.Trace);
            this.events = null;
        }
        if (profile)
        {
            HashSet<string> ran = new(StringComparer.OrdinalIgnoreCase);
            foreach (IModInfo mod in this.helper.ModRegistry.GetAll())
            {
                if (string.Equals(mod.Manifest.UniqueID, this.ownId, StringComparison.OrdinalIgnoreCase))
                    break;
                ran.Add(mod.Manifest.UniqueID);
            }
            this.entryMissed = EntryTiming.Patch(this.harmony, this.helper.ModRegistry.GetAll(), this.ownId, ran);
            this.entryTimed = true;
        }
        this.helper.Events.Content.AssetRequested += this.OnAssetRequested;
        this.helper.Events.Content.AssetRequested += this.OnAssetRequestedLast;
        this.helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
        this.helper.Events.GameLoop.UpdateTicking += this.OnUpdateTicking;
        this.helper.Events.Display.RenderedActiveMenu += this.OnRenderedActiveMenu;
        this.events?.WrapNew();
    }

    // Runs before other handlers so the ones registered since the last wrap are timed in this same raise.
    [EventPriority((EventPriority)1001)]
    private void OnAssetRequested(object? sender, AssetRequestedEventArgs e) => this.Guard(() => this.events?.WrapNew());

    // Runs after every other handler, when the request holds all the operations mods registered for it.
    [EventPriority((EventPriority)(-1001))]
    private void OnAssetRequestedLast(object? sender, AssetRequestedEventArgs e) => this.Guard(() => this.assets.Wrap(e));

    [EventPriority((EventPriority)1001)]
    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e) =>
        this.Guard(() =>
        {
            this.phases.GameLaunched = this.Now();
            this.phases.EntryDone = EntryTiming.LastEntryEnd > 0 ? this.FromTimestamp(EntryTiming.LastEntryEnd) : 0;
            this.events?.WrapNew();
        });

    [EventPriority((EventPriority)1001)]
    private void OnUpdateTicking(object? sender, UpdateTickingEventArgs e) =>
        this.Guard(() =>
        {
            this.events?.WrapNew();
            if (DateTime.UtcNow - this.processStart > GiveUp)
                this.Finish(titleSeen: false);
        });

    private void OnRenderedActiveMenu(object? sender, RenderedActiveMenuEventArgs e) =>
        this.Guard(() =>
        {
            object? menu = Game1Menu();
            if (menu?.GetType().Name != "TitleMenu")
                return;
            if (this.phases.TitleMenu == 0)
            {
                this.phases.TitleMenu = this.Now();
                // A measured launch skips the intro animation, so the title time is the mods' loading, not a fixed
                // cut scene that overlaps it.
                if (this.skipIntro)
                    menu.GetType().GetMethod("skipToTitleButtons", BindingFlags.Instance | BindingFlags.Public)?.Invoke(menu, null);
            }
            // The title's intro animation is the game's, not a mod's: the screen counts as reached once the logo
            // has settled and the buttons can be used.
            FieldInfo? settled = menu.GetType().GetField("titleInPosition", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (settled == null || settled.GetValue(menu) is true)
                this.Finish(titleSeen: true);
        });

    private void Finish(bool titleSeen)
    {
        if (this.done)
            return;
        this.done = true;
        this.phases.TitleScreen = titleSeen ? this.Now() : 0;
        StartupClock.Stop();
        this.Unhook();
        this.Write(StartupClock.Snapshot());
    }

    private void Write(IReadOnlyDictionary<StartupClock.Key, long> ticks)
    {
        try
        {
            List<StartupMod> mods = StartupReport.Group(ticks, this.ManifestOf);
            long end = this.phases.TitleScreen > 0 ? this.phases.TitleScreen : this.Now();
            StartupReport report = new()
            {
                Smapi = Constants.ApiVersion.ToString(),
                Game = GameVersion(),
                ProcessStart = this.processStart,
                Phases = this.phases,
                EntryTimed = this.entryTimed,
                EntryMissed = this.entryMissed,
                Mods = mods,
                OtherMs = Math.Max(0, end - this.phases.BridgeEntry - mods.Sum(m => m.TotalMs)),
            };
            string path = StartupReport.Write(this.profileDir, report);
            this.monitor.Log($"Startup report written to {path}.", LogLevel.Trace);
        }
        catch (Exception ex)
        {
            this.monitor.Log($"Startup report could not be written: {ex.Message}", LogLevel.Trace);
        }
    }

    private void Unhook()
    {
        this.events?.Restore();
        this.harmony.UnpatchAll(this.harmony.Id);
        this.helper.Events.Content.AssetRequested -= this.OnAssetRequested;
        this.helper.Events.Content.AssetRequested -= this.OnAssetRequestedLast;
        this.helper.Events.GameLoop.GameLaunched -= this.OnGameLaunched;
        this.helper.Events.GameLoop.UpdateTicking -= this.OnUpdateTicking;
        this.helper.Events.Display.RenderedActiveMenu -= this.OnRenderedActiveMenu;
    }

    // A timing failure must never reach the game: log once, drop every hook, and skip the report.
    private void Guard(Action action)
    {
        if (this.done)
            return;
        try
        {
            action();
        }
        catch (Exception ex)
        {
            this.Abandon(ex);
        }
    }

    private void Abandon(Exception ex)
    {
        this.done = true;
        StartupClock.Stop();
        this.monitor.Log($"Startup timing is off for this launch: {ex.Message}", LogLevel.Trace);
        try
        {
            this.Unhook();
        }
        catch (Exception cleanup)
        {
            this.monitor.Log($"Startup timing cleanup failed: {cleanup.Message}", LogLevel.Trace);
        }
    }

    private (string Name, string Version)? ManifestOf(string id) =>
        this.helper.ModRegistry.Get(id)?.Manifest is { } m ? (m.Name, m.Version.ToString()) : null;

    private long Now() => (long)(DateTime.UtcNow - this.processStart).TotalMilliseconds;

    private long FromTimestamp(long timestamp) =>
        this.Now() - (long)((Stopwatch.GetTimestamp() - timestamp) * 1000.0 / Stopwatch.Frequency);

    private static string GameVersion() =>
        ModEntry.GameType()?.GetField("version", BindingFlags.Static | BindingFlags.Public)?.GetValue(null) as string ?? "";

    private static object? Game1Menu()
    {
        Type? game1 = ModEntry.GameType();
        const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        return game1?.GetField("activeClickableMenu", flags)?.GetValue(null) ?? game1?.GetProperty("activeClickableMenu", flags)?.GetValue(null);
    }
}
