using Game.Client.Input;
using Xunit;

namespace Game.Tests.ClientStateTests;

public sealed class FixedStepButtonLatchTests
{
    [Fact]
    public void ShortPressSurvivesReleaseUntilOneFixedStepConsumesIt()
    {
        var latch = new FixedStepButtonLatch();

        latch.Observe(isHeld: true, wasPressed: true);
        latch.Observe(isHeld: false, wasPressed: false);

        Assert.True(latch.IsActiveForFixedStep);

        latch.ConsumePress();

        Assert.False(latch.IsActiveForFixedStep);
    }

    [Fact]
    public void HeldInputRemainsActiveAcrossFixedStepsWithoutSynthesizingRelease()
    {
        var latch = new FixedStepButtonLatch();

        latch.Observe(isHeld: true, wasPressed: true);
        latch.ConsumePress();

        Assert.True(latch.IsActiveForFixedStep);

        latch.Observe(isHeld: false, wasPressed: false);

        Assert.False(latch.IsActiveForFixedStep);
    }

    [Fact]
    public void ResetClearsHeldAndPendingInput()
    {
        var latch = new FixedStepButtonLatch();
        latch.Observe(isHeld: true, wasPressed: true);

        latch.Reset();

        Assert.False(latch.IsActiveForFixedStep);
    }

    [Fact]
    public void ObserveAndConsumeSteadyStateAllocateNoManagedMemory()
    {
        var latch = new FixedStepButtonLatch();
        for (var index = 0; index < 32; index++)
        {
            latch.Observe(isHeld: true, wasPressed: index == 0);
            latch.ConsumePress();
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < 1_024; index++)
        {
            latch.Observe(isHeld: (index & 1) == 0, wasPressed: (index & 15) == 0);
            latch.ConsumePress();
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
}
