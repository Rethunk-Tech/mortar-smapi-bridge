using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using StardewModdingAPI;
using StardewModdingAPI.Events;

namespace MortarSmapiBridge;

public sealed class ModEntry : Mod
{
    private const string StateFileName = "mortar-smapi-bridge.json";

    private BridgeServer? Server;
    private OverlayServer? Overlay;
    private IModHelper? OverlayHelper;
    private readonly HashSet<string> OverlayReadErrors = [];
    private string? StatePath;
    private Action<string>? Enqueue;

    public override void Entry(IModHelper helper)
    {
        ISemanticVersion api = Constants.ApiVersion;
        if (!ApiRange.IsTested(api.MajorVersion, api.MinorVersion))
        {
            this.Monitor.Log($"Mortar SMAPI Bridge {this.ModManifest.Version} is tested on SMAPI {ApiRange.Tested}, but SMAPI {api} is running, so console commands from Mortar are off until the bridge is updated.", LogLevel.Warn);
            return;
        }

        this.Enqueue = ResolveRawCommandQueue();
        if (this.Enqueue == null)
        {
            this.Monitor.Log("This SMAPI version has no reachable raw command queue; the bridge is disabled.", LogLevel.Error);
            return;
        }

        string token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        this.Server = new BridgeServer(token, this.Submit, line => this.Monitor.Log($"Command received: {line}", LogLevel.Trace));
        this.Server.Start();

        this.StatePath = Path.Combine(helper.DirectoryPath, StateFileName);
        WriteStateFile(this.StatePath, JsonSerializer.Serialize(new { port = this.Server.Port, token, pid = Environment.ProcessId }));
        AppDomain.CurrentDomain.ProcessExit += (_, _) => this.Shutdown();
        this.StartOverlay(helper);
        this.Monitor.Log($"Listening on 127.0.0.1:{this.Server.Port}.", LogLevel.Info);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            this.Shutdown();
        base.Dispose(disposing);
    }

    private string? Submit(string line)
    {
        this.Enqueue!(line);
        return null;
    }

    private void StartOverlay(IModHelper helper)
    {
        ModConfig config = helper.ReadConfig<ModConfig>();
        if (!config.OverlayEnabled)
            return;

        // SMAPI keeps Constants.GameVersion internal and refuses reflection into its own types, so the version comes
        // from the game's Game1.version, which SMAPI lets a mod reflect on.
        Type? game1 = GameType();
        string? raw = game1 == null ? null : helper.Reflection.GetField<string>(game1, "version", false)?.GetValue();
        if (raw == null || !SemanticVersion.TryParse(raw, out ISemanticVersion? gameVersion)
            || !ApiRange.IsTestedGame(gameVersion.MajorVersion, gameVersion.MinorVersion, gameVersion.PatchVersion))
        {
            this.Monitor.Log($"Stream overlay is disabled: Stardew Valley {raw ?? "unknown"} is outside the verified range {ApiRange.TestedGame}.", LogLevel.Warn);
            return;
        }

        if (!OverlayServer.IsValidPort(config.OverlayPort))
        {
            this.Monitor.Log($"Stream overlay is disabled: port {config.OverlayPort} is invalid.", LogLevel.Error);
            return;
        }

        string overlayToken = EnsureOverlayToken(helper, config);
        var overlay = new OverlayServer(config.OverlayPort, overlayToken);
        try
        {
            overlay.Start();
        }
        catch (SocketException ex)
        {
            overlay.Dispose();
            this.Monitor.Log($"Stream overlay is disabled: could not bind 127.0.0.1:{config.OverlayPort}: {ex.Message}", LogLevel.Error);
            return;
        }

        this.Overlay = overlay;
        this.OverlayHelper = helper;
        helper.Events.GameLoop.OneSecondUpdateTicked += this.UpdateOverlayState;
        this.Monitor.Log($"Stream overlay listening on 127.0.0.1:{overlay.Port}/state.", LogLevel.Info);
    }

    private void UpdateOverlayState(object? sender, OneSecondUpdateTickedEventArgs e)
    {
        if (this.Overlay == null || this.OverlayHelper == null)
            return;

        if (!Context.IsWorldReady)
        {
            this.Overlay.SetNotInGame();
            return;
        }

        try
        {
            this.Overlay.SetSnapshot(ReadOverlaySnapshot(this.OverlayHelper));
        }
        catch (Exception ex)
        {
            if (OverlayServer.ShouldLogReadError(this.OverlayReadErrors, ex.Message))
                this.Monitor.Log($"Stream overlay state is unavailable: {ex.Message}", LogLevel.Error);
        }
    }

