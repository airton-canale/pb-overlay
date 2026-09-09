using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace PbOverlay.Core.Classify;

public sealed class SaturationStateClassifier : IStateClassifierBatch
{
    private readonly IEmptySlotDetector _emptyDetector;
    private readonly double _threshold;
    private readonly ILogger<SaturationStateClassifier>? _log;
    private readonly List<SlotObservation> _last = new();

    public SaturationStateClassifier(
        IEmptySlotDetector emptyDetector,
        double aliveDeadThreshold,
        ILogger<SaturationStateClassifier>? logger = null)
    {
        _emptyDetector = emptyDetector;
        _threshold = aliveDeadThreshold;
        _log = logger;
    }

    public IReadOnlyList<SlotObservation> LastObservations => _last;

    public IReadOnlyList<SlotState> ClassifyAll(Mat frameBgra, IReadOnlyList<Rect> slots)
    {
        _last.Clear();
        var states = new SlotState[slots.Count];
        for (var i = 0; i < slots.Count; i++)
        {
            var obs = Observe(frameBgra, slots[i]);
            _last.Add(obs);
            states[i] = _emptyDetector.IsEmpty(obs)
                ? SlotState.Empty
                : obs.MeanSaturation >= _threshold ? SlotState.Alive : SlotState.Dead;
        }
        return states;
    }

    private static SlotObservation Observe(Mat frameBgra, Rect slotRect)
    {
        using var roi = new Mat(frameBgra, slotRect);
        using var bgr = new Mat();
        Cv2.CvtColor(roi, bgr, ColorConversionCodes.BGRA2BGR);

        using var hsv = new Mat();
        Cv2.CvtColor(bgr, hsv, ColorConversionCodes.BGR2HSV);
        Cv2.Split(hsv, out var chans);
        try
        {
            var meanSat = Cv2.Mean(chans[1]).Val0;

            using var gray = new Mat();
            Cv2.CvtColor(bgr, gray, ColorConversionCodes.BGR2GRAY);
            Cv2.MeanStdDev(gray, out _, out var std);
            var variance = std.Val0 * std.Val0;

            return new SlotObservation(slotRect, meanSat, variance);
        }
        finally
        {
            foreach (var c in chans) c.Dispose();
        }
    }

    public static double AutoCalibrateThreshold(IEnumerable<double> perSlotMeanSaturations)
    {
        var list = perSlotMeanSaturations.ToList();
        if (list.Count == 0) return 0.0;
        if (list.Count == 1) return list[0];
        var min = list.Min();
        var max = list.Max();
        return (min + max) / 2.0;
    }
}
