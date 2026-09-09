namespace PbOverlay.Core.Config;

public sealed class GameTargetConfig
{
    public string TitleSubstring { get; set; } = "";
    public string ProcessName { get; set; } = "";

    public bool IsValid() =>
        !string.IsNullOrWhiteSpace(TitleSubstring) && !string.IsNullOrWhiteSpace(ProcessName);
}
