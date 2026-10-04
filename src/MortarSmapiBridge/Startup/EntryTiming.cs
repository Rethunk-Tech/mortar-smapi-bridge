using System.Reflection;
using HarmonyLib;
using StardewModdingAPI;

namespace MortarSmapiBridge.Startup;

/// <summary>Times other mods' Entry. Patching ~200 methods costs time itself, so this runs only on a measured launch.
/// Mods whose Entry ran before the bridge's (the bridge was not loaded early) cannot be timed.</summary>
internal static class EntryTiming
{
    private static readonly Dictionary<Type, string> ids = [];

    internal static long LastEntryEnd { get; private set; }

    /// <summary>Returns how many mods' Entry had already run (nothing to patch for those).</summary>
    internal static int Patch(Harmony harmony, IEnumerable<IModInfo> mods, string ownId, ISet<string> alreadyRan)
    {
        HarmonyMethod prefix = new(typeof(EntryTiming).GetMethod(nameof(Prefix), BindingFlags.Static | BindingFlags.NonPublic));
        HarmonyMethod finalizer = new(typeof(EntryTiming).GetMethod(nameof(Finalizer), BindingFlags.Static | BindingFlags.NonPublic));
        int missed = 0;
        foreach (IModInfo info in mods)
        {
            string id = info.Manifest.UniqueID;
            if (info.IsContentPack || string.Equals(id, ownId, StringComparison.OrdinalIgnoreCase))
                continue;
            if (alreadyRan.Contains(id))
            {
                missed++;
                continue;
            }
            if (info.GetType().GetProperty("Mod")?.GetValue(info) is not IMod mod)
                continue;
            MethodInfo? entry = mod.GetType().GetMethod(nameof(Mod.Entry), BindingFlags.Instance | BindingFlags.Public, [typeof(IModHelper)]);
            if (entry == null || entry.DeclaringType == typeof(Mod) || entry.IsAbstract)
                continue;
            ids[mod.GetType()] = id;
            harmony.Patch(entry, prefix: prefix, finalizer: finalizer);
        }
        return missed;
    }

    private static void Prefix(object __instance, out StartupClock.Frame? __state) =>
        __state = ids.TryGetValue(__instance.GetType(), out string? id) ? StartupClock.Begin(id, "entry") : null;

    private static Exception? Finalizer(StartupClock.Frame? __state, Exception? __exception)
    {
        StartupClock.End(__state);
        LastEntryEnd = System.Diagnostics.Stopwatch.GetTimestamp();
        return __exception;
    }
}
