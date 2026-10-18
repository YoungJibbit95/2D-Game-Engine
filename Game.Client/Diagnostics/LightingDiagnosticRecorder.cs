using Game.Core.Diagnostics;

namespace Game.Client.Diagnostics;

public readonly record struct LightingDiagnosticFrame(
    int Frame, long SimulationTick, float CameraX, float CameraY, float Zoom,
    long PrepareExecutions, long UploadCalls, long UploadBytes, long TextureRebuilds,
    int MaskWidth, int MaskHeight, long RaysCast, long OccluderSamples, int PointLightsUsed,
    int ReprojectedPixels, int RejectedPixels, bool HistoryReset,
    float ProbeShadow, float ProbeRed, float ProbeGreen, float ProbeBlue, float ProbeBloom,
    long EstimatedTextureBytes, bool ProbeAvailable = true,
    long AmbientCacheHits = 0, long PortalCacheHits = 0, long CacheLookups = 0,
    long CacheBypasses = 0, long DependencySamples = 0, long CacheCopyBytes = 0,
    long CachePayloadBytes = 0);

public readonly record struct LightingDiagnosticSample(int Frame, double Milliseconds, long AllocatedBytes);
public sealed record LightingDiagnosticScope(string Name, LightingDiagnosticSample[] Samples);
public sealed record LightingDiagnosticCapture(int Version, string GpuTiming,
    LightingDiagnosticFrame[] Frames, LightingDiagnosticScope[] Scopes);

/// <summary>Opt-in bounded raw samples. No per-frame allocation; no EMA-as-mean conversion.</summary>
internal sealed class LightingDiagnosticRecorder
{
    private static readonly string[] Names =
    [
        "Simulation.FixedUpdate", "Frame.Update", "Frame.Draw", "Presentation.LightingPrepare",
        "Presentation.LightEntities", "Presentation.LightTiles", "Presentation.LightingMaskCpu",
        "Presentation.LightingTemporal", "Presentation.LightingHistoryCopy",
        "Presentation.LightingReflectionRadianceCpu", "Presentation.LightingColorEncode",
        "Presentation.LightingUploadHash", "Presentation.LightingGpuUpload", "Render.Lighting"
    ];
    private readonly LightingDiagnosticFrame[] _frames;
    private readonly LightingDiagnosticSample[][] _samples;
    private readonly int[] _counts = new int[Names.Length];
    private readonly long[] _lastCounts = new long[Names.Length];
    private int _count;

    public LightingDiagnosticRecorder(int capacity)
    {
        if (capacity is < 60 or > 4096) { throw new ArgumentOutOfRangeException(nameof(capacity)); }
        _frames = new LightingDiagnosticFrame[capacity];
        _samples = new LightingDiagnosticSample[Names.Length][];
        for (var index = 0; index < Names.Length; index++)
        {
            _samples[index] = new LightingDiagnosticSample[capacity];
        }
    }

    public void Record(in LightingDiagnosticFrame frame, PerformanceProfiler profiler)
    {
        if (_count >= _frames.Length) { throw new InvalidOperationException("Diagnostic frame capacity exceeded."); }
        _frames[_count++] = frame;
        for (var index = 0; index < Names.Length; index++)
        {
            if (!profiler.TryGetSnapshot(Names[index], out var metric) || metric.SampleCount == _lastCounts[index])
            {
                continue;
            }
            if (metric.SampleCount != _lastCounts[index] + 1)
            {
                throw new InvalidOperationException($"Diagnostic scope has missing or multiple samples: {Names[index]}");
            }
            _lastCounts[index] = metric.SampleCount;
            _samples[index][_counts[index]++] = new LightingDiagnosticSample(
                frame.Frame, metric.LastMilliseconds, metric.LastAllocatedBytes);
        }
    }

    public LightingDiagnosticCapture Capture()
    {
        var scopes = new List<LightingDiagnosticScope>(Names.Length);
        for (var index = 0; index < Names.Length; index++)
        {
            if (_counts[index] > 0)
            {
                scopes.Add(new LightingDiagnosticScope(Names[index], _samples[index][.._counts[index]]));
            }
        }
        return new LightingDiagnosticCapture(1, "unknown", _frames[.._count], scopes.ToArray());
    }
}
