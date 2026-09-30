using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using StardewModdingAPI;

namespace MortarSmapiBridge;

public sealed class ModEntry : Mod
{
    private const string StateFileName = "mortar-smapi-bridge.json";

    private BridgeServer? Server;
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

    private void Shutdown()
    {
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
        if (!OperatingSystem.IsWindows() && chmod(path, 0x180) != 0)
            throw new IOException($"Could not restrict permissions on {path}.");
        File.WriteAllText(path, json);
    }

    [DllImport("libc", SetLastError = true)]
    private static extern int chmod(string path, uint mode);
}
