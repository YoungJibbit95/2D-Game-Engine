using Microsoft.Xna.Framework.Graphics;

namespace Game.Client.Rendering.Diagnostics;

public readonly record struct RendererMetricsAvailability(
    bool CountersAvailable,
    string CounterSource,
    string CounterSemantics,
    bool GpuTimeAvailable,
    string? GpuTimeSource,
    string? GpuTimeUnavailableReason)
{
    public static RendererMetricsAvailability MonoGameGraphicsDeviceMetrics { get; } = new(
        true,
        "MonoGame GraphicsDevice.Metrics",
        "CPU-side framework submission counters captured as Draw start/end deltas. DrawCount is the " +
        "number of graphics-device Draw submissions (including SpriteBatch flushes), SpriteCount is " +
        "the number of SpriteBatch sprite/text commands, and TextureCount is the number of GPU texture " +
        "binding changes. MonoGame resets its source counters after each presented frame; a decreasing " +
        "counter is treated as a source reset.",
        false,
        null,
        "MonoGame 3.8.4 GraphicsDevice.Metrics does not expose backend GPU timestamp queries. " +
        "CPU Draw duration is not reported as GPU time.");

    public static RendererMetricsAvailability Unsupported(string counterSource)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(counterSource);
        return new RendererMetricsAvailability(
            false,
            counterSource,
            "Renderer submission counters are unavailable from this source.",
            false,
            null,
            "The selected renderer counter source does not expose backend GPU timestamp queries.");
    }
}

public readonly record struct RendererMetricCounters(
    long ClearCount,
    long DrawCount,
    long PrimitiveCount,
    long SpriteCount,
    long TextureCount,
    long TargetCount,
    long PixelShaderCount,
    long VertexShaderCount)
{
    public static RendererMetricCounters Capture(GraphicsMetrics metrics)
    {
        return new RendererMetricCounters(
            metrics.ClearCount,
            metrics.DrawCount,
            metrics.PrimitiveCount,
            metrics.SpriteCount,
            metrics.TextureCount,
            metrics.TargetCount,
            metrics.PixelShaderCount,
            metrics.VertexShaderCount);
    }

    public RendererMetricCounters DeltaFrom(RendererMetricCounters baseline)
    {
        return new RendererMetricCounters(
            CounterDelta(ClearCount, baseline.ClearCount),
            CounterDelta(DrawCount, baseline.DrawCount),
            CounterDelta(PrimitiveCount, baseline.PrimitiveCount),
            CounterDelta(SpriteCount, baseline.SpriteCount),
            CounterDelta(TextureCount, baseline.TextureCount),
            CounterDelta(TargetCount, baseline.TargetCount),
            CounterDelta(PixelShaderCount, baseline.PixelShaderCount),
            CounterDelta(VertexShaderCount, baseline.VertexShaderCount));
    }

    internal static RendererMetricCounters Add(
        RendererMetricCounters left,
        RendererMetricCounters right)
    {
        return new RendererMetricCounters(
            left.ClearCount + right.ClearCount,
            left.DrawCount + right.DrawCount,
            left.PrimitiveCount + right.PrimitiveCount,
            left.SpriteCount + right.SpriteCount,
            left.TextureCount + right.TextureCount,
            left.TargetCount + right.TargetCount,
            left.PixelShaderCount + right.PixelShaderCount,
            left.VertexShaderCount + right.VertexShaderCount);
    }

    internal static RendererMetricCounters Subtract(
        RendererMetricCounters left,
        RendererMetricCounters right)
    {
        return new RendererMetricCounters(
            left.ClearCount - right.ClearCount,
            left.DrawCount - right.DrawCount,
            left.PrimitiveCount - right.PrimitiveCount,
            left.SpriteCount - right.SpriteCount,
            left.TextureCount - right.TextureCount,
            left.TargetCount - right.TargetCount,
            left.PixelShaderCount - right.PixelShaderCount,
            left.VertexShaderCount - right.VertexShaderCount);
    }

    internal static RendererMetricCounters Max(
        RendererMetricCounters left,
        RendererMetricCounters right)
    {
        return new RendererMetricCounters(
            Math.Max(left.ClearCount, right.ClearCount),
            Math.Max(left.DrawCount, right.DrawCount),
            Math.Max(left.PrimitiveCount, right.PrimitiveCount),
            Math.Max(left.SpriteCount, right.SpriteCount),
            Math.Max(left.TextureCount, right.TextureCount),
            Math.Max(left.TargetCount, right.TargetCount),
            Math.Max(left.PixelShaderCount, right.PixelShaderCount),
            Math.Max(left.VertexShaderCount, right.VertexShaderCount));
    }

    private static long CounterDelta(long current, long baseline)
    {
        return current >= baseline ? current - baseline : current;
    }
}

public readonly record struct RendererMetricAverages(
    double ClearCount,
    double DrawCount,
    double PrimitiveCount,
    double SpriteCount,
    double TextureCount,
    double TargetCount,
    double PixelShaderCount,
    double VertexShaderCount)
{
    internal static RendererMetricAverages FromTotal(RendererMetricCounters total, int sampleCount)
    {
        if (sampleCount == 0)
        {
            return default;
        }

        return new RendererMetricAverages(
            total.ClearCount / (double)sampleCount,
            total.DrawCount / (double)sampleCount,
            total.PrimitiveCount / (double)sampleCount,
            total.SpriteCount / (double)sampleCount,
            total.TextureCount / (double)sampleCount,
            total.TargetCount / (double)sampleCount,
            total.PixelShaderCount / (double)sampleCount,
            total.VertexShaderCount / (double)sampleCount);
    }
}

