using OpenCvSharp;

namespace PbOverlay.Core.Classify;

public interface IStateClassifier
{
    SlotState Classify(SlotObservation obs, double aliveDeadThreshold);
}

public interface IStateClassifierBatch
{
    IReadOnlyList<SlotState> ClassifyAll(Mat frameBgra, IReadOnlyList<Rect> slots);
    IReadOnlyList<SlotObservation> LastObservations { get; }
}
