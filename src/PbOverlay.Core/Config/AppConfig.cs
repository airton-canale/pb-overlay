using PbOverlay.Core.Roi;

namespace PbOverlay.Core.Config;

public sealed class AppConfig
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public GameTargetConfig Target { get; set; } = new();
    public List<RoiProfile> RoiProfiles { get; set; } = new();
    public string ActiveRoiProfileName { get; set; } = "";
    public ClassifierConfig Classifier { get; set; } = new();
    public OverlayConfig Overlay { get; set; } = new();
    public FpsConfig Fps { get; set; } = new();

    public RoiProfile? GetActiveProfile() =>
        RoiProfiles.FirstOrDefault(p => string.Equals(p.Name, ActiveRoiProfileName, StringComparison.Ordinal));
}
