using OpenCvSharp;

namespace PbOverlay.Core.Roi;

public static class RoiMapper
{
    /// <summary>
    /// Slice a team-cluster rect into <paramref name="slotCount"/> equal-width
    /// slot rects laid out left-to-right. The height matches the cluster.
    /// </summary>
    public static IReadOnlyList<Rect> SliceSlots(Rect cluster, int slotCount)
    {
        if (slotCount <= 0) return Array.Empty<Rect>();
        var slots = new Rect[slotCount];
        var slotW = cluster.Width / (double)slotCount;
        for (var i = 0; i < slotCount; i++)
        {
            var x = cluster.X + (int)Math.Round(i * slotW);
            var next = cluster.X + (int)Math.Round((i + 1) * slotW);
            slots[i] = new Rect(x, cluster.Y, next - x, cluster.Height);
        }
        return slots;
    }

    public static (IReadOnlyList<Rect> teamA, IReadOnlyList<Rect> teamB) SliceProfile(
        RoiProfile profile, int frameWidth, int frameHeight)
    {
        var a = SliceSlots(profile.TeamA.ToPixel(frameWidth, frameHeight), profile.SlotsPerTeam);
        var b = SliceSlots(profile.TeamB.ToPixel(frameWidth, frameHeight), profile.SlotsPerTeam);
        return (a, b);
    }
}
