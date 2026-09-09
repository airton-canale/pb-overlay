using System.Collections.Generic;
using OpenCvSharp;
using PbOverlay.Core.Classify;

namespace PbOverlay.Tests.Fixtures;

public static class SyntheticFixtures
{
    public static Mat MakeAliveSlot(int w = 64, int h = 64)
        => new Mat(h, w, MatType.CV_8UC4, new Scalar(0, 0, 255, 255));

    public static Mat MakeDeadSlot(int w = 64, int h = 64)
        => new Mat(h, w, MatType.CV_8UC4, new Scalar(128, 128, 128, 255));

    public static Mat MakeEmptySlot(int w = 64, int h = 64)
    {
        var patch = new Mat(h, w, MatType.CV_8UC4, new Scalar(20, 20, 20, 255));
        Cv2.Randn(patch, new Scalar(0, 0, 0, 0), new Scalar(0.5, 0.5, 0.5, 0));
        return patch;
    }

    public static Mat MakeFrameWithSlots(IEnumerable<(SlotState kind, Rect where)> slots, int width, int height)
    {
        var frame = new Mat(height, width, MatType.CV_8UC4, Scalar.All(0));
        foreach (var (kind, rect) in slots)
        {
            using var patch = kind switch
            {
                SlotState.Alive => MakeAliveSlot(rect.Width, rect.Height),
                SlotState.Dead => MakeDeadSlot(rect.Width, rect.Height),
                _ => MakeEmptySlot(rect.Width, rect.Height),
            };
            using var view = new Mat(frame, rect);
            patch.CopyTo(view);
        }
        return frame;
    }
}
