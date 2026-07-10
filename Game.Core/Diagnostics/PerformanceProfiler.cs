using System.Diagnostics;

namespace Game.Core.Diagnostics;

public sealed class PerformanceProfiler
{
    private readonly Dictionary<string, MetricAccumulator> _metrics = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _orderedNames = new();
    private readonly double _smoothingFactor;

    public PerformanceProfiler(double smoothingFactor = 0.1)
    {
        if (smoothingFactor is <= 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(smoothingFactor), "Smoothing factor must be in the range (0, 1].");
        }

        _smoothingFactor = smoothingFactor;
    }

    public long FrameIndex { get; private set; }

    public int MetricCount => _metrics.Count;

    public void BeginFrame()
    {
        FrameIndex++;
    }

    public PerformanceMeasureScope Measure(string name, double budgetMilliseconds = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new PerformanceMeasureScope(
            this,
            name,
            budgetMilliseconds,
            Stopwatch.GetTimestamp(),
            GC.GetAllocatedBytesForCurrentThread());
    }

    public void Record(
        string name,
        double elapsedMilliseconds,
        long allocatedBytes = 0,
        double budgetMilliseconds = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (!_metrics.TryGetValue(name, out var metric))
        {
            metric = new MetricAccumulator(name);
            _metrics.Add(name, metric);
            _orderedNames.Add(name);
        }

        metric.Record(
            Math.Max(0, elapsedMilliseconds),
            Math.Max(0, allocatedBytes),
            Math.Max(0, budgetMilliseconds),
            _smoothingFactor);
    }

    public bool TryGetSnapshot(string name, out PerformanceMetricSnapshot snapshot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (_metrics.TryGetValue(name, out var metric))
        {
            snapshot = metric.Snapshot;
            return true;
        }

        snapshot = default;
        return false;
    }

    public IReadOnlyList<PerformanceMetricSnapshot> Snapshot()
    {
        if (_orderedNames.Count == 0)
        {
            return Array.Empty<PerformanceMetricSnapshot>();
        }

        var result = new PerformanceMetricSnapshot[_orderedNames.Count];
        for (var index = 0; index < _orderedNames.Count; index++)
        {
            result[index] = _metrics[_orderedNames[index]].Snapshot;
        }

        return result;
    }

    public IReadOnlyList<PerformanceMetricSnapshot> SnapshotSlowest(int maximumCount)
    {
        if (maximumCount <= 0 || _metrics.Count == 0)
        {
            return Array.Empty<PerformanceMetricSnapshot>();
        }

        return _metrics.Values
            .Select(metric => metric.Snapshot)
            .OrderByDescending(metric => metric.AverageMilliseconds)
            .ThenBy(metric => metric.Name, StringComparer.OrdinalIgnoreCase)
            .Take(maximumCount)
            .ToArray();
    }

    public void ResetPeaks()
    {
        foreach (var metric in _metrics.Values)
        {
            metric.ResetPeak();
        }
    }

    public void Clear()
    {
        _metrics.Clear();
        _orderedNames.Clear();
        FrameIndex = 0;
    }

    internal void EndMeasure(
        string name,
        double budgetMilliseconds,
        long startedAtTimestamp,
        long allocatedBytesAtStart)
    {
        var elapsedTicks = Stopwatch.GetTimestamp() - startedAtTimestamp;
        var elapsedMilliseconds = elapsedTicks * 1000d / Stopwatch.Frequency;
        var allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBytesAtStart;
        Record(name, elapsedMilliseconds, allocatedBytes, budgetMilliseconds);
    }

    private sealed class MetricAccumulator
    {
        private long _sampleCount;
        private double _lastMilliseconds;
        private double _averageMilliseconds;
        private double _peakMilliseconds;
        private long _lastAllocatedBytes;
        private double _averageAllocatedBytes;
        private double _budgetMilliseconds;

        public MetricAccumulator(string name)
        {
            Name = name;
        }

        public string Name { get; }

        public PerformanceMetricSnapshot Snapshot => new(
            Name,
            _sampleCount,
            _lastMilliseconds,
            _averageMilliseconds,
            _peakMilliseconds,
            _lastAllocatedBytes,
            _averageAllocatedBytes,
            _budgetMilliseconds);

        public void Record(double elapsedMilliseconds, long allocatedBytes, double budgetMilliseconds, double smoothingFactor)
        {
            _sampleCount++;
            _lastMilliseconds = elapsedMilliseconds;
            _lastAllocatedBytes = allocatedBytes;
            _budgetMilliseconds = budgetMilliseconds;
            _peakMilliseconds = Math.Max(_peakMilliseconds, elapsedMilliseconds);

            if (_sampleCount == 1)
            {
                _averageMilliseconds = elapsedMilliseconds;
                _averageAllocatedBytes = allocatedBytes;
                return;
            }

            _averageMilliseconds += (elapsedMilliseconds - _averageMilliseconds) * smoothingFactor;
            _averageAllocatedBytes += (allocatedBytes - _averageAllocatedBytes) * smoothingFactor;
        }

        public void ResetPeak()
        {
            _peakMilliseconds = _lastMilliseconds;
        }
    }
}

public readonly struct PerformanceMeasureScope : IDisposable
{
    private readonly PerformanceProfiler? _profiler;
    private readonly string? _name;
    private readonly double _budgetMilliseconds;
    private readonly long _startedAtTimestamp;
    private readonly long _allocatedBytesAtStart;

    internal PerformanceMeasureScope(
        PerformanceProfiler profiler,
        string name,
        double budgetMilliseconds,
        long startedAtTimestamp,
        long allocatedBytesAtStart)
    {
        _profiler = profiler;
        _name = name;
        _budgetMilliseconds = budgetMilliseconds;
        _startedAtTimestamp = startedAtTimestamp;
        _allocatedBytesAtStart = allocatedBytesAtStart;
    }

    public void Dispose()
    {
        if (_profiler is null || _name is null)
        {
            return;
        }

        _profiler.EndMeasure(_name, _budgetMilliseconds, _startedAtTimestamp, _allocatedBytesAtStart);
    }
}
