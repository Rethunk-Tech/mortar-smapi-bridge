using System.Reflection;
using HarmonyLib;
using StardewModdingAPI;

namespace MortarSmapiBridge.Startup;

/// <summary>Splits Content Patcher's first-tick load by content pack. CP loads every pack inside one UpdateTicked
/// handler, which on a large profile is the biggest single startup cost, so the event time alone cannot say which
/// packs are slow. Methods are found by name; one missing in another CP version is skipped.</summary>
internal static class ContentPatcherTiming
{
    internal const string Id = "Pathoschild.ContentPatcher";

    // Each takes or is a pack: RawContentPack reads content.json, ConfigFileHandler reads config.json, PatchLoader
    // parses the pack's patches (recursively for Include), each patch's UpdateContext evaluates its tokens, and the
    // config is saved and registered with Generic Mod Config Menu.
    private static readonly (string Type, string Method)[] Targets =
    [
        ("ContentPatcher.Framework.RawContentPack", "TryReloadContent"),
        ("ContentPatcher.Framework.ConfigFileHandler", "Read"),
        ("ContentPatcher.Framework.ConfigFileHandler", "Save"),
        ("ContentPatcher.Framework.GenericModConfigMenuIntegrationForContentPack", "Register"),
        ("ContentPatcher.Framework.PatchLoader", "LoadPatches"),
        ("ContentPatcher.Framework.Patches.Patch", "UpdateContext"),
    ];

    internal static int Patch(Harmony harmony, IModHelper helper)
    {
        if (helper.ModRegistry.Get(Id)?.GetType().GetProperty("Mod")?.GetValue(helper.ModRegistry.Get(Id)) is not IMod cp)
            return 0;
        Assembly assembly = cp.GetType().Assembly;
        HarmonyMethod prefix = new(typeof(ContentPatcherTiming).GetMethod(nameof(Prefix), BindingFlags.Static | BindingFlags.NonPublic));
        HarmonyMethod finalizer = new(typeof(ContentPatcherTiming).GetMethod(nameof(Finalizer), BindingFlags.Static | BindingFlags.NonPublic));
        int patched = 0;
        Type[] types = LoadableTypes(assembly);
        foreach ((string typeName, string method) in Targets)
        {
            if (assembly.GetType(typeName) is not { } baseType)
                continue;
            // Overrides in patch subclasses are patched too; a nested base call is the same pack, so exclusive time is unchanged.
            foreach (Type type in types.Where(t => baseType.IsAssignableFrom(t)))
            {
                const BindingFlags declared = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
                foreach (MethodInfo target in type.GetMethods(declared).Where(m => m.Name == method && !m.IsAbstract && !m.IsGenericMethodDefinition))
                {
                    harmony.Patch(target, prefix: prefix, finalizer: finalizer);
                    patched++;
                }
            }
        }
        return patched;
    }

    private static void Prefix(object __instance, object[] __args, out StartupClock.Frame? __state)
    {
        // The patches stay after the title screen (unpatching blocks the game for seconds), so this must be cheap.
        if (!StartupClock.On)
        {
            __state = null;
            return;
        }
        string? pack = PackId(__instance) ?? __args.Select(PackId).FirstOrDefault(id => id != null);
        __state = pack == null ? null : StartupClock.Begin(Id, "load", pack);
    }

    private static Exception? Finalizer(StartupClock.Frame? __state, Exception? __exception)
    {
        StartupClock.End(__state);
        return __exception;
    }

    private static string? PackId(object? value)
    {
        const BindingFlags any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        Type? type = value?.GetType();
        object? pack = type?.GetProperty("ContentPack", any)?.GetValue(value) ?? type?.GetField("ContentPack", any)?.GetValue(value) ?? value;
        return pack?.GetType().GetProperty("Manifest", BindingFlags.Instance | BindingFlags.Public)?.GetValue(pack) is IManifest manifest ? manifest.UniqueID : null;
    }

    private static Type[] LoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return [.. ex.Types.OfType<Type>()];
        }
    }
}
