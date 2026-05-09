namespace Game.Core.World;

public sealed class World
{
    private readonly Dictionary<ChunkPos, Chunk> _chunks = new();

    public World(int widthTiles, int heightTiles, WorldMetadata metadata, bool isHorizontallyInfinite = false)
    {
        if (widthTiles <= 0 && !isHorizontallyInfinite)
        {
            throw new ArgumentOutOfRangeException(nameof(widthTiles), "World width must be greater than zero.");
        }

        if (heightTiles <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(heightTiles), "World height must be greater than zero.");
        }

        WidthTiles = isHorizontallyInfinite ? Math.Max(widthTiles, GameConstants.ChunkSize) : widthTiles;
        HeightTiles = heightTiles;
        Metadata = metadata;
        IsHorizontallyInfinite = isHorizontallyInfinite;
    }

    public int WidthTiles { get; }

    public int HeightTiles { get; }

    public bool IsHorizontallyInfinite { get; }

    public IReadOnlyDictionary<ChunkPos, Chunk> Chunks => _chunks;

    public WorldMetadata Metadata { get; private set; }

    public void SetMetadata(WorldMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        Metadata = metadata;
    }

    public IEnumerable<ChunkPos> GetChunkPositions()
    {
        if (IsHorizontallyInfinite)
        {
            return _chunks.Keys.OrderBy(position => position.Y).ThenBy(position => position.X);
        }

        var maxChunkX = CoordinateUtils.TileToChunk(WidthTiles - 1, 0).X;
        var maxChunkY = CoordinateUtils.TileToChunk(0, HeightTiles - 1).Y;

        return EnumerateFiniteChunkPositions(maxChunkX, maxChunkY);
    }

    public TileInstance GetTile(int x, int y)
    {
        if (!IsInBounds(x, y))
        {
            return TileInstance.Air;
        }

        var chunkPosition = CoordinateUtils.TileToChunk(x, y);
        if (!_chunks.TryGetValue(chunkPosition, out var chunk))
        {
            return TileInstance.Air;
        }

        var local = CoordinateUtils.LocalTileInChunk(x, y);
        return chunk.GetTile(local.X, local.Y);
    }

    public void SetTile(int x, int y, ushort tileId)
    {
        SetTile(x, y, TileInstance.FromTileId(tileId));
    }

    public void SetTile(int x, int y, TileInstance tile)
    {
        EnsureInBounds(x, y);

        var chunk = GetOrCreateChunk(CoordinateUtils.TileToChunk(x, y));
        var local = CoordinateUtils.LocalTileInChunk(x, y);

        chunk.SetTile(local.X, local.Y, tile);
        MarkDirtyAround(x, y);
    }

    public void RemoveTile(int x, int y)
    {
        SetTile(x, y, TileInstance.Air);
    }

    public void SetTileLight(int x, int y, byte light)
    {
        if (!IsInBounds(x, y))
        {
            return;
        }

        var chunk = GetOrCreateChunk(CoordinateUtils.TileToChunk(x, y));
        var local = CoordinateUtils.LocalTileInChunk(x, y);
        chunk.SetTileLight(local.X, local.Y, light);
    }

    public bool IsSolid(int x, int y)
    {
        return IsInBounds(x, y) && GetTile(x, y).IsSolid;
    }

    public bool IsInBounds(int x, int y)
    {
        return y >= 0 && y < HeightTiles && (IsHorizontallyInfinite || (x >= 0 && x < WidthTiles));
    }

    public void MarkDirtyAround(int x, int y)
    {
        MarkChunkDirtyIfLoaded(CoordinateUtils.TileToChunk(x, y));
        MarkChunkDirtyForTileIfInBounds(x - 1, y);
        MarkChunkDirtyForTileIfInBounds(x + 1, y);
        MarkChunkDirtyForTileIfInBounds(x, y - 1);
        MarkChunkDirtyForTileIfInBounds(x, y + 1);
    }

    public bool TryGetChunk(ChunkPos position, out Chunk? chunk)
    {
        return _chunks.TryGetValue(position, out chunk);
    }

    public Chunk GetOrCreateChunk(ChunkPos position)
    {
        if (_chunks.TryGetValue(position, out var chunk))
        {
            return chunk;
        }

        chunk = new Chunk(position);
        _chunks.Add(position, chunk);
        return chunk;
    }

    public bool UnloadChunk(ChunkPos position, bool requireClean = true)
    {
        if (!_chunks.TryGetValue(position, out var chunk))
        {
            return false;
        }

        if (requireClean && chunk.IsDirty)
        {
            return false;
        }

        return _chunks.Remove(position);
    }

    public void ClearAllDirtyFlags()
    {
        foreach (var chunk in _chunks.Values)
        {
            chunk.ClearDirtyFlags();
        }
    }

    private void MarkChunkDirtyForTileIfInBounds(int x, int y)
    {
        if (!IsInBounds(x, y))
        {
            return;
        }

        MarkChunkDirtyIfLoaded(CoordinateUtils.TileToChunk(x, y));
    }

    private void MarkChunkDirtyIfLoaded(ChunkPos position)
    {
        if (_chunks.TryGetValue(position, out var chunk))
        {
            chunk.MarkDirty(needsMeshRebuild: true, needsLightUpdate: true);
        }
    }

    private void EnsureInBounds(int x, int y)
    {
        if (!IsInBounds(x, y))
        {
            throw new ArgumentOutOfRangeException(nameof(x), $"Tile ({x}, {y}) is outside the world bounds.");
        }
    }

    private static IEnumerable<ChunkPos> EnumerateFiniteChunkPositions(int maxChunkX, int maxChunkY)
    {
        for (var cy = 0; cy <= maxChunkY; cy++)
        {
            for (var cx = 0; cx <= maxChunkX; cx++)
            {
                yield return new ChunkPos(cx, cy);
            }
        }
    }
}
