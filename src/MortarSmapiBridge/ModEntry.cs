using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using StardewModdingAPI;
using MortarSmapiBridge.Gmcm;
using MortarSmapiBridge.Perf;
using MortarSmapiBridge.Startup;

namespace MortarSmapiBridge;

public sealed class ModEntry : Mod
{
    private const string StateFileName = "mortar-smapi-bridge.json";

    private BridgeServer? Server;
    private FrameProfiler? Profiler;
    private string? StatePath;
    private Action<string>? Enqueue;
    private int shutdown;

    public override void Entry(IModHelper helper)
    {
        // First, so that with the bridge loaded early every other mod's Entry and handlers are measured.
        ModConfig config = helper.ReadConfig<ModConfig>();
        StartupSession.Start(helper, this.Monitor, this.ModManifest, config);
        TitleScreen.Start(helper, this.Monitor);
        IntroSkip.Start(helper, this.Monitor);
        new GmcmSession(helper, this.Monitor, config).Attach();
        this.Profiler = new FrameProfiler(helper, this.Monitor, this.ModManifest.UniqueID);
        AppDomain.CurrentDomain.ProcessExit += (_, _) => this.Shutdown();

        ISemanticVersion api = Constants.ApiVersion;
        if (!ApiRange.IsTested(api.MajorVersion, api.MinorVersion, api.PatchVersion))
        {
            this.Monitor.Log($"Mortar SMAPI Bridge {this.ModManifest.Version} is tested on SMAPI {ApiRange.Tested}, but SMAPI {api} is running, so console commands from Mortar are off until the bridge is updated.", LogLevel.Warn);
            return;
        }

        this.Enqueue = ResolveRawCommandQueue();
        if (this.Enqueue == null)
        {
            this.Monitor.Log("This SMAPI version has no reachable raw command queue; console commands from Mortar are off.", LogLevel.Error);
            return;
        }

        string token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        this.Server = new BridgeServer(token, this.Enqueue!, name => this.Monitor.Log($"Command received: {name}", LogLevel.Trace), this.Answer);
        this.Server.Start();

        this.StatePath = Path.Join(helper.DirectoryPath, StateFileName);
        try
        {
            Files.AtomicWrite(this.StatePath, JsonSerializer.Serialize(new { port = this.Server.Port, token }), Files.Restrict);
        }
        catch
        {
            // Without the state file Mortar cannot find the port, so a listener left running would only be dead weight.
            this.Server.Dispose();
            this.Server = null;
            throw;
        }

        this.Monitor.Log($"Listening on 127.0.0.1:{this.Server.Port}.", LogLevel.Info);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            this.Shutdown();
        base.Dispose(disposing);
    }

    // The questions Mortar asks the running game; anything else is a console command.
    private string? Answer(string line) => line == StatusReply.Line
        ? StatusReply.Build(ReadGameVersion(this.Helper), this.Helper.ModRegistry.GetAll().Select(mod => (mod.Manifest.UniqueID, mod.Manifest.Version.ToString())))
        : this.Profiler?.TryAnswer(line);

    // SMAPI keeps Constants.GameVersion internal and refuses reflection into its own types, so the version comes
    // from the game's Game1.version, which SMAPI lets a mod reflect on.
    private static string? ReadGameVersion(IModHelper helper)
    {
        Type? game1 = GameType();
        return game1 == null ? null : helper.Reflection.GetField<string>(game1, "version", false)?.GetValue();
    }

    internal static (Type? Type, object? Instance) SCore()
    {
        Type? core = typeof(Mod).Assembly.GetType("StardewModdingAPI.Framework.SCore");
        return (core, core?.GetProperty("Instance", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)?.GetValue(null));
    }

    internal static Type? GameType() =>
        AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(assembly => string.Equals(assembly.GetName().Name, "Stardew Valley", StringComparison.OrdinalIgnoreCase))
            ?.GetType("StardewValley.Game1");

    private void Shutdown()
    {
        if (Interlocked.Exchange(ref this.shutdown, 1) != 0)
            return;

        try
        {
            this.Server?.Dispose();
            this.Server = null;
        }
        finally
        {
            if (this.StatePath != null)
                File.Delete(this.StatePath);
        }
    }

    /// <summary>
    /// SMAPI 4.5.2 has no public API to run another mod's or a built-in command. Its console input feeds an internal
    /// thread-safe queue that the game update tick drains, so we add to that same queue by reflection.
    /// </summary>
    private static Action<string>? ResolveRawCommandQueue()
    {
        (Type? core, object? instance) = ModEntry.SCore();
        object? queue = core?.GetField("RawCommandQueue", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(instance);
        MethodInfo? add = queue?.GetType().GetMethod("Add", [typeof(string)]);
        return add == null ? null : line => add.Invoke(queue, [line]);
    }
}
