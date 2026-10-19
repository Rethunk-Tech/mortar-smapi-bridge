using System.Reflection;
using StardewModdingAPI;
using StardewModdingAPI.Events;

namespace MortarSmapiBridge.Startup;

/// <summary>Skips the title screen's intro animation when the Mortar profile asks for it. Mortar writes
/// <c>startup/skip-intro</c> in the profile before a launch and removes it when the player wants the intro; both of
/// the request's levels end at the title buttons, which are Stardew Valley's main menu.</summary>
internal sealed class IntroSkip
{
    private static object? lastSkipped;

    private readonly IModHelper helper;
    private readonly IMonitor monitor;

    private IntroSkip(IModHelper helper, IMonitor monitor)
    {
        this.helper = helper;
        this.monitor = monitor;
    }

    /// <summary>Whether the profile in <paramref name="profileDirectory"/> asks to skip the intro.</summary>
    internal static bool Requested(string profileDirectory) =>
        File.Exists(Path.Combine(profileDirectory, "startup", "skip-intro"));

    /// <summary>Watches for the title screen when the Mortar profile this mod runs in asks for it.</summary>
    internal static void Start(IModHelper helper, IMonitor monitor)
    {
        if (ProfileDirectory.Find(helper.DirectoryPath) is { } profile && Requested(profile))
            new IntroSkip(helper, monitor).Attach();
    }

    /// <summary>Jumps the title menu to its buttons, once per menu so a measured launch that also asks does not repeat it.</summary>
    internal static void Skip(object menu)
    {
        if (ReferenceEquals(lastSkipped, menu))
            return;
        lastSkipped = menu;
        menu.GetType().GetMethod("skipToTitleButtons", BindingFlags.Instance | BindingFlags.Public)?.Invoke(menu, null);
    }

    private void Attach() => this.helper.Events.Display.RenderedActiveMenu += this.OnRenderedActiveMenu;

    private void OnRenderedActiveMenu(object? sender, RenderedActiveMenuEventArgs e)
    {
        try
        {
            object? menu = StartupSession.Game1Menu();
            if (menu?.GetType().Name != "TitleMenu")
                return;
            Skip(menu);
        }
        catch (Exception ex)
        {
            this.monitor.Log($"Skipping the intro failed: {ex.Message}", LogLevel.Trace);
        }
        this.helper.Events.Display.RenderedActiveMenu -= this.OnRenderedActiveMenu;
    }
}
