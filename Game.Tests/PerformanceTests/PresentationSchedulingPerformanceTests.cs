using System.Diagnostics;
using Game.Client.Rendering.Performance;
using Xunit;
using Xunit.Abstractions;

namespace Game.Tests.PerformanceTests;

[Collection(LongSessionPerformanceCollection.Name)]
public sealed class PresentationSchedulingPerformanceTests
{
    private readonly ITestOutputHelper _output;

    public PresentationSchedulingPerformanceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void SchedulerAndFrameTelemetry_AreAllocationFreeInSteadyState()
    {
        const int measuredFrames = 200_000;
        var scheduler = new PresentationWorkScheduler(3);
        var lighting = scheduler.Register(CreateSchedule(60));
        var reflections = scheduler.Register(CreateSchedule(30));
        var atmosphere = scheduler.Register(CreateSchedule(20));
        var frameTimes = new FrameTimeTelemetryWindow(64);

        RunFrames(scheduler, lighting, reflections, atmosphere, frameTimes, 10_000);
        for (var index = 0; index < 100; index++)
        {
            _ = frameTimes.Capture();
        }

        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var startedAt = Stopwatch.GetTimestamp();
        var scheduledCount = RunFrames(
            scheduler,
            lighting,
            reflections,
            atmosphere,
            frameTimes,
            measuredFrames);
        var elapsed = Stopwatch.GetElapsedTime(startedAt);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

        Assert.Equal(0, allocated);
        Assert.True(scheduledCount > 0);
        Assert.True(elapsed < TimeSpan.FromSeconds(5), $"elapsed={elapsed}");

        var captureAllocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < 10_000; index++)
        {
            _ = frameTimes.Capture();
        }

        var captureAllocated = GC.GetAllocatedBytesForCurrentThread() - captureAllocatedBefore;
        Assert.Equal(0, captureAllocated);
        _output.WriteLine(
            "frames={0} decisions={1} elapsedMs={2:0.000} nsPerDecision={3:0.0} hotPathAllocatedBytes={4} captureAllocatedBytes={5}",
            measuredFrames,
            measuredFrames * 3,
            elapsed.TotalMilliseconds,
            elapsed.TotalMilliseconds * 1_000_000d / (measuredFrames * 3),
            allocated,
            captureAllocated);
    }

    private static long RunFrames(
        PresentationWorkScheduler scheduler,
        PresentationWorkHandle lighting,
        PresentationWorkHandle reflections,
        PresentationWorkHandle atmosphere,
        FrameTimeTelemetryWindow frameTimes,
        int frameCount)
    {
        long scheduledCount = 0;
        for (var frame = 0; frame < frameCount; frame++)
        {
            scheduler.AdvanceFrame(1d / 165d);
            var revision = frame / 8;
            var cameraX = frame * 0.125;
            var state = new PresentationWorkState(
                revision,
                cameraX,
                Math.Sin(frame * 0.001) * 4,
                1 + ((frame & 255) * 0.0001),
                IsDirty: (frame & 63) == 0);
            scheduledCount += scheduler.TrySchedule(lighting, state, out _) ? 1 : 0;
            scheduledCount += scheduler.TrySchedule(reflections, state, out _) ? 1 : 0;
            scheduledCount += scheduler.TrySchedule(atmosphere, state, out _) ? 1 : 0;
            frameTimes.RecordMilliseconds(5.5 + ((frame & 31) * 0.05));
        }

        return scheduledCount;
    }

    private static PresentationWorkSchedule CreateSchedule(double targetHz)
    {
        return new PresentationWorkSchedule(
            TargetHz: targetHz,
            MaximumStalenessSeconds: 0.25,
            MaximumDeferredFrames: 90,
            Triggers:
                PresentationWorkTrigger.Periodic |
                PresentationWorkTrigger.Dirty |
                PresentationWorkTrigger.Revision |
                PresentationWorkTrigger.CameraTranslation |
                PresentationWorkTrigger.CameraZoom,
            MinimumRevisionDelta: 1,
            CameraTranslationThreshold: 1,
            CameraZoomThreshold: 0.01);
    }
}
