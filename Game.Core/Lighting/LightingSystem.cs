using Game.Core.Tiles;
using Game.Core.World;

namespace Game.Core.Lighting;

public sealed class LightingSystem
{
    private const int DirtyPaddingTiles = 12;
    private const byte AirCell = 0;
    private const byte SolidCell = 1;
    private const byte UnknownCell = 2;

    private readonly List<Chunk> _dirtyChunks = new(16);
    private readonly List<ChunkPos> _processedChunkPositions = new(8);
    private readonly List<LightSource> _tileSources = new(16);
    private readonly HashSet<ChunkPos> _capturedMissingChunks = new();
    private readonly Dictionary<ChunkPos, HashSet<ChunkPos>> _missingChunksByTarget = new();
    private readonly Dictionary<ChunkPos, HashSet<ChunkPos>> _targetsByMissingChunk = new();
    private readonly Dictionary<ChunkPos, Chunk> _knownResidency = new();
    private readonly List<ChunkPos> _addedChunks = new(8);
    private readonly List<ChunkPos> _removedChunks = new(8);
    private readonly DirtyChunkComparer _dirtyChunkComparer = new();
    private World.World? _trackedWorld;
    private byte[] _lightBuffer = Array.Empty<byte>();
    private byte[] _cellBuffer = Array.Empty<byte>();
    private byte[] _visitedBuffer = Array.Empty<byte>();
    private int[] _queuePositions = Array.Empty<int>();
    private int[] _queueDistances = Array.Empty<int>();

    /// <summary>
    /// Exposes the deterministic order used by the most recent dirty-light update
    /// without allocating a per-frame result collection.
    /// </summary>
    public IReadOnlyList<ChunkPos> LastProcessedChunkPositions => _processedChunkPositions;

    public LightingSchedulingTelemetry LastSchedulingTelemetry { get; private set; }

    public void Recalculate(
        World.World world,
        IEnumerable<LightSource> lightSources,
        byte sunlight = 255,
        LightingOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(lightSources);
        options ??= LightingOptions.Default;
        ValidateOptions(options);
        ResetTracking(world);

        var sources = lightSources as IReadOnlyList<LightSource> ?? lightSources.ToArray();
        if (!world.IsHorizontallyInfinite)
        {
            RecalculateRegion(
                world,
                new RectI(0, 0, world.WidthTiles, world.HeightTiles),
                sources,
                sunlight,
                options,
                createMissingTiles: true);
        }
        else
        {
            _dirtyChunks.Clear();
            _dirtyChunks.AddRange(world.Chunks.Values);
            _dirtyChunkComparer.Configure(null);
            _dirtyChunks.Sort(_dirtyChunkComparer);
            foreach (var chunk in _dirtyChunks)
            {
                PrepareTargetDependencies(chunk.Position);
                var area = ClampToWorld(
                    world,
                    CoordinateUtils.ChunkTileBounds(chunk.Position).Inflate(DirtyPaddingTiles));
                RecalculateRegion(
                    world,
                    area,
                    sources,
                    sunlight,
                    options,
                    createMissingTiles: false);
                CommitTargetDependencies(chunk.Position);
            }
        }

        foreach (var chunk in world.Chunks.Values)
        {
            chunk.ClearLightUpdateFlag();
        }

        CaptureCurrentResidency(world);
    }

    public LightingUpdateResult RecalculateDirty(
        World.World world,
        TileRegistry tiles,
        byte sunlight,
        LightingOptions? options = null,
        int maxChunks = 4)
    {
        return RecalculateDirtyCore(world, tiles, sunlight, null, options, maxChunks);
    }

    /// <summary>
    /// Recalculates a bounded number of dirty chunks, prioritizing chunks that
    /// intersect the visible tile area and then their distance to its center.
    /// </summary>
    public LightingUpdateResult RecalculateDirty(
        World.World world,
        TileRegistry tiles,
        byte sunlight,
        RectI visibleTileArea,
        LightingOptions? options = null,
        int maxChunks = 4)
    {
        return RecalculateDirtyCore(world, tiles, sunlight, visibleTileArea, options, maxChunks);
    }

