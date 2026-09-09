using System;
using PbOverlay.Core.Classify;
using Xunit;

namespace PbOverlay.Tests;

public class TemporalSmootherTests
{
    [Fact]
    public void Requires_N_consecutive_agreements_before_flipping()
    {
        var s = new TemporalSmoother(1, 3);
        var empty = new[] { SlotState.Empty };
        var alive = new[] { SlotState.Alive };

        s.Update(empty);
        s.Update(empty);
        var stable = s.Update(empty);
        Assert.Equal(SlotState.Empty, stable[0]);

        stable = s.Update(alive);
        Assert.Equal(SlotState.Empty, stable[0]);

        stable = s.Update(alive);
        Assert.Equal(SlotState.Empty, stable[0]);

        stable = s.Update(alive);
        Assert.Equal(SlotState.Alive, stable[0]);
    }

    [Fact]
    public void Noise_does_not_flip_state()
    {
        var s = new TemporalSmoother(1, 3);
        var alive = new[] { SlotState.Alive };
        var dead = new[] { SlotState.Dead };

        s.Update(alive);
        s.Update(alive);
        var stable = s.Update(alive);
        Assert.Equal(SlotState.Alive, stable[0]);

        stable = s.Update(dead);
        Assert.Equal(SlotState.Alive, stable[0]);
        stable = s.Update(alive);
        Assert.Equal(SlotState.Alive, stable[0]);
        stable = s.Update(dead);
        Assert.Equal(SlotState.Alive, stable[0]);
    }

    [Fact]
    public void ArgumentException_when_count_mismatch()
    {
        var s = new TemporalSmoother(2, 3);
        Assert.Throws<ArgumentException>(() => s.Update(new[] { SlotState.Alive }));
    }
}
