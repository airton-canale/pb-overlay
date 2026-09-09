namespace PbOverlay.Core.Config;

public sealed class FpsConfig
{
    public bool Enabled { get; set; } = true;
    public bool ShowFrameTimeMs { get; set; } = false;
    public int RollingWindowSeconds { get; set; } = 5;
}