    // SMAPI's reflection helper cannot bind a static property getter, so Game1's static properties are read directly.
    private static T? StaticProperty<T>(Type type, string name) =>
        (T?)(type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new MissingMemberException(type.FullName, name)).GetValue(null);

    private static Type? GameType() =>
        AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(assembly => string.Equals(assembly.GetName().Name, "Stardew Valley", StringComparison.OrdinalIgnoreCase))
            ?.GetType("StardewValley.Game1");

    private static OverlaySnapshot? ReadOverlaySnapshot(IModHelper helper)
    {
        Type? game1 = GameType();
        if (game1 == null)
            return null;

        object? player = StaticProperty<object>(game1, "player");
        object? location = StaticProperty<object>(game1, "currentLocation");
        if (player == null || location == null)
            return null;

        object? weather = helper.Reflection.GetMethod(location, "GetWeather", false)?.Invoke<object>([]);
        string weatherName = weather == null
            ? "unknown"
            : helper.Reflection.GetProperty<string>(weather, "Weather", false)?.GetValue() ?? "unknown";
        var skillLevels = new Dictionary<string, int>();
        var getSkillLevel = helper.Reflection.GetMethod(player, "GetSkillLevel", true);
        foreach ((string name, int index) in new[] { ("farming", 0), ("fishing", 1), ("foraging", 2), ("mining", 3), ("combat", 4), ("luck", 5) })
            skillLevels[name] = getSkillLevel.Invoke<int>(index);

        return new OverlaySnapshot(
            true,
            helper.Reflection.GetProperty<string>(location, "Name", true).GetValue(),
            helper.Reflection.GetProperty<string>(player, "Name", true).GetValue(),
            StaticProperty<string>(game1, "currentSeason") ?? "unknown",
            helper.Reflection.GetField<int>(game1, "dayOfMonth", true).GetValue(),
            helper.Reflection.GetField<int>(game1, "year", true).GetValue(),
            helper.Reflection.GetField<int>(game1, "timeOfDay", true).GetValue(),
            helper.Reflection.GetProperty<int>(player, "Money", true).GetValue(),
            weatherName,
            helper.Reflection.GetField<int>(player, "health", true).GetValue(),
            helper.Reflection.GetField<int>(player, "maxHealth", true).GetValue(),
            helper.Reflection.GetProperty<float>(player, "Stamina", true).GetValue(),
            helper.Reflection.GetProperty<int>(player, "MaxStamina", true).GetValue(),
            skillLevels);
    }

    private static string EnsureOverlayToken(IModHelper helper, ModConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.OverlayToken))
        {
            config.OverlayToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
            WriteConfigFile(helper, config);
        }
        else
        {
            RestrictFile(Path.Combine(helper.DirectoryPath, "config.json"));
        }

        return config.OverlayToken;
    }

    private static void WriteConfigFile(IModHelper helper, ModConfig config)
    {
        string path = Path.Combine(helper.DirectoryPath, "config.json");
        if (!File.Exists(path))
            File.WriteAllText(path, "");
        RestrictFile(path);
        helper.WriteConfig(config);
        RestrictFile(path);
    }

    private void Shutdown()
    {
        if (this.OverlayHelper != null)
            this.OverlayHelper.Events.GameLoop.OneSecondUpdateTicked -= this.UpdateOverlayState;
        this.OverlayHelper = null;
        this.Overlay?.Dispose();
        this.Overlay = null;
        this.Server?.Dispose();
        this.Server = null;
        if (this.StatePath != null)
            File.Delete(this.StatePath);
    }

    /// <summary>
    /// SMAPI 4.5.2 has no public API to run another mod's or a built-in command. Its console input feeds an internal
    /// thread-safe queue that the game update tick drains, so we add to that same queue by reflection.
    /// </summary>
    private static Action<string>? ResolveRawCommandQueue()
    {
        Type? core = typeof(Mod).Assembly.GetType("StardewModdingAPI.Framework.SCore");
        object? instance = core?.GetProperty("Instance", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)?.GetValue(null);
        object? queue = core?.GetField("RawCommandQueue", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(instance);
        MethodInfo? add = queue?.GetType().GetMethod("Add", [typeof(string)]);
        return add == null ? null : line => add.Invoke(queue, [line]);
    }

    private static void WriteStateFile(string path, string json)
    {
        // Create empty and restrict before writing so the secret is never readable by others.
        File.WriteAllText(path, "");
        RestrictFile(path);
        File.WriteAllText(path, json);
    }

    private static void RestrictFile(string path)
    {
        if (!OperatingSystem.IsWindows() && chmod(path, 0x180) != 0)
            throw new IOException($"Could not restrict permissions on {path}.");
    }

    [DllImport("libc", SetLastError = true)]
    private static extern int chmod(string path, uint mode);
}
