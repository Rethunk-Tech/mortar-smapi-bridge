namespace MortarSmapiBridge;

public sealed class ModConfig
{
    public bool OverlayEnabled { get; set; }
    public bool GmcmEnabled { get; set; } = true;
    public int OverlayPort { get; set; } = 8123;
    public string? OverlayToken { get; set; }
}
