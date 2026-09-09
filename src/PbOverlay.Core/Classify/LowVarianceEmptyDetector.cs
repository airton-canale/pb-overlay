namespace PbOverlay.Core.Classify;

public sealed class LowVarianceEmptyDetector : IEmptySlotDetector
{
    private readonly double _varianceThreshold;

    public LowVarianceEmptyDetector(double varianceThreshold = 15.0)
    {
        _varianceThreshold = varianceThreshold;
    }

    public bool IsEmpty(SlotObservation obs) => obs.GrayscaleVariance < _varianceThreshold;
}
