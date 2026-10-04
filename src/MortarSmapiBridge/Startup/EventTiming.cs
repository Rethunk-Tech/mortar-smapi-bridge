using System.Collections;
using System.Reflection;
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
    private readonly HashSet<object> wrapped = new(ReferenceEqualityComparer.Instance);
    private readonly List<(object Handler, FieldInfo Field, Delegate Original)> originals = [];

    /// <summary>Finds every ManagedEvent on SMAPI's event manager; false when SMAPI's internals have a different shape.</summary>
    internal bool Attach()
    {
        Type? core = typeof(Mod).Assembly.GetType("StardewModdingAPI.Framework.SCore");
        object? instance = core?.GetProperty("Instance", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)?.GetValue(null);
        object? manager = core?.GetField("EventManager", Instance)?.GetValue(instance);
        if (manager == null)
            return false;
        foreach (FieldInfo field in manager.GetType().GetFields(Instance))
        {
            object? managed = field.GetValue(manager);
            if (managed?.GetType().GetField("Handlers", Instance)?.GetValue(managed) is IList handlers)
                this.events.Add((field.Name, handlers));
        }
        this.counts = new int[this.events.Count];
        return this.events.Count > 0;
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
                if (!this.wrapped.Add(handler))
                    continue;
                Type type = handler.GetType();
                FieldInfo? field = type.GetField("<Handler>k__BackingField", Instance);
                if (field?.GetValue(handler) is not Delegate original || type.GetProperty("SourceMod")?.GetValue(handler) is not IModInfo mod)
                    continue;
                Type args = original.GetType().GetGenericArguments()[0];
                Delegate timed = (Delegate)WrapMethod.MakeGenericMethod(args).Invoke(null, [original, mod.Manifest.UniqueID, name])!;
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
