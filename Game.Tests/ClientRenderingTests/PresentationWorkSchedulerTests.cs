using Game.Client.Rendering.Performance;
using Xunit;

namespace Game.Tests.ClientRenderingTests;

public sealed class PresentationWorkSchedulerTests
{
    private static readonly PresentationWorkState StableState = new(
        Revision: 0,
        CameraX: 0,
        CameraY: 0,
        CameraZoom: 1);

    [Fact]
    public void PeriodicCadence_RunsAtConfiguredRateWithoutLimitingFrameAdvancement()
    {
        var scheduler = new PresentationWorkScheduler(1);
        var handle = scheduler.Register(new PresentationWorkSchedule(
            TargetHz: 30,
            MaximumStalenessSeconds: 1,
            MaximumDeferredFrames: 1_000,
            Triggers: PresentationWorkTrigger.Periodic));
        var scheduledCount = 0;

        for (var frame = 0; frame < 120; frame++)
        {
            scheduler.AdvanceFrame(1d / 120d);
            if (scheduler.TrySchedule(handle, StableState, out _))
            {
                scheduledCount++;
            }
        }

        Assert.Equal(120, scheduler.FrameIndex);
        Assert.Equal(30, scheduledCount);
        var telemetry = scheduler.CaptureTelemetry(handle);
        Assert.Equal(120, telemetry.EvaluationCount);
        Assert.Equal(1, telemetry.InitialScheduleCount);
        Assert.Equal(29, telemetry.PeriodicScheduleCount);
    }

    [Fact]
    public void DirtySignal_IsLatchedUntilCadenceBecomesEligible()
    {
        var scheduler = new PresentationWorkScheduler(1);
        var handle = scheduler.Register(new PresentationWorkSchedule(
            TargetHz: 10,
            MaximumStalenessSeconds: 1,
            MaximumDeferredFrames: 100,
            Triggers: PresentationWorkTrigger.Dirty));
        Assert.True(scheduler.TrySchedule(handle, StableState, out _));

        scheduler.AdvanceFrame(0.02);
        var dirtyState = StableState with { IsDirty = true };
        Assert.False(scheduler.TrySchedule(handle, dirtyState, out _));
        for (var frame = 0; frame < 3; frame++)
        {
            scheduler.AdvanceFrame(0.02);
            Assert.False(scheduler.TrySchedule(handle, StableState, out _));
        }

        scheduler.AdvanceFrame(0.02);
        Assert.True(scheduler.TrySchedule(handle, StableState, out var decision));
        Assert.True((decision.Reasons & PresentationWorkReason.Dirty) != 0);
        Assert.Equal(1, scheduler.CaptureTelemetry(handle).DirtyScheduleCount);
    }

    [Fact]
    public void RevisionCameraAndZoomThresholds_SuppressSubThresholdWork()
    {
        var scheduler = new PresentationWorkScheduler(1);
        var handle = scheduler.Register(new PresentationWorkSchedule(
            TargetHz: 10,
            MaximumStalenessSeconds: 2,
            MaximumDeferredFrames: 1_000,
            Triggers:
                PresentationWorkTrigger.Revision |
                PresentationWorkTrigger.CameraTranslation |
                PresentationWorkTrigger.CameraZoom,
            MinimumRevisionDelta: 2,
            CameraTranslationThreshold: 5,
            CameraZoomThreshold: 0.25));
        Assert.True(scheduler.TrySchedule(handle, StableState, out _));

        scheduler.AdvanceFrame(0.1);
        var belowThreshold = new PresentationWorkState(1, 3, 0, 1.2);
        Assert.False(scheduler.TrySchedule(handle, belowThreshold, out _));

        scheduler.AdvanceFrame(0.01);
        var revisionChanged = belowThreshold with { Revision = 2 };
        Assert.True(scheduler.TrySchedule(handle, revisionChanged, out var revisionDecision));
        Assert.Equal(PresentationWorkReason.Revision, revisionDecision.Reasons);

        scheduler.AdvanceFrame(0.1);
        var cameraMoved = revisionChanged with { CameraX = 6, CameraY = 4 };
        Assert.True(scheduler.TrySchedule(handle, cameraMoved, out var cameraDecision));
        Assert.Equal(PresentationWorkReason.CameraTranslation, cameraDecision.Reasons);

        scheduler.AdvanceFrame(0.1);
        var zoomChanged = cameraMoved with { CameraZoom = 1.45 };
        Assert.True(scheduler.TrySchedule(handle, zoomChanged, out var zoomDecision));
        Assert.Equal(PresentationWorkReason.CameraZoom, zoomDecision.Reasons);
    }

    [Fact]
    public void StarvationLimits_RunWorkWithoutPeriodicOrChangeTriggers()
    {
        var scheduler = new PresentationWorkScheduler(1);
        var handle = scheduler.Register(new PresentationWorkSchedule(
            TargetHz: 60,
            MaximumStalenessSeconds: 0.1,
            MaximumDeferredFrames: 5,
            Triggers: PresentationWorkTrigger.None));
        Assert.True(scheduler.TrySchedule(handle, StableState, out _));

        for (var frame = 0; frame < 4; frame++)
        {
            scheduler.AdvanceFrame(0.001);
            Assert.False(scheduler.TrySchedule(handle, StableState, out _));
        }

        scheduler.AdvanceFrame(0.001);
        Assert.True(scheduler.TrySchedule(handle, StableState, out var frameDecision));
        Assert.True((frameDecision.Reasons & PresentationWorkReason.FrameStarvation) != 0);

        scheduler.Configure(handle, new PresentationWorkSchedule(
            TargetHz: 60,
            MaximumStalenessSeconds: 0.05,
            MaximumDeferredFrames: 100,
            Triggers: PresentationWorkTrigger.None));
        for (var frame = 0; frame < 4; frame++)
        {
            scheduler.AdvanceFrame(0.01);
            Assert.False(scheduler.TrySchedule(handle, StableState, out _));
        }

        scheduler.AdvanceFrame(0.01);
        Assert.True(scheduler.TrySchedule(handle, StableState, out var timeDecision));
        Assert.True((timeDecision.Reasons & PresentationWorkReason.TimeStarvation) != 0);
        Assert.Equal(2, scheduler.CaptureTelemetry(handle).StarvationScheduleCount);
    }

    [Fact]
    public void ImmediateRequest_BypassesCadenceAndResetsNextInterval()
    {
        var scheduler = new PresentationWorkScheduler(1);
        var handle = scheduler.Register(new PresentationWorkSchedule(
            TargetHz: 1,
            MaximumStalenessSeconds: 2,
            MaximumDeferredFrames: 1_000,
            Triggers: PresentationWorkTrigger.Periodic));
        Assert.True(scheduler.TrySchedule(handle, StableState, out _));

        scheduler.AdvanceFrame(0.01);
        scheduler.RequestImmediate(handle);
        Assert.True(scheduler.TrySchedule(handle, StableState, out var decision));
        Assert.Equal(PresentationWorkReason.ImmediateRequest, decision.Reasons);

        scheduler.AdvanceFrame(0.99);
        Assert.False(scheduler.TrySchedule(handle, StableState, out _));
        scheduler.AdvanceFrame(0.01);
        Assert.True(scheduler.TrySchedule(handle, StableState, out var periodicDecision));
        Assert.Equal(PresentationWorkReason.Periodic, periodicDecision.Reasons);
    }
}
