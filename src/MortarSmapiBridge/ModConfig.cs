namespace MortarSmapiBridge;

public sealed class ModConfig
{
    public bool StartupTimings { get; set; } = true;
    public bool StartupProfile { get; set; }
    public bool OverlayEnabled { get; set; }
    public bool GmcmEnabled { get; set; } = true;
    public int OverlayPort { get; set; } = 8123;
    public string? OverlayToken { get; set; }
}
