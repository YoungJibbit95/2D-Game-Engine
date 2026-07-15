using Game.Core.Runtime;
using System.Diagnostics;
using Xunit;

namespace Game.Tests.PerformanceTests;

public sealed class SimulationPhaseTelemetryPerformanceTests
{
    [Fact]
    public void DisabledMeasure_IsAllocationFreeAcrossLargeSteadyStateLoop()
    {
        var telemetry = new SimulationPhaseTelemetry();
        const int iterations = 200_000;

        for (var index = 0; index < 2_000; index++)
        {
            telemetry.Measure(GameSimulationPhase.Player).Dispose();
        }

        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < iterations; index++)
        {
            telemetry.Measure((GameSimulationPhase)(index % 16)).Dispose();
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        Assert.Equal(0, allocated);
        Assert.All(telemetry.CaptureSnapshot().Measurements, measurement => Assert.Equal(0, measurement.Samples));
    }

    [Fact]
    public void EnabledMeasure_RecordsEveryDeclaredSimulationPhase()
    {
        var telemetry = new SimulationPhaseTelemetry();
        telemetry.SetEnabled(true);

        foreach (var phase in Enum.GetValues<GameSimulationPhase>())
        {
            using (telemetry.Measure(phase))
            {
                Thread.SpinWait(32);
            }
        }

        var snapshot = telemetry.CaptureSnapshot();
        Assert.True(snapshot.IsEnabled);
        Assert.Equal(Enum.GetValues<GameSimulationPhase>().Length, snapshot.Measurements.Count);
        foreach (var phase in Enum.GetValues<GameSimulationPhase>())
        {
            Assert.True(snapshot.TryGet(phase, out var measurement));
            Assert.Equal(1, measurement.Samples);
            Assert.True(measurement.TotalElapsedTicks > 0);
            Assert.True(measurement.AverageMilliseconds >= 0);
        }
    }

    [Fact]
    public void EnabledMeasure_UsesBoundedAggregateAllocationInsteadOfPerSampleObjects()
    {
        var telemetry = new SimulationPhaseTelemetry();
        telemetry.SetEnabled(true);
        const int iterations = 100_000;

        for (var index = 0; index < 2_000; index++)
        {
            telemetry.Measure(GameSimulationPhase.Entities).Dispose();
        }

        // Cross the tiered-JIT promotion threshold before measuring the steady-state path.
        for (var index = 0; index < iterations; index++)
        {
            telemetry.Measure(GameSimulationPhase.Entities).Dispose();
        }

        telemetry.Reset();

        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var startedAt = Stopwatch.GetTimestamp();
        for (var index = 0; index < iterations; index++)
        {
            telemetry.Measure(GameSimulationPhase.Entities).Dispose();
        }

        var elapsed = Stopwatch.GetElapsedTime(startedAt);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        Assert.Equal(0, allocated);
        Assert.True(elapsed < TimeSpan.FromSeconds(10), $"elapsed={elapsed}");

        Assert.True(telemetry.CaptureSnapshot().TryGet(GameSimulationPhase.Entities, out var measurement));
        Assert.Equal(iterations, measurement.Samples);
    }
}
