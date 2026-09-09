using OpenCvSharp;
using PbOverlay.Core.Classify;
using PbOverlay.Tests.Fixtures;
using Xunit;

namespace PbOverlay.Tests;

public class SaturationStateClassifierTests
{
    [Fact]
    public void Alive_slot_classifies_as_Alive()
    {
        var rect = new Rect(0, 0, 64, 64);
        using var frame = SyntheticFixtures.MakeFrameWithSlots(
            new[] { (SlotState.Alive, rect) }, 64, 64);
        var classifier = new SaturationStateClassifier(
            new LowVarianceEmptyDetector(15.0), aliveDeadThreshold: 50.0);
        var result = classifier.ClassifyAll(frame, new[] { rect });
        Assert.Equal(SlotState.Alive, result[0]);
    }

    [Fact]
    public void Dead_slot_classifies_as_Dead()
    {
        var rect = new Rect(0, 0, 64, 64);
        using var frame = SyntheticFixtures.MakeFrameWithSlots(
            new[] { (SlotState.Dead, rect) }, 64, 64);
        var classifier = new SaturationStateClassifier(
            new LowVarianceEmptyDetector(15.0), aliveDeadThreshold: 50.0);
        var result = classifier.ClassifyAll(frame, new[] { rect });
        Assert.Equal(SlotState.Dead, result[0]);
    }

    [Fact]
    public void Empty_slot_classifies_as_Empty()
    {
        var rect = new Rect(0, 0, 64, 64);
        using var frame = SyntheticFixtures.MakeFrameWithSlots(
            new[] { (SlotState.Empty, rect) }, 64, 64);
        var classifier = new SaturationStateClassifier(
            new LowVarianceEmptyDetector(100.0), aliveDeadThreshold: 50.0);
        var result = classifier.ClassifyAll(frame, new[] { rect });
        Assert.Equal(SlotState.Empty, result[0]);
    }

    [Fact]
    public void AutoCalibrateThreshold_returns_midpoint_between_extremes()
    {
        Assert.Equal(110.0,
            SaturationStateClassifier.AutoCalibrateThreshold(new[] { 10.0, 20.0, 200.0, 210.0 }),
            3);
    }

    [Fact]
    public void LastObservations_populated_after_ClassifyAll()
    {
        var rects = new[]
        {
            new Rect(0, 0, 64, 64),
            new Rect(64, 0, 64, 64),
            new Rect(128, 0, 64, 64),
        };
        using var frame = SyntheticFixtures.MakeFrameWithSlots(
            new[]
            {
                (SlotState.Alive, rects[0]),
                (SlotState.Dead, rects[1]),
                (SlotState.Empty, rects[2]),
            },
            192, 64);
        var classifier = new SaturationStateClassifier(
            new LowVarianceEmptyDetector(15.0), aliveDeadThreshold: 50.0);
        classifier.ClassifyAll(frame, rects);
        Assert.Equal(3, classifier.LastObservations.Count);
    }
}
