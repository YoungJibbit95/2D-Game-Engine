namespace Game.Core.World.Streaming;

public sealed class ChunkStreamingPlanner
{
    public ChunkStreamingPlan Plan(World world, RectI visibleTileArea, ChunkStreamingOptions? options = null)
    {
        return CreateRequestSnapshot(world, visibleTileArea, 0, 0, options).ToPlan();
    }

    public ChunkStreamingRequestSnapshot CreateRequestSnapshot(
        World world,
        RectI visibleTileArea,
        long worldSessionGeneration,
        long requestSequence,
        ChunkStreamingOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(world);

        var resolvedOptions = options ?? new ChunkStreamingOptions();
        ValidateOptions(resolvedOptions);

        var required = CreateChunkWindow(world, visibleTileArea, resolvedOptions.LoadMarginChunks);
        var retain = CreateChunkWindow(world, visibleTileArea, resolvedOptions.UnloadMarginChunks);

        var centerChunk = CoordinateUtils.TileToChunk(
            visibleTileArea.Left + Math.Max(0, visibleTileArea.Width - 1) / 2,
            visibleTileArea.Top + Math.Max(0, visibleTileArea.Height - 1) / 2);
        var toLoad = new List<ChunkPos>(required.Count);
        foreach (var position in required)
        {
            if (!world.TryGetChunk(position, out _))
            {
                toLoad.Add(position);
            }
        }

        SortByDistance(toLoad, centerChunk, descending: false);

        var toUnload = new List<ChunkPos>();
        foreach (var position in world.Chunks.Keys)
        {
            if (!retain.Contains(position) && CanUnload(world, position, resolvedOptions))
            {
                toUnload.Add(position);
            }
        }

        SortByDistance(toUnload, centerChunk, descending: true);

        return new ChunkStreamingRequestSnapshot(
            worldSessionGeneration,
            requestSequence,
            visibleTileArea,
            centerChunk,
            required,
            retain,
            toLoad,
            toUnload);
    }

    public int ApplyUnloadPlan(World world, ChunkStreamingPlan plan)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(plan);

        var unloaded = 0;
        foreach (var chunk in plan.ChunksToUnload)
        {
            if (world.UnloadChunk(chunk))
            {
                unloaded++;
            }
        }

        return unloaded;
    }

    private static ChunkWindowSet CreateChunkWindow(World world, RectI area, int marginChunks)
    {
        if (area.IsEmpty)
        {
            return new ChunkWindowSet(0, 0, -1, -1);
        }

        var minTileX = world.IsHorizontallyInfinite ? area.Left : Math.Clamp(area.Left, 0, world.WidthTiles - 1);
        var minTileY = Math.Clamp(area.Top, 0, world.HeightTiles - 1);
        var maxTileX = world.IsHorizontallyInfinite ? area.Right - 1 : Math.Clamp(area.Right - 1, 0, world.WidthTiles - 1);
        var maxTileY = Math.Clamp(area.Bottom - 1, 0, world.HeightTiles - 1);

        var minChunk = CoordinateUtils.TileToChunk(minTileX, minTileY);
        var maxChunk = CoordinateUtils.TileToChunk(maxTileX, maxTileY);
        var worldMaxChunkY = CoordinateUtils.TileToChunk(0, world.HeightTiles - 1).Y;

        var startX = world.IsHorizontallyInfinite
            ? SaturatingAdd(minChunk.X, -marginChunks)
            : Math.Max(0, SaturatingAdd(minChunk.X, -marginChunks));
        var startY = Math.Max(0, SaturatingAdd(minChunk.Y, -marginChunks));
        var endX = world.IsHorizontallyInfinite
            ? SaturatingAdd(maxChunk.X, marginChunks)
            : Math.Min(CoordinateUtils.TileToChunk(world.WidthTiles - 1, world.HeightTiles - 1).X, SaturatingAdd(maxChunk.X, marginChunks));
        var endY = Math.Min(worldMaxChunkY, SaturatingAdd(maxChunk.Y, marginChunks));

        return new ChunkWindowSet(startX, startY, endX, endY);
    }

    private static bool CanUnload(World world, ChunkPos position, ChunkStreamingOptions options)
    {
        if (!options.KeepDirtyChunksLoaded)
        {
            return true;
        }

        return !world.TryGetChunk(position, out var chunk) || chunk is null || !chunk.IsDirty;
    }

    public static void ValidateOptions(ChunkStreamingOptions options)
    {
        if (options.LoadMarginChunks < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Load margin must not be negative.");
        }

        if (options.UnloadMarginChunks < options.LoadMarginChunks)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Unload margin must be greater than or equal to load margin.");
        }

        if (options.MaxChunkOperationsPerUpdate < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Chunk operation budget must be at least one.");
        }

        if (options.MaxConcurrentLoadJobs < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Concurrent load job limit must be at least one.");
        }

        if (options.MaxConcurrentSaveJobs < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Concurrent save job limit must be at least one.");
        }

        if (options.MaxApplyQueueLength < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Apply queue limit must be at least one.");
        }

        if (options.MaxApplyTimePerUpdate <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Apply time budget must be positive.");
        }

        if (options.MaxApplyDecodedBytesPerUpdate < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Apply decoded-byte budget must be at least one byte.");
        }

        ArgumentNullException.ThrowIfNull(options.RetryPolicy);
        options.RetryPolicy.Validate();
    }

    private static void SortByDistance(List<ChunkPos> positions, ChunkPos center, bool descending)
    {
        for (var index = 1; index < positions.Count; index++)
        {
            var value = positions[index];
            var insertionIndex = index;
            while (insertionIndex > 0 && Compare(positions[insertionIndex - 1], value, center, descending) > 0)
            {
                positions[insertionIndex] = positions[insertionIndex - 1];
                insertionIndex--;
            }

            positions[insertionIndex] = value;
        }
    }

    private static int Compare(ChunkPos first, ChunkPos second, ChunkPos center, bool descending)
    {
        var distanceComparison = DistanceSquared(first, center).CompareTo(DistanceSquared(second, center));
        if (descending)
        {
            distanceComparison = -distanceComparison;
        }

        if (distanceComparison != 0)
        {
            return distanceComparison;
        }

        var yComparison = first.Y.CompareTo(second.Y);
        return yComparison != 0 ? yComparison : first.X.CompareTo(second.X);
    }

    private static ulong DistanceSquared(ChunkPos first, ChunkPos second)
    {
        var x = (long)first.X - second.X;
        var y = (long)first.Y - second.Y;
        var absoluteX = AbsoluteAsUnsigned(x);
        var absoluteY = AbsoluteAsUnsigned(y);
        var squaredX = absoluteX * absoluteX;
        var squaredY = absoluteY * absoluteY;
        return ulong.MaxValue - squaredX < squaredY ? ulong.MaxValue : squaredX + squaredY;
    }

    private static ulong AbsoluteAsUnsigned(long value)
    {
        return value < 0 ? (ulong)(-(value + 1)) + 1 : (ulong)value;
    }

    private static int SaturatingAdd(int value, int offset)
    {
        return (int)Math.Clamp((long)value + offset, int.MinValue, int.MaxValue);
    }
}
