namespace PbOverlay.Core.Fps;

public sealed record FpsSample(double AvgFps, double OnePercentLowFps, double? FrameTimeMs);
