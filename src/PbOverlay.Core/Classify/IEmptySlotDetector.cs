namespace PbOverlay.Core.Classify;

public interface IEmptySlotDetector
{
    bool IsEmpty(SlotObservation obs);
}
