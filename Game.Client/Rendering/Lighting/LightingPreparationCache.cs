using Game.Client.Rendering.Effects;
using Game.Core.World;
using Microsoft.Xna.Framework;

namespace Game.Client.Rendering.Lighting;

internal readonly record struct LightingPreparationReuseTelemetry(
    int AmbientHits, int PortalHits, int Lookups, int Bypasses,
    int DependencySamples, long CopiedBytes, long PayloadBytes);

/// <summary>
/// One bounded AO/sky-portal entry, never the final or temporally stabilized field.
/// Mutable chunk arrays cannot be certified by dirty flags or setter versions:
/// compare every packed sample the builder actually reads, including light output.
/// </summary>
internal sealed class LightingPreparationCache
{
    internal const int MaximumDependencyChunks = 128;
    internal const int MaximumMaskPixels = 16_384;
    private readonly float[] _samples;
    private readonly float[] _ambient;
    private readonly float[] _portals;
    private readonly int[] _surfaces;
    private readonly Chunk?[] _chunks = new Chunk?[MaximumDependencyChunks];
    private readonly Chunk?[] _candidateChunks = new Chunk?[MaximumDependencyChunks];
    private World? _world;
    private Rectangle _visibleWorld;
    private PresentationQualityProfile _profile;
    private Rectangle _viewport;
    private Vector2 _camera;
    private float _zoom;
    private long _resourceGeneration;
    private bool _hasContext;
    private bool _hasAmbient;
    private bool _hasPortals;
    private bool _hasSurfaceModel;
    private bool _canStore;
    private int _chunkCount;
    private int _candidateCount;

    public LightingPreparationCache(int pixels, int columns)
    {
        if (pixels is < 1 or > MaximumMaskPixels) { throw new ArgumentOutOfRangeException(nameof(pixels)); }
        if (columns < 1 || columns > pixels) { throw new ArgumentOutOfRangeException(nameof(columns)); }
        _samples = new float[pixels];
        _ambient = new float[pixels];
        _portals = new float[pixels];
        _surfaces = new int[columns];
    }

    public long PayloadBytes => (long)_samples.Length * 12 + (long)_surfaces.Length * 4 +
        MaximumDependencyChunks * 2L * IntPtr.Size;

    public LightingPreparationReuseTelemetry LastTelemetry { get; private set; }

    public void SetContext(Rectangle viewport, Vector2 camera, float zoom, long resourceGeneration)
    {
        if (!_hasContext || _viewport != viewport || _camera != camera || _zoom != zoom ||
            _resourceGeneration != resourceGeneration || !float.IsFinite(zoom) ||
            !float.IsFinite(camera.X) || !float.IsFinite(camera.Y))
        {
            Reset();
            _viewport = viewport;
            _camera = camera;
            _zoom = zoom;
            _resourceGeneration = resourceGeneration;
            _hasContext = true;
        }
    }

    public void Reset()
    {
        _world = null;
        _hasAmbient = false;
        _hasPortals = false;
        _canStore = false;
        _chunkCount = 0;
        _candidateCount = 0;
        Array.Clear(_chunks);
        Array.Clear(_candidateChunks);
        BeginFrame();
    }

    public void BeginFrame() => LastTelemetry = new(0, 0, 0, 0, 0, 0, PayloadBytes);

    public bool TryRestoreAmbient(World world, Rectangle visibleWorld,
        in PresentationQualityProfile profile, ReadOnlySpan<float> samples, Span<float> ambient)
    {
        var count = profile.MaskPixelCount;
        _canStore = profile.MaskSize.X > 0 && profile.MaskSize.Y > 0 &&
            (long)profile.MaskSize.X * profile.MaskSize.Y <= _samples.Length &&
            profile.MaskSize.X <= _surfaces.Length && CaptureChunks(world, visibleWorld, profile.MaskSize);
        if (!_canStore)
        {
            Reset();
            LastTelemetry = LastTelemetry with { Bypasses = 1 };
            return false;
        }

        LastTelemetry = LastTelemetry with { Lookups = 1, DependencySamples = count };
        var matches = _hasAmbient && ReferenceEquals(_world, world) && _visibleWorld == visibleWorld &&
            _profile == profile && _chunkCount == _candidateCount &&
            samples[..count].SequenceEqual(_samples.AsSpan(0, count));
        for (var index = 0; matches && index < _candidateCount; index++)
        {
            matches = ReferenceEquals(_chunks[index], _candidateChunks[index]);
        }
        if (matches)
        {
            _ambient.AsSpan(0, count).CopyTo(ambient);
            LastTelemetry = LastTelemetry with { AmbientHits = 1, CopiedBytes = count * 4L };
            return true;
        }

        _hasAmbient = false;
        _hasPortals = false;
        _world = world;
        _visibleWorld = visibleWorld;
        _profile = profile;
        samples[..count].CopyTo(_samples);
        _candidateChunks.AsSpan(0, _candidateCount).CopyTo(_chunks);
        if (_chunkCount > _candidateCount) { _chunks.AsSpan(_candidateCount, _chunkCount - _candidateCount).Clear(); }
        _chunkCount = _candidateCount;
        LastTelemetry = LastTelemetry with { CopiedBytes = count * 4L };
        return false;
    }

