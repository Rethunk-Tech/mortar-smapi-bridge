using System.Reflection;
using StardewModdingAPI;
using StardewModdingAPI.Events;

namespace MortarSmapiBridge.Startup;

/// <summary>
/// Tells Mortar the game is up: one log line, <see cref="Marker"/>, once the title screen has settled. Mortar's launch
/// screen waits for it, since SMAPI's own "Loaded N mods" comes before any mod has run or any content has loaded.
/// </summary>
internal static class TitleScreen
{
    internal const string Marker = "Title screen reached.";

    internal static void Start(IModHelper helper, IMonitor monitor)
    {
        void OnRendered(object? sender, RenderedActiveMenuEventArgs e)
        {
            if (Settled(StartupSession.Game1Menu()))
            {
                helper.Events.Display.RenderedActiveMenu -= OnRendered;
                monitor.Log(Marker, LogLevel.Info);
            }
        }
        helper.Events.Display.RenderedActiveMenu += OnRendered;
    }

    /// <summary>Whether menu is the title screen with its logo settled and its buttons usable.</summary>
    internal static bool Settled(object? menu)
    {
        if (menu?.GetType().Name != "TitleMenu")
            return false;
        FieldInfo? settled = menu.GetType().GetField("titleInPosition", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        return settled == null || settled.GetValue(menu) is true;
    }
}
