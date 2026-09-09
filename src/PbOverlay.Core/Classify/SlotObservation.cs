using OpenCvSharp;

namespace PbOverlay.Core.Classify;

public sealed record SlotObservation(Rect Region, double MeanSaturation, double GrayscaleVariance);
