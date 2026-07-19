using Game.Core.Diagnostics.Performance;
using Xunit;

namespace Game.Tests.PerformanceTests;

[Collection(LongSessionPerformanceCollection.Name)]
public sealed class LongSessionDistributionCollectorTests
{
    [Fact]
    public void Capture_ComputesNearestRankPercentilesAndOverBudgetRatio()
    {
        var collector = new LongSessionDistributionCollector(
            "simulation.test.tick-ms",
            capacity: 10,
            budget: 5);

        for (var sample = 1; sample <= 10; sample++)
        {
            collector.Add(sample);
        }

        var snapshot = collector.Capture();
        Assert.Equal(10, snapshot.Capacity);
        Assert.Equal(10, snapshot.RetainedSampleCount);
        Assert.Equal(10, snapshot.TotalSampleCount);
        Assert.Equal(5, snapshot.Budget);
        Assert.Equal(5.5, snapshot.Average, 8);
        Assert.Equal(5, snapshot.P50);
        Assert.Equal(10, snapshot.P95);
        Assert.Equal(10, snapshot.P99);
        Assert.Equal(10, snapshot.Maximum);
        Assert.Equal(5, snapshot.OverBudgetSampleCount);
        Assert.Equal(0.5, snapshot.OverBudgetRatio, 8);
    }

    [Fact]
    public void RingWrap_RetainsNewestBoundedSamplesAndReplacesBudgetCounts()
    {
        var collector = new LongSessionDistributionCollector(
            "streaming.test.wrap-ms",
            capacity: 4,
            budget: 4);

        for (var sample = 1; sample <= 6; sample++)
        {
            collector.Add(sample);
        }

        var snapshot = collector.Capture();
        Assert.Equal(4, collector.Count);
        Assert.Equal(6, collector.TotalSampleCount);
        Assert.Equal(4, snapshot.RetainedSampleCount);
        Assert.Equal(6, snapshot.TotalSampleCount);
        Assert.Equal(4.5, snapshot.Average, 8);
        Assert.Equal(4, snapshot.P50);
        Assert.Equal(6, snapshot.P95);
        Assert.Equal(6, snapshot.P99);
        Assert.Equal(6, snapshot.Maximum);
        Assert.Equal(2, snapshot.OverBudgetSampleCount);
        Assert.Equal(0.5, snapshot.OverBudgetRatio, 8);
    }

    [Fact]
    public void Boundaries_RejectInvalidConfigurationAndSamples()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new LongSessionDistributionCollector("simulation.test.tick-ms", 0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new LongSessionDistributionCollector("simulation.test.tick-ms", 1, double.NaN));
        Assert.Throws<ArgumentException>(
            () => new LongSessionDistributionCollector("Simulation Test", 1, 1));

        var collector = new LongSessionDistributionCollector("simulation.test.tick-ms", 1, 1);
        Assert.Throws<ArgumentOutOfRangeException>(() => collector.Add(-0.01));
        Assert.Throws<ArgumentOutOfRangeException>(() => collector.Add(double.PositiveInfinity));

        collector.Add(1);
        Assert.Equal(0, collector.Capture().OverBudgetSampleCount);
        collector.Add(1.0001);
        Assert.Equal(1, collector.Capture().OverBudgetSampleCount);
    }

    [Fact]
    public void Labels_AreCanonicalAndStableForAllLongSessionScenarios()
    {
        Assert.Equal(
            LongSessionDistributionLabels.StreamingColdBidirectionalSettleMilliseconds,
            LongSessionDistributionLabel.Create("STREAMING", "Camera_Bidirectional", "Cold Settle MS"));
        Assert.Equal(
            LongSessionDistributionLabels.StreamingWarmBidirectionalSettleMilliseconds,
            LongSessionDistributionLabel.Create(" streaming ", "camera  bidirectional", "warm-settle-ms"));
        Assert.Equal(
            LongSessionDistributionLabels.SpawnFinalTreeTwoHundredMaintenanceMilliseconds,
            LongSessionDistributionLabel.Create("Spawn", "Final Tree 200", "Maintenance MS"));
        Assert.Equal(
            LongSessionDistributionLabels.AiFinalTreeTwoHundredUpdateMilliseconds,
            LongSessionDistributionLabel.Create("Simulation", "Final Tree 200", "AI Update MS"));

        Assert.Throws<ArgumentException>(
            () => LongSessionDistributionLabel.Create("simulation", "final/tree", "tick-ms"));
    }

    [Fact]
    public void AddAndCapture_DoNotAllocateAfterWarmup()
    {
        var collector = new LongSessionDistributionCollector(
            "simulation.allocation.tick-ms",
            capacity: 256,
            budget: 8);

        for (var index = 0; index < 10_000; index++)
        {
            collector.Add(index % 17);
            if ((index & 255) == 0)
            {
                collector.Capture();
            }
        }

        collector.Clear();
        var snapshot = default(LongSessionDistributionSnapshot);
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < 100_000; index++)
        {
            collector.Add(index % 17);
            if ((index & 255) == 0)
            {
                snapshot = collector.Capture();
            }
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        Assert.Equal(0, allocated);
        Assert.Equal("simulation.allocation.tick-ms", snapshot.Label);
        Assert.Equal(100_000, collector.TotalSampleCount);
    }
}
