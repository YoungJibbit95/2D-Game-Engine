namespace Game.Core.Diagnostics;

public readonly record struct PerformanceMetricSnapshot(
    string Name,
    long SampleCount,
    double LastMilliseconds,
    double AverageMilliseconds,
    double PeakMilliseconds,
    long LastAllocatedBytes,
    double AverageAllocatedBytes,
    double BudgetMilliseconds)
{
    public bool IsOverBudget => BudgetMilliseconds > 0 && LastMilliseconds > BudgetMilliseconds;

    public double BudgetUsage => BudgetMilliseconds <= 0
        ? 0
        : LastMilliseconds / BudgetMilliseconds;
}
