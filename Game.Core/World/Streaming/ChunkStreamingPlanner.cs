namespace Game.Core.World.Streaming;

public sealed class ChunkStreamingPlanner
{
    public ChunkStreamingPlan Plan(World world, RectI visibleTileArea, ChunkStreamingOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(world);

        var resolvedOptions = options ?? new ChunkStreamingOptions();
        ValidateOptions(resolvedOptions);

        var loaded = world.Chunks.Keys.ToHashSet();
        var required = EnumerateChunksForArea(world, visibleTileArea, resolvedOptions.LoadMarginChunks).ToHashSet();
        var retain = EnumerateChunksForArea(world, visibleTileArea, resolvedOptions.UnloadMarginChunks).ToHashSet();

        var toLoad = required.Where(chunk => !loaded.Contains(chunk)).ToHashSet();
        var toUnload = loaded
            .Where(chunk => !retain.Contains(chunk))
            .Where(chunk => CanUnload(world, chunk, resolvedOptions))
            .ToHashSet();

        return new ChunkStreamingPlan(required, toLoad, toUnload);
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

    private static IEnumerable<ChunkPos> EnumerateChunksForArea(World world, RectI area, int marginChunks)
    {
        if (area.IsEmpty)
        {
            yield break;
        }

        var minTileX = world.IsHorizontallyInfinite ? area.Left : Math.Clamp(area.Left, 0, world.WidthTiles - 1);
        var minTileY = Math.Clamp(area.Top, 0, world.HeightTiles - 1);
        var maxTileX = world.IsHorizontallyInfinite ? area.Right - 1 : Math.Clamp(area.Right - 1, 0, world.WidthTiles - 1);
        var maxTileY = Math.Clamp(area.Bottom - 1, 0, world.HeightTiles - 1);

        var minChunk = CoordinateUtils.TileToChunk(minTileX, minTileY);
        var maxChunk = CoordinateUtils.TileToChunk(maxTileX, maxTileY);
        var worldMaxChunkY = CoordinateUtils.TileToChunk(0, world.HeightTiles - 1).Y;

        var startX = world.IsHorizontallyInfinite ? minChunk.X - marginChunks : Math.Max(0, minChunk.X - marginChunks);
        var startY = Math.Max(0, minChunk.Y - marginChunks);
        var endX = world.IsHorizontallyInfinite
            ? maxChunk.X + marginChunks
            : Math.Min(CoordinateUtils.TileToChunk(world.WidthTiles - 1, world.HeightTiles - 1).X, maxChunk.X + marginChunks);
        var endY = Math.Min(worldMaxChunkY, maxChunk.Y + marginChunks);

        for (var y = startY; y <= endY; y++)
        {
            for (var x = startX; x <= endX; x++)
            {
                yield return new ChunkPos(x, y);
            }
        }
    }

    private static bool CanUnload(World world, ChunkPos position, ChunkStreamingOptions options)
    {
        if (!options.KeepDirtyChunksLoaded)
        {
            return true;
        }

        return !world.TryGetChunk(position, out var chunk) || chunk is null || !chunk.IsDirty;
    }

    private static void ValidateOptions(ChunkStreamingOptions options)
    {
        if (options.LoadMarginChunks < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Load margin must not be negative.");
        }

        if (options.UnloadMarginChunks < options.LoadMarginChunks)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Unload margin must be greater than or equal to load margin.");
        }
    }
}
