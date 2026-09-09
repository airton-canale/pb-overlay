namespace PbOverlay.Core.Roi;

/// <summary>
/// A named HUD layout: three normalized rects (whole portrait bar, plus team
/// A and team B clusters) and the number of portrait slots per team.
///
/// A profile list is stored, not a single profile, because Point Blank has
/// multiple HUD layouts per mode. Switching profiles is a single-click op
/// in the calibration tab.
/// </summary>
public sealed class RoiProfile
{
    public string Name { get; set; } = "default";

    /// <summary>The whole portrait bar. Used as a sanity/hit-test area.</summary>
    public NormalizedRect Bar { get; set; } = NormalizedRect.Empty;

    /// <summary>Team A's slot cluster.</summary>
    public NormalizedRect TeamA { get; set; } = NormalizedRect.Empty;

    /// <summary>Team B's slot cluster.</summary>
    public NormalizedRect TeamB { get; set; } = NormalizedRect.Empty;

    public int SlotsPerTeam { get; set; } = 8;

    public bool IsCalibrated =>
        !Bar.IsEmpty && !TeamA.IsEmpty && !TeamB.IsEmpty && SlotsPerTeam > 0;
}
