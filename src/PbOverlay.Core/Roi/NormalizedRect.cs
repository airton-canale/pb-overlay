using OpenCvSharp;

namespace PbOverlay.Core.Roi;

/// <summary>A rectangle in normalized capture-window coordinates (0..1).</summary>
public readonly record struct NormalizedRect(double X, double Y, double Width, double Height)
{
    public static readonly NormalizedRect Empty = new(0, 0, 0, 0);

    public bool IsEmpty => Width <= 0 || Height <= 0;

    public Rect ToPixel(int frameWidth, int frameHeight)
    {
        var px = (int)Math.Round(X * frameWidth);
        var py = (int)Math.Round(Y * frameHeight);
        var pw = (int)Math.Round(Width * frameWidth);
        var ph = (int)Math.Round(Height * frameHeight);
        // Clamp to frame bounds; a slightly-out-of-frame rect must not crash.
        px = Math.Clamp(px, 0, frameWidth);
        py = Math.Clamp(py, 0, frameHeight);
        pw = Math.Clamp(pw, 0, frameWidth - px);
        ph = Math.Clamp(ph, 0, frameHeight - py);
        return new Rect(px, py, pw, ph);
    }

    public static NormalizedRect FromPixel(Rect r, int frameWidth, int frameHeight) =>
        new((double)r.X / frameWidth, (double)r.Y / frameHeight,
            (double)r.Width / frameWidth, (double)r.Height / frameHeight);
}
