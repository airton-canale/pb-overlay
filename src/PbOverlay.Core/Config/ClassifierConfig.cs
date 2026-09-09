namespace PbOverlay.Core.Config;

public sealed class ClassifierConfig
{
    public double AliveDeadSaturationThreshold { get; set; } = 0.0;
    public bool AutoCalibrateOnFirstRun { get; set; } = true;
    public int TemporalSmoothingFrames { get; set; } = 3;
    public double EmptyVarianceThreshold { get; set; } = 15.0;
}
