using System.Reflection;
using HarmonyLib;
using StardewModdingAPI;

namespace MortarSmapiBridge.Startup;

/// <summary>Times other mods' Entry from SMAPI's side: SMAPI calls its own <c>SetApi</c> once per mod right after that
/// mod's Entry and GetApi, so the time between two calls is the second mod's. Mods' own methods are never patched:
/// a replaced Entry breaks mods that find their assembly from the call stack, such as Harmony's parameterless
/// <c>PatchAll()</c>. Mods whose Entry ran before the bridge's (the bridge was not loaded early) cannot be timed.</summary>
internal static class EntryTiming
{
    private static StartupClock.Frame? open;

    internal static long LastEntryEnd { get; private set; }

    /// <summary>Returns false when SMAPI's shape is unexpected.</summary>
    internal static bool Patch(Harmony harmony, IModInfo own)
    {
        MethodInfo? setApi = own.GetType().GetMethod("SetApi", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (setApi == null)
            return false;
        harmony.Patch(setApi, postfix: new HarmonyMethod(typeof(EntryTiming).GetMethod(nameof(Postfix), BindingFlags.Static | BindingFlags.NonPublic)));
        return true;
    }

    /// <summary>Drops the frame opened after the last mod, which no Entry ran in.</summary>
    internal static void Close()
    {
        StartupClock.Discard(open);
        open = null;
    }

    private static void Postfix(object __instance)
    {
        if (open != null && __instance is IModInfo info)
        {
            open.Mod = info.Manifest.UniqueID;
            StartupClock.End(open);
        }
        LastEntryEnd = System.Diagnostics.Stopwatch.GetTimestamp();
        open = StartupClock.Begin("", "entry");
    }
}
