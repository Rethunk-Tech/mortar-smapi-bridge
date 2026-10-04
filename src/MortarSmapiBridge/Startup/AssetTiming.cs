using System.Collections;
using System.Reflection;
using StardewModdingAPI;
using StardewModdingAPI.Events;

namespace MortarSmapiBridge.Startup;

/// <summary>Times each asset edit and load SMAPI applies, by the mod that registered it and the content pack it acts for
/// (Content Patcher registers its packs' edits with the pack as OnBehalfOf). SMAPI collects the operations on the
/// event args and applies them after the raise, so a handler that runs last can wrap every operation's delegate.</summary>
internal sealed class AssetTiming
{
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private readonly HashSet<object> wrapped = new(ReferenceEqualityComparer.Instance);

    internal void Wrap(AssetRequestedEventArgs e)
    {
        this.WrapAll(e, "EditOperations", "ApplyEdit");
        this.WrapAll(e, "LoadOperations", "GetData");
    }

    private void WrapAll(AssetRequestedEventArgs e, string list, string member)
    {
        if (typeof(AssetRequestedEventArgs).GetProperty(list, Instance)?.GetValue(e) is not IList operations)
            return;
        foreach (object op in operations)
        {
            if (!this.wrapped.Add(op) || op.GetType().GetField($"<{member}>k__BackingField", Instance) is not { } field)
                continue;
            (string mod, string? pack) = Owner(op);
            switch (field.GetValue(op))
            {
                case Action<IAssetData> edit:
                    field.SetValue(op, new Action<IAssetData>(asset =>
                    {
                        StartupClock.Frame? frame = StartupClock.Begin(mod, "asset", pack);
                        try
                        {
                            edit(asset);
                        }
                        finally
                        {
                            StartupClock.End(frame);
                        }
                    }));
                    break;
                case Func<IAssetInfo, object> load:
                    field.SetValue(op, new Func<IAssetInfo, object>(asset =>
                    {
                        StartupClock.Frame? frame = StartupClock.Begin(mod, "asset", pack);
                        try
                        {
                            return load(asset);
                        }
                        finally
                        {
                            StartupClock.End(frame);
                        }
                    }));
                    break;
            }
        }
    }

    private static (string Mod, string? Pack) Owner(object op)
    {
        Type type = op.GetType();
        string mod = (type.GetProperty("Mod")?.GetValue(op) as IModInfo)?.Manifest.UniqueID ?? "unknown";
        string? pack = (type.GetProperty("OnBehalfOf")?.GetValue(op) as IModInfo)?.Manifest.UniqueID;
        return (mod, pack == mod ? null : pack);
    }
}
