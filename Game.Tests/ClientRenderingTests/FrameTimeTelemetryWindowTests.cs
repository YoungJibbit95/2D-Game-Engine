using Game.Client.Rendering.Performance;
using Xunit;

namespace Game.Tests.ClientRenderingTests;

public sealed class FrameTimeTelemetryWindowTests
{
    [Fact]
    public void Capture_ReportsRollingAveragePercentilesAndRefreshBudgets()
    {
        var telemetry = new FrameTimeTelemetryWindow(5);
        telemetry.RecordMilliseconds(1);
        telemetry.RecordMilliseconds(2);
        telemetry.RecordMilliseconds(3);
        telemetry.RecordMilliseconds(4);
        telemetry.RecordMilliseconds(10);

        var snapshot = telemetry.Capture();
        Assert.Equal(5, snapshot.SampleCount);
        Assert.Equal(5, snapshot.TotalSamples);
        Assert.Equal(4, snapshot.AverageMilliseconds, 10);
        Assert.Equal(10, snapshot.P95Milliseconds);
        Assert.Equal(10, snapshot.P99Milliseconds);
        Assert.Equal(10, snapshot.MaximumMilliseconds);
        Assert.Equal(1, snapshot.OverBudget120HzCount);
        Assert.Equal(1, snapshot.OverBudget144HzCount);
        Assert.Equal(1, snapshot.OverBudget165HzCount);

        telemetry.RecordMilliseconds(6.5);
        snapshot = telemetry.Capture();
        Assert.Equal(5, snapshot.SampleCount);
        Assert.Equal(6, snapshot.TotalSamples);
        Assert.Equal(5.1, snapshot.AverageMilliseconds, 10);
        Assert.Equal(1, snapshot.OverBudget120HzCount);
        Assert.Equal(1, snapshot.OverBudget144HzCount);
        Assert.Equal(2, snapshot.OverBudget165HzCount);
    }

    [Fact]
    public void Capture_UsesNearestRankForP95AndP99()
    {
        var telemetry = new FrameTimeTelemetryWindow(100);
        for (var milliseconds = 100; milliseconds >= 1; milliseconds--)
        {
            telemetry.RecordMilliseconds(milliseconds);
        }

        var snapshot = telemetry.Capture();
        Assert.Equal(50.5, snapshot.AverageMilliseconds, 10);
        Assert.Equal(95, snapshot.P95Milliseconds);
        Assert.Equal(99, snapshot.P99Milliseconds);
    }

    [Fact]
    public void BudgetCounts_UseStrictlyOverBudgetSemantics()
    {
        var telemetry = new FrameTimeTelemetryWindow(6);
        telemetry.RecordMilliseconds(FrameTimeBudgets.Budget120HzMilliseconds);
        telemetry.RecordMilliseconds(FrameTimeBudgets.Budget144HzMilliseconds);
        telemetry.RecordMilliseconds(FrameTimeBudgets.Budget165HzMilliseconds);
        telemetry.RecordMilliseconds(FrameTimeBudgets.Budget120HzMilliseconds + 0.0001);
        telemetry.RecordMilliseconds(FrameTimeBudgets.Budget144HzMilliseconds + 0.0001);
        telemetry.RecordMilliseconds(FrameTimeBudgets.Budget165HzMilliseconds + 0.0001);

        var snapshot = telemetry.Capture();
        Assert.Equal(1, snapshot.OverBudget120HzCount);
        Assert.Equal(3, snapshot.OverBudget144HzCount);
        Assert.Equal(5, snapshot.OverBudget165HzCount);
        Assert.Equal(1d / 6d, snapshot.OverBudget120HzRatio, 10);
        Assert.Equal(3d / 6d, snapshot.OverBudget144HzRatio, 10);
        Assert.Equal(5d / 6d, snapshot.OverBudget165HzRatio, 10);
    }

    [Fact]
    public void Clear_ResetsWindowAndLifetimeCount()
    {
        var telemetry = new FrameTimeTelemetryWindow(8);
        telemetry.RecordSeconds(1d / 144d);
        telemetry.Record(TimeSpan.FromMilliseconds(9));

        telemetry.Clear();

        var snapshot = telemetry.Capture();
        Assert.Equal(0, telemetry.Count);
        Assert.Equal(0, telemetry.TotalSamples);
        Assert.Equal(8, snapshot.Capacity);
        Assert.Equal(0, snapshot.SampleCount);
        Assert.Equal(0, snapshot.P99Milliseconds);
    }
}
