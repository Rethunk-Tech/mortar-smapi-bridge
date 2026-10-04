using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using HarmonyLib;
using StardewModdingAPI;

namespace MortarSmapiBridge.Startup;

/// <summary>Swaps each SMAPI event handler's delegate for a timed one. SMAPI's ManagedEvent keeps its handlers in a
/// private list of ManagedEventHandler objects and raises through each object's Handler, so replacing the delegate in
/// place covers cached handler arrays too.</summary>
internal sealed class EventTiming
{
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly MethodInfo WrapMethod = typeof(EventTiming).GetMethod(nameof(Wrap), BindingFlags.Static | BindingFlags.NonPublic)!;

    private readonly List<(string Name, IList Handlers)> events = [];
    private int[] counts = [];
    private readonly HashSet<object> seen = new(ReferenceEqualityComparer.Instance);
    private readonly List<(object Handler, FieldInfo Field, Delegate Original)> originals = [];
    private readonly List<Type> eventTypes = [];

    // A mod that removes its own handler passes the original delegate, which differs from the wrapper in SMAPI's
    // list; the Remove prefix puts the original back in that handler first, so run-once handlers still unsubscribe.
    private static readonly ConcurrentDictionary<Delegate, (object Handler, FieldInfo Field)> wrapped = new();

    internal static int Unwrapped;

    /// <summary>Finds every ManagedEvent on SMAPI's event manager; false when SMAPI's internals have a different shape.</summary>
    internal bool Attach()
    {
        (Type? core, object? instance) = ModEntry.SCore();
        object? manager = core?.GetField("EventManager", Instance)?.GetValue(instance);
        if (manager == null)
            return false;
        foreach (FieldInfo field in manager.GetType().GetFields(Instance))
        {
            object? managed = field.GetValue(manager);
            if (managed?.GetType().GetField("Handlers", Instance)?.GetValue(managed) is IList handlers)
            {
                this.events.Add((field.Name, handlers));
                if (!this.eventTypes.Contains(managed.GetType()))
                    this.eventTypes.Add(managed.GetType());
            }
        }
        this.counts = new int[this.events.Count];
        return this.events.Count > 0;
    }

    /// <summary>Patches each event type's Remove so a mod can still remove a handler the bridge wrapped; false when
    /// SMAPI's Remove has a different shape, in which case nothing may be wrapped.</summary>
    internal bool PatchRemove(Harmony harmony)
    {
        HarmonyMethod prefix = new(typeof(EventTiming).GetMethod(nameof(RemovePrefix), BindingFlags.Static | BindingFlags.NonPublic));
        foreach (Type type in this.eventTypes)
        {
            if (type.GetMethod("Remove", Instance) is not { } remove || remove.GetParameters().Length != 1)
                return false;
            harmony.Patch(remove, prefix: prefix);
        }
        return true;
    }

    private static void RemovePrefix(object[] __args)
    {
        if (__args[0] is Delegate original && wrapped.TryRemove(original, out (object Handler, FieldInfo Field) entry))
        {
            entry.Field.SetValue(entry.Handler, original);
            Interlocked.Increment(ref Unwrapped);
        }
    }

    /// <summary>Wraps handlers registered since the last call. Cheap when nothing was added, since it runs on every asset request.</summary>
    internal void WrapNew()
    {
        for (int i = 0; i < this.events.Count; i++)
        {
            (string name, IList handlers) = this.events[i];
            if (handlers.Count == this.counts[i])
                continue;
            this.counts[i] = handlers.Count;
            object[] snapshot;
            lock (handlers)
            {
                snapshot = new object[handlers.Count];
                handlers.CopyTo(snapshot, 0);
            }
            foreach (object handler in snapshot)
            {
                if (!this.seen.Add(handler))
                    continue;
                Type type = handler.GetType();
                FieldInfo? field = type.GetField("<Handler>k__BackingField", Instance);
                if (field?.GetValue(handler) is not Delegate original || type.GetProperty("SourceMod")?.GetValue(handler) is not IModInfo mod)
                    continue;
                Type args = original.GetType().GetGenericArguments()[0];
                Delegate timed = (Delegate)WrapMethod.MakeGenericMethod(args).Invoke(null, [original, mod.Manifest.UniqueID, name])!;
                wrapped[original] = (handler, field);
                field.SetValue(handler, timed);
                this.originals.Add((handler, field, original));
            }
        }
    }

    internal void Restore()
    {
        foreach ((object handler, FieldInfo field, Delegate original) in this.originals)
            field.SetValue(handler, original);
        this.originals.Clear();
        wrapped.Clear();
    }

    private static EventHandler<T> Wrap<T>(EventHandler<T> inner, string mod, string eventName) =>
        (sender, args) =>
        {
            StartupClock.Frame? frame = StartupClock.Begin(mod, eventName);
            try
            {
                inner(sender, args);
            }
            finally
            {
                StartupClock.End(frame);
            }
        };
}
