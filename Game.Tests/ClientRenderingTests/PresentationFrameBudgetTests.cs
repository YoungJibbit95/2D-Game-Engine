using Game.Client.Rendering.Performance;
using Xunit;

namespace Game.Tests.ClientRenderingTests;

public sealed class PresentationFrameBudgetTests
{
    private static readonly PresentationWorkState StableState = new(
        Revision: 0,
        CameraX: 0,
        CameraY: 0,
        CameraZoom: 1);

    [Fact]
    public void FrameBudget_DefersNonStarvedWorkInsteadOfClusteringRefreshes()
    {
        var scheduler = new PresentationWorkScheduler(2);
        var first = scheduler.Register(CreatePeriodicSchedule(maximumDeferredFrames: 100));
        var second = scheduler.Register(CreatePeriodicSchedule(maximumDeferredFrames: 100));
        var budget = new PresentationFrameBudget(10);

        Assert.True(scheduler.TrySchedule(first, StableState, 2, budget, out _));
        Assert.True(scheduler.TrySchedule(second, StableState, 2, budget, out _));

        scheduler.AdvanceFrame(1);
        budget.Reset(3);
        Assert.True(scheduler.TrySchedule(first, StableState, 2, budget, out _));
        Assert.False(scheduler.TrySchedule(second, StableState, 2, budget, out var deferred));

        var telemetry = budget.CaptureTelemetry();
        Assert.False(deferred.ShouldRun);
        Assert.Equal(2, telemetry.ConsumedUnits);
        Assert.Equal(1, telemetry.AdmittedWorkCount);
        Assert.Equal(1, telemetry.DeferredWorkCount);
        Assert.Equal(0, telemetry.ForcedOverBudgetCount);
    }

    [Fact]
    public void FrameBudget_StarvationProtectionEventuallyForcesDeferredWork()
    {
        var scheduler = new PresentationWorkScheduler(2);
        var first = scheduler.Register(CreatePeriodicSchedule(maximumDeferredFrames: 100));
        var second = scheduler.Register(CreatePeriodicSchedule(maximumDeferredFrames: 2));
        var budget = new PresentationFrameBudget(4);

        Assert.True(scheduler.TrySchedule(first, StableState, 2, budget, out _));
        Assert.True(scheduler.TrySchedule(second, StableState, 2, budget, out _));

        scheduler.AdvanceFrame(1);
        budget.Reset(2);
        Assert.True(scheduler.TrySchedule(first, StableState, 2, budget, out _));
        Assert.False(scheduler.TrySchedule(second, StableState, 2, budget, out _));

        scheduler.AdvanceFrame(1);
        budget.Reset(2);
        Assert.True(scheduler.TrySchedule(first, StableState, 2, budget, out _));
        Assert.True(scheduler.TrySchedule(second, StableState, 2, budget, out var forced));

        var telemetry = budget.CaptureTelemetry();
        Assert.True((forced.Reasons & PresentationWorkReason.FrameStarvation) != 0);
        Assert.Equal(4, telemetry.ConsumedUnits);
        Assert.Equal(1, telemetry.ForcedOverBudgetCount);
    }

    [Fact]
    public void BudgetedScheduling_ReusesStateWithoutSteadyStateAllocation()
    {
        var scheduler = new PresentationWorkScheduler(1);
        var handle = scheduler.Register(new PresentationWorkSchedule(
            TargetHz: 120,
            MaximumStalenessSeconds: 1,
            MaximumDeferredFrames: 1_000,
            Triggers: PresentationWorkTrigger.Periodic));
        var budget = new PresentationFrameBudget(4);
        Assert.True(scheduler.TrySchedule(handle, StableState, 2, budget, out _));

        var allScheduled = true;
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var frame = 0; frame < 1_000; frame++)
        {
            scheduler.AdvanceFrame(1d / 120d);
            budget.Reset(4);
            allScheduled &= scheduler.TrySchedule(handle, StableState, 2, budget, out _);
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(allScheduled);
        Assert.Equal(0, allocated);
    }

    [Fact]
    public void FrameBudget_RejectsInvalidCapacityAndWorkCost()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PresentationFrameBudget(0));

        var scheduler = new PresentationWorkScheduler(1);
        var handle = scheduler.Register(CreatePeriodicSchedule(maximumDeferredFrames: 10));
        var budget = new PresentationFrameBudget(1);
        Assert.Throws<ArgumentOutOfRangeException>(
            () => scheduler.TrySchedule(handle, StableState, 0, budget, out _));
    }

    private static PresentationWorkSchedule CreatePeriodicSchedule(int maximumDeferredFrames)
    {
        return new PresentationWorkSchedule(
            TargetHz: 1,
            MaximumStalenessSeconds: 10,
            MaximumDeferredFrames: maximumDeferredFrames,
            Triggers: PresentationWorkTrigger.Periodic);
    }
}
