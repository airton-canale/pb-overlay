using OpenCvSharp;

namespace PbOverlay.Core.Capture;

/// <summary>
/// A single captured frame. The <see cref="Bgra"/> Mat is owned by the
/// producer and is only valid until the next frame is delivered — consumers
/// must clone it if they need to keep the data around.
/// </summary>
public sealed record CapturedFrame(Mat Bgra, int Width, int Height, long TimestampTicks);
