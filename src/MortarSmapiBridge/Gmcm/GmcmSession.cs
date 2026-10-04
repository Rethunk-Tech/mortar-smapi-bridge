using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using StardewModdingAPI;
using StardewModdingAPI.Events;

namespace MortarSmapiBridge.Gmcm;

internal sealed class GmcmSession
{
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    private readonly IModHelper _helper;
    private readonly IMonitor _monitor;
    private readonly ModConfig _config;
    private bool _disabled;
    private bool _gameLaunched;
    private bool _firstTickDone;

    internal GmcmSession(IModHelper helper, IMonitor monitor, ModConfig config)
    {
        _helper = helper;
        _monitor = monitor;
        _config = config;
    }

    internal void Attach()
    {
        if (!_config.GmcmEnabled)
            return;

        _helper.Events.GameLoop.GameLaunched += OnGameLaunched;
        _helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
        _helper.Events.Display.MenuChanged += OnMenuChanged;
        _helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        _gameLaunched = true;
    }

    private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
    {
        if (!_gameLaunched || _firstTickDone || _disabled)
            return;
        _firstTickDone = true;
        ApplyPending();
        CaptureAll();
    }

    private void OnMenuChanged(object? sender, MenuChangedEventArgs e)
    {
        if (_disabled || !_firstTickDone)
            return;
        if (IsGmcmMenu(GmcmReflection.GetMemberValue(e, "OldMenu")) && !IsGmcmMenu(GmcmReflection.GetMemberValue(e, "NewMenu")))
            CaptureAll();
    }

    private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
    {
        if (_disabled || !_firstTickDone)
            return;
        CaptureAll();
    }

    internal static bool IsGmcmMenu(object? menu)
    {
        if (menu == null)
            return false;
        string? fullName = menu.GetType().FullName;
        return fullName != null && fullName.IndexOf("GenericModConfigMenu", StringComparison.Ordinal) >= 0;
    }