    public void StoreAmbient(ReadOnlySpan<float> ambient)
    {
        if (!_canStore) { return; }
        ambient[.._profile.MaskPixelCount].CopyTo(_ambient);
        _hasAmbient = true;
        LastTelemetry = LastTelemetry with { CopiedBytes = LastTelemetry.CopiedBytes + _profile.MaskPixelCount * 4L };
    }

    public bool TryRestorePortals(bool hasSurfaceModel, ReadOnlySpan<int> surfaces, Span<float> portals)
    {
        if (!_canStore || !_hasAmbient || !_hasPortals || _hasSurfaceModel != hasSurfaceModel ||
            hasSurfaceModel && !surfaces.SequenceEqual(_surfaces.AsSpan(0, _profile.MaskSize.X)))
        {
            return false;
        }
        _portals.AsSpan(0, _profile.MaskPixelCount).CopyTo(portals);
        LastTelemetry = LastTelemetry with { PortalHits = 1, CopiedBytes = LastTelemetry.CopiedBytes + _profile.MaskPixelCount * 4L };
        return true;
    }

    public void StorePortals(bool hasSurfaceModel, ReadOnlySpan<int> surfaces, ReadOnlySpan<float> portals)
    {
        if (!_canStore || !_hasAmbient) { return; }
        if (hasSurfaceModel) { surfaces.CopyTo(_surfaces); }
        portals[.._profile.MaskPixelCount].CopyTo(_portals);
        _hasSurfaceModel = hasSurfaceModel;
        _hasPortals = true;
        LastTelemetry = LastTelemetry with
        {
            CopiedBytes = LastTelemetry.CopiedBytes + _profile.MaskPixelCount * 4L + (hasSurfaceModel ? surfaces.Length * 4L : 0)
        };
    }

    private bool CaptureChunks(World world, Rectangle visibleWorld, Point size)
    {
        var first = CoordinateUtils.TileToChunk(
            TileRayCastShadowMaskBuilder.WorldPixelToTile(TileRayCastShadowMaskBuilder.SampleCoordinate(visibleWorld.X, visibleWorld.Width, 0, size.X)),
            TileRayCastShadowMaskBuilder.WorldPixelToTile(TileRayCastShadowMaskBuilder.SampleCoordinate(visibleWorld.Y, visibleWorld.Height, 0, size.Y)));
        var last = CoordinateUtils.TileToChunk(
            TileRayCastShadowMaskBuilder.WorldPixelToTile(TileRayCastShadowMaskBuilder.SampleCoordinate(visibleWorld.X, visibleWorld.Width, size.X - 1, size.X)),
            TileRayCastShadowMaskBuilder.WorldPixelToTile(TileRayCastShadowMaskBuilder.SampleCoordinate(visibleWorld.Y, visibleWorld.Height, size.Y - 1, size.Y)));
        var columns = (long)last.X - first.X + 1;
        var rows = (long)last.Y - first.Y + 1;
        if (columns <= 0 || rows <= 0 || columns > MaximumDependencyChunks || rows > MaximumDependencyChunks ||
            columns * rows > MaximumDependencyChunks)
        {
            return false;
        }
        var count = 0;
        for (var y = first.Y; y <= last.Y; y++)
        {
            for (var x = first.X; x <= last.X; x++)
            {
                world.Chunks.TryGetValue(new ChunkPos(x, y), out var chunk);
                _candidateChunks[count++] = chunk;
            }
        }
        if (_candidateCount > count) { _candidateChunks.AsSpan(count, _candidateCount - count).Clear(); }
        _candidateCount = count;
        return true;
    }
}
