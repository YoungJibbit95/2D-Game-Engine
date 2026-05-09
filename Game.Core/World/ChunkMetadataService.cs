using Game.Core.World.TileEntities;

namespace Game.Core.World;

public sealed class ChunkMetadataService
{
    public int RefreshAll(World world, TileEntityManager? tileEntities = null, long? lastSavedTick = null)
    {
        ArgumentNullException.ThrowIfNull(world);

        var refreshed = 0;
        foreach (var position in world.GetChunkPositions())
        {
            if (RefreshChunk(world, position, tileEntities, lastSavedTick))
            {
                refreshed++;
            }
        }

        return refreshed;
    }

    public bool RefreshChunk(
        World world,
        ChunkPos position,
        TileEntityManager? tileEntities = null,
        long? lastSavedTick = null)
    {
        ArgumentNullException.ThrowIfNull(world);

        if (!world.TryGetChunk(position, out var chunk) || chunk is null)
        {
            return false;
        }

        var bounds = ClampChunkBounds(world, CoordinateUtils.ChunkTileBounds(position));
        var activeLiquids = 0;
        var activeLights = 0;

        for (var y = bounds.Top; y < bounds.Bottom; y++)
        {
            for (var x = bounds.Left; x < bounds.Right; x++)
            {
                var tile = world.GetTile(x, y);
                if (tile.HasLiquid)
                {
                    activeLiquids++;
                }

                if (tile.Light > 0)
                {
                    activeLights++;
                }
            }
        }

        var entityCount = tileEntities?.Query(bounds).Count ?? 0;
        var savedTick = lastSavedTick ?? chunk.Metadata.LastSavedTick;
        chunk.UpdateMetadata(new ChunkMetadata(activeLiquids, activeLights, entityCount, savedTick));
        return true;
    }

    public int RefreshRegions(World world, IEnumerable<RectI> regions, TileEntityManager? tileEntities = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(regions);

        var chunks = new HashSet<ChunkPos>();
        foreach (var region in regions)
        {
            var clamped = ClampRegion(world, region);
            if (clamped.IsEmpty)
            {
                continue;
            }

            var min = CoordinateUtils.TileToChunk(clamped.Left, clamped.Top);
            var max = CoordinateUtils.TileToChunk(clamped.Right - 1, clamped.Bottom - 1);
            for (var cy = min.Y; cy <= max.Y; cy++)
            {
                for (var cx = min.X; cx <= max.X; cx++)
                {
                    chunks.Add(new ChunkPos(cx, cy));
                }
            }
        }

        var refreshed = 0;
        foreach (var chunk in chunks)
        {
            if (RefreshChunk(world, chunk, tileEntities))
            {
                refreshed++;
            }
        }

        return refreshed;
    }

    public void MarkSaved(Chunk chunk, long saveTick)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        chunk.UpdateMetadata(chunk.Metadata with { LastSavedTick = saveTick });
    }

    private static RectI ClampChunkBounds(World world, RectI chunkBounds)
    {
        if (!world.IsHorizontallyInfinite)
        {
            return chunkBounds.ClampTo(new RectI(0, 0, world.WidthTiles, world.HeightTiles));
        }

        var top = Math.Max(chunkBounds.Top, 0);
        var bottom = Math.Min(chunkBounds.Bottom, world.HeightTiles);
        return bottom <= top
            ? new RectI(0, 0, 0, 0)
            : new RectI(chunkBounds.X, top, chunkBounds.Width, bottom - top);
    }

    private static RectI ClampRegion(World world, RectI region)
    {
        if (!world.IsHorizontallyInfinite)
        {
            return region.ClampTo(new RectI(0, 0, world.WidthTiles, world.HeightTiles));
        }

        if (region.IsEmpty)
        {
            return new RectI(0, 0, 0, 0);
        }

        var top = Math.Max(region.Top, 0);
        var bottom = Math.Min(region.Bottom, world.HeightTiles);
        return bottom <= top
            ? new RectI(0, 0, 0, 0)
            : new RectI(region.X, top, region.Width, bottom - top);
    }
}
