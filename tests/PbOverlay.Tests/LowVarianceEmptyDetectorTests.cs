using OpenCvSharp;
using PbOverlay.Core.Classify;
using Xunit;

namespace PbOverlay.Tests;

public class LowVarianceEmptyDetectorTests
{
    [Fact]
    public void Low_variance_observation_is_empty()
    {
        var obs = new SlotObservation(new Rect(0, 0, 10, 10), 0, 1.0);
        Assert.True(new LowVarianceEmptyDetector(15.0).IsEmpty(obs));
    }

    [Fact]
    public void High_variance_observation_is_not_empty()
    {
        var obs = new SlotObservation(new Rect(0, 0, 10, 10), 0, 100.0);
        Assert.False(new LowVarianceEmptyDetector(15.0).IsEmpty(obs));
    }

    [Fact]
    public void Custom_threshold_respected()
    {
        var detector = new LowVarianceEmptyDetector(200.0);
        Assert.True(detector.IsEmpty(new SlotObservation(new Rect(0, 0, 10, 10), 0, 150.0)));
        Assert.False(detector.IsEmpty(new SlotObservation(new Rect(0, 0, 10, 10), 0, 250.0)));
    }
}
