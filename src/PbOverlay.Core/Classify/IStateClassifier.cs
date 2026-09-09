using OpenCvSharp;

namespace PbOverlay.Core.Classify;

public interface IStateClassifierBatch
{
    IReadOnlyList<SlotState> ClassifyAll(Mat frameBgra, IReadOnlyList<Rect> slots);
    IReadOnlyList<SlotObservation> LastObservations { get; }
}