    internal void ApplyPending()
    {
        if (_disabled)
            return;

        string? pendingDir = PendingDir();
        if (pendingDir == null || !Directory.Exists(pendingDir))
            return;

        if (!TryGetConfigManager(out object? manager, out string? gmcmVersion) || manager == null)
            return;

        Dictionary<string, object> configs = IndexConfigs(manager);
        foreach (string path in Directory.GetFiles(pendingDir, "*.json"))
        {
            string name = Path.GetFileName(path);
            if (name.StartsWith("_", StringComparison.Ordinal) || name.EndsWith(".result.json", StringComparison.OrdinalIgnoreCase))
                continue;
            if (!name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                continue;

            string uniqueId = Path.GetFileNameWithoutExtension(name);
            try
            {
                ApplyOne(path, uniqueId, configs);
            }
            catch (Exception ex)
            {
                Disable("applying pending GMCM edits", ex);
                return;
            }
        }
    }

    internal void CaptureAll()
    {
        if (_disabled)
            return;

        if (!TryGetConfigManager(out object? manager, out string? gmcmVersion) || manager == null)
            return;

        string? gmcmDir = CaptureDir();
        if (gmcmDir == null)
            return;
        Directory.CreateDirectory(gmcmDir);
        DateTimeOffset capturedAt = DateTimeOffset.UtcNow;
        GmcmIndexFile index = new()
        {
            Schema = 1,
            GmcmVersion = gmcmVersion ?? "",
            CapturedAt = capturedAt.ToString("o")
        };

        try
        {
            object? all = GmcmReflection.InvokeMember(manager, "GetAll");
            if (all == null)
            {
                Disable("ModConfigManager.GetAll()", null);
                return;
            }

            foreach (object modConfig in GmcmReflection.Enumerate(all))
            {
                if (!TryCaptureOne(modConfig, gmcmVersion ?? "", capturedAt, gmcmDir, out GmcmIndexEntry? entry, out string? error))
                {
                    Disable(error ?? "capturing a mod config", null);
                    return;
                }

                if (entry != null)
                    index.Mods.Add(entry);
            }

            AtomicWrite(Path.Combine(gmcmDir, "_index.json"), JsonSerializer.Serialize(index, JsonOptions));
        }
        catch (Exception ex)
        {
            Disable("walking GMCM ConfigManager.GetAll()", ex);
        }
    }

    private bool TryCaptureOne(object modConfig, string gmcmVersion, DateTimeOffset capturedAt, string gmcmDir, out GmcmIndexEntry? entry, out string? error)
    {
        entry = null;
        error = null;
        try
        {
            object? manifest = GmcmReflection.GetMemberValue(modConfig, "ModManifest")
                ?? throw new InvalidOperationException("ModConfig.ModManifest");
            string id = GmcmReflection.GetMemberValue(manifest, "UniqueID")?.ToString()
                ?? throw new InvalidOperationException("IManifest.UniqueID");
            string name = GmcmReflection.GetMemberValue(manifest, "Name")?.ToString() ?? id;
            string version = GmcmReflection.GetMemberValue(manifest, "Version")?.ToString() ?? "";

            GmcmCaptureFile file = GmcmWalker.WalkModConfig(
                modConfig,
                new GmcmModIdentity { Id = id, Name = name, Version = version },
                gmcmVersion,
                capturedAt);
            string dest = Path.Combine(gmcmDir, SafeFileName(id) + ".json");
            AtomicWrite(dest, JsonSerializer.Serialize(file, JsonOptions));
            entry = new GmcmIndexEntry { Id = id, Name = name, Version = version };
            return true;
        }
        catch (Exception ex)
        {
            error = "capturing ModConfig (" + ex.Message + ")";
            return false;
        }
    }

    private void ApplyOne(string pendingPath, string uniqueId, Dictionary<string, object> configs)
    {
        string json = File.ReadAllText(pendingPath);
        GmcmPendingFile? pending = JsonSerializer.Deserialize<GmcmPendingFile>(json, JsonOptions);
        if (pending == null)
        {
            WriteResult(pendingPath, new GmcmPendingResultFile
            {
                Skipped = { new GmcmSkippedEdit { Edit = new GmcmPendingEdit { Name = uniqueId }, Reason = "invalid pending JSON" } }
            });
            return;
        }

        if (pending.Schema != GmcmSchema.Current)
        {
            GmcmPendingResultFile unsupported = new();
            foreach (GmcmPendingEdit edit in pending.Edits)
                unsupported.Skipped.Add(new GmcmSkippedEdit { Edit = edit, Reason = $"pending file schema {pending.Schema} is not supported; update the bridge" });
            WriteResult(pendingPath, unsupported);
            return;
        }

        if (!configs.TryGetValue(uniqueId, out object? modConfig))
        {
            GmcmPendingResultFile missing = new();
            foreach (GmcmPendingEdit edit in pending.Edits)
                missing.Skipped.Add(new GmcmSkippedEdit { Edit = edit, Reason = "mod not registered with GMCM" });
            WriteResult(pendingPath, missing);
            return;
        }

        GmcmPendingResultFile result = GmcmApply.ApplyEdits(modConfig, pending.Edits);
        if (result.Applied.Count > 0)
        {
            try
            {
                GmcmApply.SaveLikeGmcm(modConfig);
            }
            catch (Exception ex)
            {
                result.Skipped.Add(new GmcmSkippedEdit
                {
                    Edit = new GmcmPendingEdit { Name = uniqueId },
                    Reason = "save failed: " + ex.Message
                });
                WriteResult(pendingPath, result);
                return;
            }

            WriteResult(pendingPath, result);
            File.Delete(pendingPath);
            return;
        }

        WriteResult(pendingPath, result);
    }

    private Dictionary<string, object> IndexConfigs(object manager)
    {
        Dictionary<string, object> configs = new(StringComparer.OrdinalIgnoreCase);
        object? all = GmcmReflection.InvokeMember(manager, "GetAll");
        foreach (object modConfig in GmcmReflection.Enumerate(all))
        {
            object? manifest = GmcmReflection.GetMemberValue(modConfig, "ModManifest");
            string? id = manifest == null ? null : GmcmReflection.GetMemberValue(manifest, "UniqueID")?.ToString();
            if (!string.IsNullOrEmpty(id))
                configs[id] = modConfig;
        }

        return configs;
    }

    private bool TryGetConfigManager(out object? manager, out string? gmcmVersion)
    {
        manager = null;
        gmcmVersion = null;
        try
        {
            IModInfo? info = _helper.ModRegistry.Get(GmcmReflection.GmcmModId);
            if (info == null)
                return false;

            gmcmVersion = info.Manifest.Version.ToString();
            object? mod = GmcmReflection.GetMemberValue(info, "Mod");
            if (mod == null)
            {
                Disable("IModInfo.Mod on spacechase0.GenericModConfigMenu", null);
                return false;
            }

            manager = GmcmReflection.GetMemberValue(mod, "ConfigManager");
            if (manager == null)
            {
                Disable("GMCM Mod.ConfigManager", null);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            Disable("resolving GMCM ConfigManager", ex);
            return false;
        }
    }

    private void Disable(string what, Exception? ex)
    {
        _disabled = true;
        string suffix = ex == null ? "" : ": " + ex.Message;
        _monitor.Log(
            "GMCM capture/apply disabled this session; " + what + " changed or failed" + suffix,
            LogLevel.Warn);
    }

    // Mortar installs the bridge at <profile>/mods/<entry key>/MortarSmapiBridge, so the profile is the nearest
    // ancestor holding profile.json; without one the game was not started by Mortar and nothing is written.
    private string? ProfileDir() => MortarSmapiBridge.Startup.ProfileDirectory.Find(_helper.DirectoryPath);

    private string? CaptureDir() => ProfileDir() is { } dir ? Path.Combine(dir, "gmcm") : null;

    private string? PendingDir() => ProfileDir() is { } dir ? Path.Combine(dir, "gmcm-pending") : null;

    internal static string SafeFileName(string uniqueId)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        char[] buffer = uniqueId.ToCharArray();
        for (int i = 0; i < buffer.Length; i++)
        {
            if (Array.IndexOf(invalid, buffer[i]) >= 0)
                buffer[i] = '_';
        }

        return new string(buffer);
    }

    internal static void AtomicWrite(string path, string contents)
    {
        string temp = path + ".tmp";
        File.WriteAllText(temp, contents);
        File.Move(temp, path, overwrite: true);
    }

    private static void WriteResult(string pendingPath, GmcmPendingResultFile result)
    {
        string dest = Path.ChangeExtension(pendingPath, ".result.json");
        if (dest.EndsWith(".json.result.json", StringComparison.Ordinal))
            dest = pendingPath.Substring(0, pendingPath.Length - ".json".Length) + ".result.json";
        AtomicWrite(dest, JsonSerializer.Serialize(result, JsonOptions));
    }
}
