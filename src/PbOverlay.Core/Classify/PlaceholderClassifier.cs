using OpenCvSharp;

namespace PbOverlay.Core.Classify;

// ponytail: pre-tuning preview stand-in. Delete once SaturationStateClassifier
// is verified against real footage.
public sealed class PlaceholderClassifier : IStateClassifierBatch
{
    private static readonly IReadOnlyList<SlotObservation> Empty = Array.Empty<SlotObservation>();

    public IReadOnlyList<SlotObservation> LastObservations => Empty;

    public IReadOnlyList<SlotState> ClassifyAll(Mat frameBgra, IReadOnlyList<Rect> slots)
    {
        var states = new SlotState[slots.Count];
        for (var i = 0; i < slots.Count; i++) states[i] = SlotState.Alive;
        return states;
    }
}