    public static SkyExposureState EvaluateSkyExposure(World.World world, int tileX, int tileY)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (!world.IsInBounds(tileX, tileY))
        {
            throw new ArgumentOutOfRangeException(nameof(tileY), $"Tile ({tileX}, {tileY}) is outside world bounds.");
        }

        var unknown = false;
        for (var y = 0; y < tileY; y++)
        {
            if (!world.TryGetTile(tileX, y, out var tile))
            {
                unknown = true;
                continue;
            }

            if (tile.IsSolid)
            {
                return SkyExposureState.Occluded;
            }
        }

        return unknown ? SkyExposureState.Unknown : SkyExposureState.Open;
    }

    public static byte ResolveSunlight(double normalizedTimeOfDay)
    {
        if (!double.IsFinite(normalizedTimeOfDay))
        {
            return 56;
        }

        var radiance = SolarRadianceModel.Evaluate(normalizedTimeOfDay);
        var daylight = Math.Clamp(
            (radiance.DiffuseIrradiance - SolarRadianceModel.NightDiffuseFloor) /
            (1f - SolarRadianceModel.NightDiffuseFloor),
            0f,
            1f);
        return (byte)Math.Clamp((int)Math.Round(56f + daylight * 199f), 0, 255);
    }

    private LightingUpdateResult RecalculateDirtyCore(
        World.World world,
        TileRegistry tiles,
        byte sunlight,
        RectI? visibleTileArea,
        LightingOptions? options,
        int maxChunks)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(tiles);
        if (maxChunks < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxChunks));
        }

        options ??= LightingOptions.Default;
        ValidateOptions(options);
        EnsureWorldTracking(world);
        if (world.IsHorizontallyInfinite)
        {
            RefreshResidency(world);
        }

        _dirtyChunks.Clear();
        _processedChunkPositions.Clear();
        foreach (var chunk in world.Chunks.Values)
        {
            if (chunk.NeedsLightUpdate)
            {
                _dirtyChunks.Add(chunk);
            }
        }

        if (_dirtyChunks.Count == 0)
        {
            LastSchedulingTelemetry = default;
            return new LightingUpdateResult(0, 0, sunlight);
        }

        _dirtyChunkComparer.Configure(visibleTileArea);
        var processCount = Math.Min(maxChunks, _dirtyChunks.Count);
        OrderDirtyPrefix(processCount);
        var updatedTiles = 0;
        var unknownSkyChunks = 0;
        var visibleChunksUpdated = 0;
        for (var index = 0; index < processCount; index++)
        {
            var chunk = _dirtyChunks[index];
            if (world.IsHorizontallyInfinite)
            {
                PrepareTargetDependencies(chunk.Position);
            }

            var area = ClampToWorld(
                world,
                CoordinateUtils.ChunkTileBounds(chunk.Position).Inflate(DirtyPaddingTiles));
            CollectTileSources(world, tiles, area, _tileSources);
            var regionResult = RecalculateRegion(
                world,
                area,
                _tileSources,
                sunlight,
                options,
                createMissingTiles: false);
            if (world.IsHorizontallyInfinite)
            {
                CommitTargetDependencies(chunk.Position);
            }

            updatedTiles += regionResult.UpdatedTiles;
            if (regionResult.HasUnknownCells)
            {
                unknownSkyChunks++;
            }

            if (_dirtyChunkComparer.IsVisible(chunk.Position))
            {
                visibleChunksUpdated++;
            }

            chunk.ClearLightUpdateFlag();
            _processedChunkPositions.Add(chunk.Position);
        }

        LastSchedulingTelemetry = new LightingSchedulingTelemetry(
            _dirtyChunks.Count - processCount,
            unknownSkyChunks,
            visibleChunksUpdated);
        return new LightingUpdateResult(processCount, updatedTiles, sunlight);
    }

    private void OrderDirtyPrefix(int count)
    {
        for (var targetIndex = 0; targetIndex < count; targetIndex++)
        {
            var bestIndex = targetIndex;
            for (var candidateIndex = targetIndex + 1; candidateIndex < _dirtyChunks.Count; candidateIndex++)
            {
                if (_dirtyChunkComparer.Compare(_dirtyChunks[candidateIndex], _dirtyChunks[bestIndex]) < 0)
                {
                    bestIndex = candidateIndex;
                }
            }

            if (bestIndex == targetIndex)
            {
                continue;
            }

            (_dirtyChunks[targetIndex], _dirtyChunks[bestIndex]) =
                (_dirtyChunks[bestIndex], _dirtyChunks[targetIndex]);
        }
    }

    private RegionLightingResult RecalculateRegion(
        World.World world,
        RectI area,
        IReadOnlyList<LightSource> lightSources,
        byte sunlight,
        LightingOptions options,
        bool createMissingTiles)
    {
        if (area.IsEmpty)
        {
            return RegionLightingResult.None;
        }

        var bufferLength = checked(area.Width * area.Height);
        EnsureWorkspaceCapacity(bufferLength);
        var lightBuffer = _lightBuffer.AsSpan(0, bufferLength);
        var cellBuffer = _cellBuffer.AsSpan(0, bufferLength);
        lightBuffer.Clear();
        cellBuffer.Clear();
        _capturedMissingChunks.Clear();
        ApplySunlight(
            world,
            area,
            lightBuffer,
            cellBuffer,
            sunlight,
            options,
            treatMissingAsAir: createMissingTiles || !world.IsHorizontallyInfinite);
        PropagateIndirectSkylight(area, lightBuffer, cellBuffer, options);
        for (var sourceIndex = 0; sourceIndex < lightSources.Count; sourceIndex++)
        {
            PropagateLightSource(area, lightBuffer, cellBuffer, lightSources[sourceIndex], options);
        }

        return new RegionLightingResult(
            ApplyToWorld(world, area, lightBuffer, createMissingTiles),
            _capturedMissingChunks.Count > 0);
    }

    private void ApplySunlight(
        World.World world,
        RectI area,
        Span<byte> lightBuffer,
        Span<byte> cellBuffer,
        byte sunlight,
        LightingOptions options,
        bool treatMissingAsAir)
    {
        var unknownLight = Math.Max(options.MinimumAmbientLight, options.UnknownSkyLight);
        for (var localX = 0; localX < area.Width; localX++)
        {
            var x = area.X + localX;
            var light = (int)sunlight;
            var exposure = SkyExposureState.Open;
            for (var y = 0; y < area.Bottom; y++)
            {
                var isLoaded = world.TryGetTile(x, y, out var tile);
                if (!isLoaded && !treatMissingAsAir)
                {
                    if (exposure != SkyExposureState.Occluded || y >= area.Y)
                    {
                        _capturedMissingChunks.Add(CoordinateUtils.TileToChunk(x, y));
                    }

                    if (exposure != SkyExposureState.Occluded)
                    {
                        exposure = SkyExposureState.Unknown;
                        light = Math.Min(light, unknownLight);
                    }

                    if (y >= area.Y)
                    {
                        cellBuffer[(y - area.Y) * area.Width + localX] = UnknownCell;
                    }

                    continue;
                }

                if (!isLoaded)
                {
                    tile = TileInstance.Air;
                }

                if (tile.IsSolid)
                {
                    if (exposure == SkyExposureState.Unknown)
                    {
                        light = Math.Min(light, unknownLight);
                    }

                    light = Math.Max(options.MinimumAmbientLight, light - options.SolidFalloff);
                    exposure = SkyExposureState.Occluded;
                }
                else
                {
                    light = exposure switch
                    {
                        SkyExposureState.Open => sunlight,
                        SkyExposureState.Unknown => unknownLight,
                        _ => Math.Max(options.MinimumAmbientLight, light - options.UndergroundAirFalloff)
                    };
                }

                if (y < area.Y)
                {
                    continue;
                }

                var localY = y - area.Y;
                cellBuffer[localY * area.Width + localX] = tile.IsSolid ? SolidCell : AirCell;
                SetLight(lightBuffer, area.Width, localX, localY, (byte)Math.Clamp(light, 0, 255));
            }
        }
    }

    private static void PropagateIndirectSkylight(
        RectI area,
        Span<byte> lightBuffer,
        ReadOnlySpan<byte> cellBuffer,
        LightingOptions options)
    {
        for (var pass = 0; pass < options.SkylightRelaxationPasses; pass++)
        {
            for (var y = 0; y < area.Height; y++)
            {
                for (var x = 0; x < area.Width; x++)
                {
                    RelaxSkylightFrom(area, lightBuffer, cellBuffer, x, y, x - 1, y, options);
                    RelaxSkylightFrom(area, lightBuffer, cellBuffer, x, y, x, y - 1, options);
                }
            }

            for (var y = area.Height - 1; y >= 0; y--)
            {
                for (var x = area.Width - 1; x >= 0; x--)
                {
                    RelaxSkylightFrom(area, lightBuffer, cellBuffer, x, y, x + 1, y, options);
                    RelaxSkylightFrom(area, lightBuffer, cellBuffer, x, y, x, y + 1, options);
                }
            }
        }
    }

    private static void RelaxSkylightFrom(
        RectI area,
        Span<byte> lightBuffer,
        ReadOnlySpan<byte> cellBuffer,
        int destinationX,
        int destinationY,
        int sourceX,
        int sourceY,
        LightingOptions options)
    {
        if ((uint)sourceX >= (uint)area.Width || (uint)sourceY >= (uint)area.Height)
        {
            return;
        }

        var sourceIndex = sourceY * area.Width + sourceX;
        var destinationIndex = destinationY * area.Width + destinationX;
        if (cellBuffer[sourceIndex] == UnknownCell || cellBuffer[destinationIndex] == UnknownCell)
        {
            return;
        }

        var source = lightBuffer[sourceIndex];
        var falloff = cellBuffer[destinationIndex] == SolidCell
            ? options.IndirectSkylightSolidFalloff
            : options.IndirectSkylightAirFalloff;
        if (source <= falloff)
        {
            return;
        }

        SetLight(lightBuffer, area.Width, destinationX, destinationY, (byte)(source - falloff));
    }

    private void PropagateLightSource(
        RectI area,
        Span<byte> lightBuffer,
        ReadOnlySpan<byte> cellBuffer,
        LightSource source,
        LightingOptions options)
    {
        if (!area.Contains(source.Position.X, source.Position.Y) || source.Intensity == 0 || source.Radius <= 0)
        {
            return;
        }

        var bufferLength = checked(area.Width * area.Height);
        var visited = _visitedBuffer.AsSpan(0, bufferLength);
        visited.Clear();
        var head = 0;
        var tail = 1;
        var sourceIndex = (source.Position.Y - area.Y) * area.Width + source.Position.X - area.X;
        if (cellBuffer[sourceIndex] == UnknownCell)
        {
            return;
        }

        _queuePositions[0] = sourceIndex;
        _queueDistances[0] = 0;
        visited[sourceIndex] = source.Intensity;
        while (head < tail)
        {
            var index = _queuePositions[head];
            var distance = _queueDistances[head++];
            var localX = index % area.Width;
            var localY = index / area.Width;
            var intensity = visited[index];
            SetLight(lightBuffer, area.Width, localX, localY, intensity);

            if (distance >= source.Radius)
            {
                continue;
            }

            TryEnqueueLightNeighbor(
                area, cellBuffer, localX - 1, localY, intensity, distance,
                options, visited, _queuePositions, _queueDistances, ref tail);
            TryEnqueueLightNeighbor(
                area, cellBuffer, localX + 1, localY, intensity, distance,
                options, visited, _queuePositions, _queueDistances, ref tail);
            TryEnqueueLightNeighbor(
                area, cellBuffer, localX, localY - 1, intensity, distance,
                options, visited, _queuePositions, _queueDistances, ref tail);
            TryEnqueueLightNeighbor(
                area, cellBuffer, localX, localY + 1, intensity, distance,
                options, visited, _queuePositions, _queueDistances, ref tail);
        }
    }

    private void EnsureWorkspaceCapacity(int required)
    {
        if (_lightBuffer.Length >= required)
        {
            return;
        }

        var capacity = 256;
        while (capacity < required && capacity <= int.MaxValue / 2)
        {
            capacity *= 2;
        }

        if (capacity < required)
        {
            capacity = required;
        }

        _lightBuffer = new byte[capacity];
        _cellBuffer = new byte[capacity];
        _visitedBuffer = new byte[capacity];
        _queuePositions = new int[capacity];
        _queueDistances = new int[capacity];
    }

    private static void TryEnqueueLightNeighbor(
        RectI area,
        ReadOnlySpan<byte> cellBuffer,
        int localX,
        int localY,
        byte intensity,
        int distance,
        LightingOptions options,
        Span<byte> visited,
        int[] positions,
        int[] distances,
        ref int tail)
    {
        if ((uint)localX >= (uint)area.Width || (uint)localY >= (uint)area.Height)
        {
            return;
        }

        var index = localY * area.Width + localX;
        if (cellBuffer[index] == UnknownCell)
        {
            return;
        }

        var falloff = cellBuffer[index] == SolidCell
            ? options.PointLightSolidFalloff
            : options.PointLightAirFalloff;
        if (intensity <= falloff)
        {
            return;
        }

        var next = (byte)(intensity - falloff);
        if (visited[index] >= next)
        {
            return;
        }

        visited[index] = next;
        if (tail >= positions.Length)
        {
            return;
        }

        positions[tail] = index;
        distances[tail] = distance + 1;
        tail++;
    }

    private static void CollectTileSources(
        World.World world,
        TileRegistry tiles,
        RectI area,
        List<LightSource> sources)
    {
        sources.Clear();
        for (var y = area.Top; y < area.Bottom; y++)
        {
            for (var xValue = (long)area.Left; xValue < area.Right; xValue++)
            {
                var x = (int)xValue;
                if (!world.TryGetTile(x, y, out var tile) || tile.IsAir)
                {
                    continue;
                }

                var definition = tiles.GetByNumericId(tile.TileId);
                if (definition.EmittedLight > 0 && definition.LightRadius > 0)
                {
                    sources.Add(new LightSource(
                        new TilePos(x, y),
                        definition.EmittedLight,
                        definition.LightRadius));
                }
            }
        }
    }

    private static int ApplyToWorld(
        World.World world,
        RectI area,
        ReadOnlySpan<byte> lightBuffer,
        bool createMissingTiles)
    {
        var updated = 0;
        for (var localY = 0; localY < area.Height; localY++)
        {
            var y = area.Y + localY;
            for (var localX = 0; localX < area.Width; localX++)
            {
                var x = area.X + localX;
                TileInstance current;
                if (createMissingTiles)
                {
                    current = world.GetTile(x, y);
                }
                else if (!world.TryGetTile(x, y, out current))
                {
                    continue;
                }

                var light = GetLight(lightBuffer, area.Width, localX, localY);
                if (current.Light == light)
                {
                    continue;
                }

                world.SetTileLight(x, y, light);
                updated++;
            }
        }

        return updated;
    }

    private void EnsureWorldTracking(World.World world)
    {
        if (ReferenceEquals(_trackedWorld, world))
        {
            return;
        }

        ResetTracking(world);
        CaptureCurrentResidency(world);
    }

    private void ResetTracking(World.World world)
    {
        _trackedWorld = world;
        _knownResidency.Clear();
        _missingChunksByTarget.Clear();
        _targetsByMissingChunk.Clear();
        _addedChunks.Clear();
        _removedChunks.Clear();
        _processedChunkPositions.Clear();
        LastSchedulingTelemetry = default;
    }

    private void CaptureCurrentResidency(World.World world)
    {
        _knownResidency.Clear();
        foreach (var (position, chunk) in world.Chunks)
        {
            _knownResidency[position] = chunk;
        }
    }

    private void RefreshResidency(World.World world)
    {
        _addedChunks.Clear();
        _removedChunks.Clear();
        foreach (var (position, knownChunk) in _knownResidency)
        {
            if (!world.TryGetChunk(position, out var currentChunk) ||
                !ReferenceEquals(knownChunk, currentChunk))
            {
                _removedChunks.Add(position);
            }
        }

        foreach (var (position, currentChunk) in world.Chunks)
        {
            if (!_knownResidency.TryGetValue(position, out var knownChunk) ||
                !ReferenceEquals(knownChunk, currentChunk))
            {
                _addedChunks.Add(position);
            }
        }

        _removedChunks.Sort(ChunkPositionComparer.Instance);
        _addedChunks.Sort(ChunkPositionComparer.Instance);
        foreach (var position in _removedChunks)
        {
            RemoveTargetDependencies(position);
            MarkResidencyAffectedChunks(world, position);
        }

        foreach (var position in _addedChunks)
        {
            ResolveMaterializedDependency(world, position);
            MarkResidencyAffectedChunks(world, position);
        }

        if (_removedChunks.Count > 0 || _addedChunks.Count > 0)
        {
            CaptureCurrentResidency(world);
        }
    }

    private static void MarkResidencyAffectedChunks(World.World world, ChunkPos changedPosition)
    {
        foreach (var chunk in world.Chunks.Values)
        {
            var deltaX = (long)chunk.Position.X - changedPosition.X;
            var deltaY = (long)chunk.Position.Y - changedPosition.Y;
            var sameColumnBelow = deltaX == 0 && deltaY >= 0;
            var lightNeighbor = Math.Abs(deltaX) <= 1 && Math.Abs(deltaY) <= 1;
            if (sameColumnBelow || lightNeighbor)
            {
                chunk.MarkLightDirty();
            }
        }
    }

    private void PrepareTargetDependencies(ChunkPos target)
    {
        if (!_missingChunksByTarget.TryGetValue(target, out var missingChunks))
        {
            _missingChunksByTarget[target] = new HashSet<ChunkPos>();
            return;
        }

        foreach (var missingChunk in missingChunks)
        {
            if (!_targetsByMissingChunk.TryGetValue(missingChunk, out var targets))
            {
                continue;
            }

            targets.Remove(target);
            if (targets.Count == 0)
            {
                _targetsByMissingChunk.Remove(missingChunk);
            }
        }

        missingChunks.Clear();
    }

    private void CommitTargetDependencies(ChunkPos target)
    {
        if (!_missingChunksByTarget.TryGetValue(target, out var targetMissingChunks))
        {
            targetMissingChunks = new HashSet<ChunkPos>();
            _missingChunksByTarget[target] = targetMissingChunks;
        }

        foreach (var missingChunk in _capturedMissingChunks)
        {
            targetMissingChunks.Add(missingChunk);
            if (!_targetsByMissingChunk.TryGetValue(missingChunk, out var targets))
            {
                targets = new HashSet<ChunkPos>();
                _targetsByMissingChunk[missingChunk] = targets;
            }

            targets.Add(target);
        }
    }

    private void ResolveMaterializedDependency(World.World world, ChunkPos materialized)
    {
        if (!_targetsByMissingChunk.Remove(materialized, out var targets))
        {
            return;
        }

        foreach (var target in targets)
        {
            if (_missingChunksByTarget.TryGetValue(target, out var missingChunks))
            {
                missingChunks.Remove(materialized);
            }

            if (world.TryGetChunk(target, out var chunk) && chunk is not null)
            {
                chunk.MarkLightDirty();
            }
        }
    }

    private void RemoveTargetDependencies(ChunkPos target)
    {
        if (!_missingChunksByTarget.Remove(target, out var missingChunks))
        {
            return;
        }

        foreach (var missingChunk in missingChunks)
        {
            if (!_targetsByMissingChunk.TryGetValue(missingChunk, out var targets))
            {
                continue;
            }

            targets.Remove(target);
            if (targets.Count == 0)
            {
                _targetsByMissingChunk.Remove(missingChunk);
            }
        }
    }

    private static RectI ClampToWorld(World.World world, RectI area)
    {
        var top = Math.Clamp(area.Top, 0, world.HeightTiles);
        var bottom = Math.Clamp(area.Bottom, 0, world.HeightTiles);
        var left = world.IsHorizontallyInfinite ? area.Left : Math.Clamp(area.Left, 0, world.WidthTiles);
        var right = world.IsHorizontallyInfinite ? area.Right : Math.Clamp(area.Right, 0, world.WidthTiles);
        return right <= left || bottom <= top
            ? new RectI(0, 0, 0, 0)
            : new RectI(left, top, right - left, bottom - top);
    }

    private static byte GetLight(ReadOnlySpan<byte> buffer, int width, int x, int y)
    {
        return buffer[y * width + x];
    }

    private static void SetLight(Span<byte> buffer, int width, int x, int y, byte light)
    {
        var index = y * width + x;
        buffer[index] = Math.Max(buffer[index], light);
    }

    private static void ValidateOptions(LightingOptions options)
    {
        if (options.OpenAirFalloff < 0 ||
            options.UndergroundAirFalloff < 0 ||
            options.SolidFalloff < 0 ||
            options.PointLightAirFalloff < 1 ||
            options.PointLightSolidFalloff < 1 ||
            options.IndirectSkylightAirFalloff < 1 ||
            options.IndirectSkylightSolidFalloff < 1 ||
            options.SkylightRelaxationPasses is < 1 or > 8 ||
            options.UnknownSkyLight < options.MinimumAmbientLight)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Lighting attenuation values are invalid.");
        }
    }

    private readonly record struct RegionLightingResult(int UpdatedTiles, bool HasUnknownCells)
    {
        public static RegionLightingResult None { get; } = new(0, false);
    }

    private sealed class DirtyChunkComparer : IComparer<Chunk>
    {
        private RectI _visibleArea;
        private ChunkPos _centerChunk;
        private bool _hasVisibleArea;

        public void Configure(RectI? visibleArea)
        {
            _hasVisibleArea = visibleArea is { IsEmpty: false };
            _visibleArea = visibleArea ?? default;
            if (!_hasVisibleArea)
            {
                _centerChunk = default;
                return;
            }

            var centerX = (int)Math.Clamp(
                (long)_visibleArea.Left + Math.Max(0L, (long)_visibleArea.Width - 1L) / 2L,
                int.MinValue,
                int.MaxValue);
            var centerY = (int)Math.Clamp(
                (long)_visibleArea.Top + Math.Max(0L, (long)_visibleArea.Height - 1L) / 2L,
                int.MinValue,
                int.MaxValue);
            _centerChunk = CoordinateUtils.TileToChunk(centerX, centerY);
        }

        public int Compare(Chunk? left, Chunk? right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left is null)
            {
                return 1;
            }

            if (right is null)
            {
                return -1;
            }

            if (_hasVisibleArea)
            {
                var leftVisible = IsVisible(left.Position);
                var rightVisible = IsVisible(right.Position);
                if (leftVisible != rightVisible)
                {
                    return leftVisible ? -1 : 1;
                }

                var distance = DistanceSquared(left.Position, _centerChunk)
                    .CompareTo(DistanceSquared(right.Position, _centerChunk));
                if (distance != 0)
                {
                    return distance;
                }
            }

            var vertical = left.Position.Y.CompareTo(right.Position.Y);
            return vertical != 0 ? vertical : left.Position.X.CompareTo(right.Position.X);
        }

        public bool IsVisible(ChunkPos position)
        {
            return _hasVisibleArea && CoordinateUtils.ChunkTileBounds(position).Intersects(_visibleArea);
        }

        private static ulong DistanceSquared(ChunkPos first, ChunkPos second)
        {
            var deltaX = (long)first.X - second.X;
            var deltaY = (long)first.Y - second.Y;
            var x = AbsoluteAsUnsigned(deltaX);
            var y = AbsoluteAsUnsigned(deltaY);
            var squaredX = x > uint.MaxValue ? ulong.MaxValue : x * x;
            var squaredY = y > uint.MaxValue ? ulong.MaxValue : y * y;
            return ulong.MaxValue - squaredX < squaredY ? ulong.MaxValue : squaredX + squaredY;
        }

        private static ulong AbsoluteAsUnsigned(long value)
        {
            return value < 0 ? (ulong)(-(value + 1)) + 1UL : (ulong)value;
        }
    }

    private sealed class ChunkPositionComparer : IComparer<ChunkPos>
    {
        public static ChunkPositionComparer Instance { get; } = new();

        public int Compare(ChunkPos left, ChunkPos right)
        {
            var vertical = left.Y.CompareTo(right.Y);
            return vertical != 0 ? vertical : left.X.CompareTo(right.X);
        }
    }
}

public readonly record struct LightingUpdateResult(int UpdatedChunks, int UpdatedTiles, byte Sunlight)
{
    public static LightingUpdateResult None { get; } = new(0, 0, 0);
}

public readonly record struct LightingSchedulingTelemetry(
    int DeferredChunks,
    int UnknownSkyChunks,
    int VisibleChunksUpdated);
