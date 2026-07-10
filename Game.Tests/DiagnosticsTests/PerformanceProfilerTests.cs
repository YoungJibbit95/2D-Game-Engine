using Game.Core.Diagnostics;
using Xunit;

namespace Game.Tests.DiagnosticsTests;

public sealed class PerformanceProfilerTests
{
    [Fact]
    public void Record_CalculatesRollingAveragePeakBudgetAndAllocations()
    {
        var profiler = new PerformanceProfiler(smoothingFactor: 0.5);

        profiler.Record("World.Update", 2, 40, budgetMilliseconds: 4);
        profiler.Record("World.Update", 6, 80, budgetMilliseconds: 4);

        Assert.True(profiler.TryGetSnapshot("world.update", out var metric));
        Assert.Equal(2, metric.SampleCount);
        Assert.Equal(6, metric.LastMilliseconds);
        Assert.Equal(4, metric.AverageMilliseconds);
        Assert.Equal(6, metric.PeakMilliseconds);
        Assert.Equal(80, metric.LastAllocatedBytes);
        Assert.Equal(60, metric.AverageAllocatedBytes);
        Assert.True(metric.IsOverBudget);
        Assert.Equal(1.5, metric.BudgetUsage);
    }

    [Fact]
    public void Snapshot_PreservesRegistrationOrder_AndSlowestSortsByAverage()
    {
        var profiler = new PerformanceProfiler();
        profiler.Record("Update", 3);
        profiler.Record("World", 8);
        profiler.Record("UI", 1);

        Assert.Equal(new[] { "Update", "World", "UI" }, profiler.Snapshot().Select(metric => metric.Name));
        Assert.Equal(new[] { "World", "Update" }, profiler.SnapshotSlowest(2).Select(metric => metric.Name));
    }

    [Fact]
    public void Measure_RecordsElapsedSample()
    {
        var profiler = new PerformanceProfiler();

        using (profiler.Measure("Measured", budgetMilliseconds: 100))
        {
            _ = Enumerable.Range(0, 32).Sum();
        }

        Assert.True(profiler.TryGetSnapshot("Measured", out var metric));
        Assert.Equal(1, metric.SampleCount);
        Assert.True(metric.LastMilliseconds >= 0);
        Assert.True(metric.LastAllocatedBytes >= 0);
        Assert.False(metric.IsOverBudget);
    }

    [Fact]
    public void ResetPeaksAndClear_ResetRuntimeState()
    {
        var profiler = new PerformanceProfiler();
        profiler.Record("Update", 10);
        profiler.Record("Update", 2);
        profiler.BeginFrame();

        profiler.ResetPeaks();
        Assert.True(profiler.TryGetSnapshot("Update", out var reset));
        Assert.Equal(2, reset.PeakMilliseconds);

        profiler.Clear();
        Assert.Equal(0, profiler.FrameIndex);
        Assert.Equal(0, profiler.MetricCount);
        Assert.Empty(profiler.Snapshot());
    }

    [Fact]
    public void Constructor_RejectsInvalidSmoothingFactor()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PerformanceProfiler(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PerformanceProfiler(1.01));
    }
}
