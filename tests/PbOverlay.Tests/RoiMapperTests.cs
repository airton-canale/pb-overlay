using OpenCvSharp;
using PbOverlay.Core.Roi;
using Xunit;

namespace PbOverlay.Tests;

public class RoiMapperTests
{
    [Fact]
    public void SliceSlots_produces_expected_count_and_widths()
    {
        var slots = RoiMapper.SliceSlots(new Rect(100, 50, 800, 60), 8);
        Assert.Equal(8, slots.Count);
        Assert.Equal(100, slots[0].X);
        Assert.InRange(slots[7].X + slots[7].Width, 899, 901);
        foreach (var s in slots)
        {
            Assert.Equal(60, s.Height);
            Assert.Equal(50, s.Y);
            Assert.InRange(s.Width, 99, 101);
        }
    }

    [Fact]
    public void SliceSlots_zero_slot_count_returns_empty()
    {
        Assert.Empty(RoiMapper.SliceSlots(new Rect(0, 0, 100, 20), 0));
    }
}