public readonly record struct RendererMetricsTelemetrySnapshot(
    RendererMetricsAvailability Availability,
    int SampleCount,
    int Capacity,
    long TotalSamples,
    RendererMetricCounters LatestBaseline,
    RendererMetricCounters LatestEnd,
    RendererMetricCounters LatestDelta,
    RendererMetricAverages RollingAveragePerFrame,
    RendererMetricCounters RollingPeakPerFrame)
{
    public static RendererMetricsTelemetrySnapshot NotCaptured { get; } = new(
        RendererMetricsAvailability.Unsupported("not captured"),
        0,
        0,
        0,
        default,
        default,
        default,
        default,
        default);
}

/// <summary>
/// Captures renderer submission-counter deltas in a bounded rolling window.
/// BeginFrame, EndFrame, and Reset do not allocate after construction.
/// </summary>
public sealed class RendererMetricsTelemetryWindow
{
    private readonly RendererMetricsAvailability _availability;
    private readonly RendererMetricCounters[] _samples;
    private RendererMetricCounters _baseline;
    private RendererMetricCounters _latestEnd;
    private RendererMetricCounters _latestDelta;
    private RendererMetricCounters _rollingTotal;
    private RendererMetricCounters _rollingPeak;
    private int _nextIndex;
    private int _count;
    private long _totalSamples;
    private bool _frameOpen;

    public RendererMetricsTelemetryWindow(
        int capacity = 512,
        RendererMetricsAvailability? availability = null)
    {
        if (capacity < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        _availability = availability ?? RendererMetricsAvailability.MonoGameGraphicsDeviceMetrics;
        _samples = new RendererMetricCounters[capacity];
    }

    public int Capacity => _samples.Length;

    public int Count => _count;

    public long TotalSamples => _totalSamples;

    public bool BeginFrame(RendererMetricCounters baseline)
    {
        if (!_availability.CountersAvailable)
        {
            _frameOpen = false;
            return false;
        }

        _baseline = baseline;
        _frameOpen = true;
        return true;
    }

    public bool EndFrame(RendererMetricCounters end)
    {
        if (!_availability.CountersAvailable || !_frameOpen)
        {
            return false;
        }

        _frameOpen = false;
        _latestEnd = end;
        _latestDelta = end.DeltaFrom(_baseline);
        Record(_latestDelta);
        return true;
    }

    public RendererMetricsTelemetrySnapshot Capture()
    {
        return new RendererMetricsTelemetrySnapshot(
            _availability,
            _count,
            Capacity,
            _totalSamples,
            _baseline,
            _latestEnd,
            _latestDelta,
            RendererMetricAverages.FromTotal(_rollingTotal, _count),
            _rollingPeak);
    }

    public void Reset()
    {
        Array.Clear(_samples);
        _baseline = default;
        _latestEnd = default;
        _latestDelta = default;
        _rollingTotal = default;
        _rollingPeak = default;
        _nextIndex = 0;
        _count = 0;
        _totalSamples = 0;
        _frameOpen = false;
    }

    private void Record(RendererMetricCounters sample)
    {
        var peakNeedsRefresh = false;
        if (_count == _samples.Length)
        {
            var removed = _samples[_nextIndex];
            _rollingTotal = RendererMetricCounters.Subtract(_rollingTotal, removed);
            peakNeedsRefresh = RemovedPeakIsNotReplaced(removed, sample, _rollingPeak);
        }
        else
        {
            _count++;
        }

        _samples[_nextIndex] = sample;
        _nextIndex = (_nextIndex + 1) % _samples.Length;
        _totalSamples++;
        _rollingTotal = RendererMetricCounters.Add(_rollingTotal, sample);

        if (peakNeedsRefresh)
        {
            RefreshPeak();
        }
        else
        {
            _rollingPeak = RendererMetricCounters.Max(_rollingPeak, sample);
        }
    }

    private void RefreshPeak()
    {
        var peak = default(RendererMetricCounters);
        for (var index = 0; index < _count; index++)
        {
            peak = RendererMetricCounters.Max(peak, _samples[index]);
        }

        _rollingPeak = peak;
    }

    private static bool RemovedPeakIsNotReplaced(
        RendererMetricCounters removed,
        RendererMetricCounters incoming,
        RendererMetricCounters peak)
    {
        return removed.ClearCount == peak.ClearCount && incoming.ClearCount < peak.ClearCount ||
            removed.DrawCount == peak.DrawCount && incoming.DrawCount < peak.DrawCount ||
            removed.PrimitiveCount == peak.PrimitiveCount && incoming.PrimitiveCount < peak.PrimitiveCount ||
            removed.SpriteCount == peak.SpriteCount && incoming.SpriteCount < peak.SpriteCount ||
            removed.TextureCount == peak.TextureCount && incoming.TextureCount < peak.TextureCount ||
            removed.TargetCount == peak.TargetCount && incoming.TargetCount < peak.TargetCount ||
            removed.PixelShaderCount == peak.PixelShaderCount && incoming.PixelShaderCount < peak.PixelShaderCount ||
            removed.VertexShaderCount == peak.VertexShaderCount && incoming.VertexShaderCount < peak.VertexShaderCount;
    }
}
