namespace PbOverlay.Core.Config;

public sealed class OverlayConfig
{
    public double PositionX { get; set; } = -1;
    public double PositionY { get; set; } = -1;
    public double FontSize { get; set; } = 64;
    public string TextColor { get; set; } = "#FFFFFFFF";
    public string OutlineColor { get; set; } = "#FF000000";
    public double OutlineThickness { get; set; } = 3;
    public bool Locked { get; set; } = true;
    public string ToggleHotkey { get; set; } = "F9";
}
