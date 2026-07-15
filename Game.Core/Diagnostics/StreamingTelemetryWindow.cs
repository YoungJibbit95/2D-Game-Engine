using Game.Core.World.Streaming;

namespace Game.Core.Diagnostics;

/// <summary>
/// Keeps a bounded, allocation-free hot-path history of streaming pressure.
/// Snapshot allocation is explicit and intended for diagnostics outside the fixed tick.
/// </summary>
public sealed class StreamingTelemetryWindow
{
    private readonly StreamingTelemetryPoint[] _points;
    private int _nextIndex;
    private int _count;

    public StreamingTelemetryWindow(int capacity = 256)
    {
        if (capacity < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        _points = new StreamingTelemetryPoint[capacity];
    }

    public int Capacity => _points.Length;

    public int Count => _count;

    public void Add(long sequence, ChunkStreamingTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);
        _points[_nextIndex] = StreamingTelemetryPoint.From(sequence, telemetry);
        _nextIndex = (_nextIndex + 1) % _points.Length;
        _count = Math.Min(_count + 1, _points.Length);
    }

    public StreamingTelemetryAggregate CaptureAggregate()
    {
        if (_count == 0)
        {
            return StreamingTelemetryAggregate.Empty;
        }

        var first = GetChronological(0);
        var last = GetChronological(_count - 1);
        var maxPendingLoads = 0;
        var maxPendingSaves = 0;
        var maxApplyQueue = 0;
        var maxDeferredLoads = 0;
        var maxDeferredUnloads = 0;
        long maxQueuedBytes = 0;
        long pressureSamples = 0;
        long queueDepthTotal = 0;
        long deferredTotal = 0;

        for (var index = 0; index < _count; index++)
        {
            var point = GetChronological(index);
            maxPendingLoads = Math.Max(maxPendingLoads, point.PendingLoadJobs);
            maxPendingSaves = Math.Max(maxPendingSaves, point.PendingSaveJobs);
            maxApplyQueue = Math.Max(maxApplyQueue, point.ApplyQueueLength);
            maxDeferredLoads = Math.Max(maxDeferredLoads, point.DeferredLoadRequests);
            maxDeferredUnloads = Math.Max(maxDeferredUnloads, point.DeferredUnloadRequests);
            maxQueuedBytes = Math.Max(maxQueuedBytes, point.QueuedDecodedBytes);
            queueDepthTotal += point.ApplyQueueLength;
            deferredTotal += point.DeferredLoadRequests + point.DeferredUnloadRequests;
            if (point.HasBackpressure)
            {
                pressureSamples++;
            }
        }

        return new StreamingTelemetryAggregate(
            _count,
            first.Sequence,
            last.Sequence,
            maxPendingLoads,
            maxPendingSaves,
            maxApplyQueue,
            maxDeferredLoads,
            maxDeferredUnloads,
            maxQueuedBytes,
            queueDepthTotal / (double)_count,
            deferredTotal / (double)_count,
            pressureSamples,
            NonNegativeDelta(first.LoadOperations, last.LoadOperations),
            NonNegativeDelta(first.GenerateOperations, last.GenerateOperations),
            NonNegativeDelta(first.ApplyOperations, last.ApplyOperations),
            NonNegativeDelta(first.SaveOperations, last.SaveOperations),
            NonNegativeDelta(first.UnloadOperations, last.UnloadOperations),
            NonNegativeDelta(first.CancellationRequests, last.CancellationRequests),
            NonNegativeDelta(first.CancelledJobs, last.CancelledJobs),
            NonNegativeDelta(first.StaleResultsRejected, last.StaleResultsRejected),
            NonNegativeDelta(first.FailedJobs, last.FailedJobs));
    }

    public void Clear()
    {
        Array.Clear(_points);
        _nextIndex = 0;
        _count = 0;
    }

    private StreamingTelemetryPoint GetChronological(int index)
    {
        var start = _count == _points.Length ? _nextIndex : 0;
        return _points[(start + index) % _points.Length];
    }

    private static long NonNegativeDelta(long first, long last)
    {
        return Math.Max(0, last - first);
    }
}

public readonly record struct StreamingTelemetryPoint(
    long Sequence,
    int PendingLoadJobs,
    int PendingSaveJobs,
    int ApplyQueueLength,
    int DeferredLoadRequests,
    int DeferredUnloadRequests,
    long QueuedDecodedBytes,
    long LoadOperations,
    long GenerateOperations,
    long ApplyOperations,
    long SaveOperations,
    long UnloadOperations,
    long CancellationRequests,
    long CancelledJobs,
    long StaleResultsRejected,
    long FailedJobs)
{
    public bool HasBackpressure =>
        ApplyQueueLength > 0 || DeferredLoadRequests > 0 || DeferredUnloadRequests > 0;

    public static StreamingTelemetryPoint From(long sequence, ChunkStreamingTelemetry telemetry)
    {
        return new StreamingTelemetryPoint(
            sequence,
            telemetry.PendingLoadJobs,
            telemetry.PendingSaveJobs,
            telemetry.ApplyQueueLength,
            telemetry.DeferredLoadRequests,
            telemetry.DeferredUnloadRequests,
            telemetry.QueuedDecodedBytes,
            telemetry.LoadOperations,
            telemetry.GenerateOperations,
            telemetry.ApplyOperations,
            telemetry.SaveOperations,
            telemetry.UnloadOperations,
            telemetry.CancellationRequests,
            telemetry.CancelledJobs,
            telemetry.StaleResultsRejected,
            telemetry.FailedJobs);
    }
}

public readonly record struct StreamingTelemetryAggregate(
    int SampleCount,
    long FirstSequence,
    long LastSequence,
    int MaxPendingLoadJobs,
    int MaxPendingSaveJobs,
    int MaxApplyQueueLength,
    int MaxDeferredLoadRequests,
    int MaxDeferredUnloadRequests,
    long MaxQueuedDecodedBytes,
    double AverageApplyQueueLength,
    double AverageDeferredRequests,
    long BackpressureSampleCount,
    long LoadOperations,
    long GenerateOperations,
    long ApplyOperations,
    long SaveOperations,
    long UnloadOperations,
    long CancellationRequests,
    long CancelledJobs,
    long StaleResultsRejected,
    long FailedJobs)
{
    public static StreamingTelemetryAggregate Empty { get; } = new(
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

    public double BackpressureRatio => SampleCount == 0 ? 0 : BackpressureSampleCount / (double)SampleCount;
}
