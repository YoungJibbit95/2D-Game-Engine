namespace Game.Client.Rendering.Performance;

public static class FrameTimeBudgets
{
    public const double Budget120HzMilliseconds = 1000d / 120d;
    public const double Budget144HzMilliseconds = 1000d / 144d;
    public const double Budget165HzMilliseconds = 1000d / 165d;

    public static double MillisecondsForHz(double refreshRateHz)
    {
        if (!double.IsFinite(refreshRateHz) || refreshRateHz <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(refreshRateHz));
        }

        return 1000d / refreshRateHz;
    }
}

public readonly record struct FrameTimeTelemetrySnapshot(
    int SampleCount,
    int Capacity,
    long TotalSamples,
    double AverageMilliseconds,
    double P95Milliseconds,
    double P99Milliseconds,
    double MaximumMilliseconds,
    int OverBudget120HzCount,
    int OverBudget144HzCount,
    int OverBudget165HzCount)
{
    public double OverBudget120HzRatio => Ratio(OverBudget120HzCount);

    public double OverBudget144HzRatio => Ratio(OverBudget144HzCount);

    public double OverBudget165HzRatio => Ratio(OverBudget165HzCount);

    private double Ratio(int count)
    {
        return SampleCount == 0 ? 0 : count / (double)SampleCount;
    }
}

/// <summary>
/// Stores a bounded rolling frame-time window. Recording and snapshot capture reuse fixed arrays.
/// Percentiles use the nearest-rank definition and are intended to be captured on the client thread.
/// </summary>
public sealed class FrameTimeTelemetryWindow
{
    private readonly double[] _samples;
    private readonly double[] _sortedScratch;
    private int _nextIndex;
    private int _count;
    private long _totalSamples;
    private double _totalMilliseconds;
    private int _overBudget120HzCount;
    private int _overBudget144HzCount;
    private int _overBudget165HzCount;

    public FrameTimeTelemetryWindow(int capacity = 512)
    {
        if (capacity < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        _samples = new double[capacity];
        _sortedScratch = new double[capacity];
    }

    public int Capacity => _samples.Length;

    public int Count => _count;

    public long TotalSamples => _totalSamples;

    public void Record(TimeSpan frameTime)
    {
        RecordMilliseconds(frameTime.TotalMilliseconds);
    }

    public void RecordSeconds(double frameTimeSeconds)
    {
        if (!double.IsFinite(frameTimeSeconds) || frameTimeSeconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(frameTimeSeconds));
        }

        RecordMilliseconds(frameTimeSeconds * 1000d);
    }

    public void RecordMilliseconds(double frameTimeMilliseconds)
    {
        if (!double.IsFinite(frameTimeMilliseconds) || frameTimeMilliseconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(frameTimeMilliseconds));
        }

        if (_count == _samples.Length)
        {
            RemoveFromAggregates(_samples[_nextIndex]);
        }
        else
        {
            _count++;
        }

        _samples[_nextIndex] = frameTimeMilliseconds;
        _nextIndex = (_nextIndex + 1) % _samples.Length;
        _totalSamples++;
        AddToAggregates(frameTimeMilliseconds);
    }

    public FrameTimeTelemetrySnapshot Capture()
    {
        if (_count == 0)
        {
            return new FrameTimeTelemetrySnapshot(0, Capacity, _totalSamples, 0, 0, 0, 0, 0, 0, 0);
        }

        Array.Copy(_samples, _sortedScratch, _count);
        Array.Sort(_sortedScratch, 0, _count);
        return new FrameTimeTelemetrySnapshot(
            _count,
            Capacity,
            _totalSamples,
            _totalMilliseconds / _count,
            Percentile(0.95),
            Percentile(0.99),
            _sortedScratch[_count - 1],
            _overBudget120HzCount,
            _overBudget144HzCount,
            _overBudget165HzCount);
    }

    public void Clear()
    {
        Array.Clear(_samples);
        Array.Clear(_sortedScratch);
        _nextIndex = 0;
        _count = 0;
        _totalSamples = 0;
        _totalMilliseconds = 0;
        _overBudget120HzCount = 0;
        _overBudget144HzCount = 0;
        _overBudget165HzCount = 0;
    }

    private void AddToAggregates(double milliseconds)
    {
        _totalMilliseconds += milliseconds;
        if (milliseconds > FrameTimeBudgets.Budget120HzMilliseconds)
        {
            _overBudget120HzCount++;
        }

        if (milliseconds > FrameTimeBudgets.Budget144HzMilliseconds)
        {
            _overBudget144HzCount++;
        }

        if (milliseconds > FrameTimeBudgets.Budget165HzMilliseconds)
        {
            _overBudget165HzCount++;
        }
    }

    private void RemoveFromAggregates(double milliseconds)
    {
        _totalMilliseconds -= milliseconds;
        if (milliseconds > FrameTimeBudgets.Budget120HzMilliseconds)
        {
            _overBudget120HzCount--;
        }

        if (milliseconds > FrameTimeBudgets.Budget144HzMilliseconds)
        {
            _overBudget144HzCount--;
        }

        if (milliseconds > FrameTimeBudgets.Budget165HzMilliseconds)
        {
            _overBudget165HzCount--;
        }
    }

    private double Percentile(double percentile)
    {
        var index = Math.Clamp((int)Math.Ceiling(percentile * _count) - 1, 0, _count - 1);
        return _sortedScratch[index];
    }
}
