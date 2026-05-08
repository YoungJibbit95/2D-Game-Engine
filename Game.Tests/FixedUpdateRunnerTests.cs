using Game.Core;
using Xunit;

namespace Game.Tests;

public sealed class FixedUpdateRunnerTests
{
    [Fact]
    public void Run_DoesNotTickBeforeStepIsFull()
    {
        var runner = new FixedUpdateRunner(0.1);
        var ticks = 0;

        var steps = runner.Run(0.05, _ => ticks++);

        Assert.Equal(0, steps);
        Assert.Equal(0, ticks);
        Assert.Equal(0.05, runner.AccumulatorSeconds, precision: 6);
    }

    [Fact]
    public void Run_AccumulatesElapsedTimeAcrossFrames()
    {
        var runner = new FixedUpdateRunner(0.1);
        var ticks = 0;

        runner.Run(0.05, _ => ticks++);
        var steps = runner.Run(0.06, _ => ticks++);

        Assert.Equal(1, steps);
        Assert.Equal(1, ticks);
        Assert.Equal(0.01, runner.AccumulatorSeconds, precision: 6);
    }

    [Fact]
    public void Run_ClampsLargeFramesToMaximumSubSteps()
    {
        var runner = new FixedUpdateRunner(0.1, maxSubSteps: 3);
        var ticks = 0;

        var steps = runner.Run(1.0, _ => ticks++);

        Assert.Equal(3, steps);
        Assert.Equal(3, ticks);
        Assert.True(runner.AccumulatorSeconds < 0.1);
    }
}
