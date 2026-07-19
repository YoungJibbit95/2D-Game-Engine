namespace Game.Core.World.Generation;

/// <summary>
/// Bounds-safe, chunk-local tile workspace for initial finite-world materialization.
/// Writes intentionally bypass runtime dirty propagation because generated worlds are
/// published only after generation has completed and dirty flags have been cleared.
/// </summary>
public sealed class WorldGenerationWorkspace
{
    private readonly Chunk?[] _chunks;
    private readonly byte[] _chunkCacheStates;
    private readonly int _chunkColumns;

    public WorldGenerationWorkspace(World world)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (world.IsHorizontallyInfinite)
        {
            throw new ArgumentException(
                "The finite generation workspace cannot materialize a horizontally infinite world.",
                nameof(world));
        }

        World = world;
        _chunkColumns = DivideRoundUp(world.WidthTiles, GameConstants.ChunkSize);
        var chunkRows = DivideRoundUp(world.HeightTiles, GameConstants.ChunkSize);
        var chunkCount = checked(_chunkColumns * chunkRows);
        _chunks = new Chunk?[chunkCount];
        _chunkCacheStates = new byte[chunkCount];
    }

    public World World { get; }

    public long ChangedTiles { get; private set; }

    public bool IsInBounds(int x, int y)
    {
        return (uint)x < (uint)World.WidthTiles && (uint)y < (uint)World.HeightTiles;
    }

    public TileInstance GetTile(int x, int y)
    {
        return TryGetTile(x, y, out var tile) ? tile : TileInstance.Air;
    }

    public bool TryGetTile(int x, int y, out TileInstance tile)
    {
        tile = TileInstance.Air;
        if (!IsInBounds(x, y))
        {
            return false;
        }

        var chunk = ResolveChunk(x, y, create: false);
        if (chunk is null)
        {
            return false;
        }

        tile = chunk.Tiles[ResolveLocalIndex(x, y)];
        return true;
    }

    public bool SetTile(int x, int y, TileInstance tile)
    {
        EnsureInBounds(x, y);
        return SetTileUnchecked(x, y, tile);
    }

    public bool TrySetTile(int x, int y, TileInstance tile)
    {
        return IsInBounds(x, y) && SetTileUnchecked(x, y, tile);
    }

    public bool RemoveTile(int x, int y)
    {
        return SetTile(x, y, TileInstance.Air);
    }

    public bool TryRemoveTile(int x, int y)
    {
        return TrySetTile(x, y, TileInstance.Air);
    }

    private bool SetTileUnchecked(int x, int y, TileInstance tile)
    {
        var chunk = ResolveChunk(x, y, create: true)!;
        var index = ResolveLocalIndex(x, y);
        if (chunk.Tiles[index].Equals(tile))
        {
            return false;
        }

        chunk.Tiles[index] = tile;
        ChangedTiles++;
        return true;
    }

    private Chunk? ResolveChunk(int tileX, int tileY, bool create)
    {
        var chunkX = tileX / GameConstants.ChunkSize;
        var chunkY = tileY / GameConstants.ChunkSize;
        var cacheIndex = chunkY * _chunkColumns + chunkX;
        if (_chunkCacheStates[cacheIndex] != 0)
        {
            var cached = _chunks[cacheIndex];
            if (cached is not null)
            {
                return cached;
            }
        }

        var position = new ChunkPos(chunkX, chunkY);
        if (!World.TryGetChunk(position, out var chunk) && create)
        {
            chunk = World.GetOrCreateChunk(position);
        }

        _chunks[cacheIndex] = chunk;
        _chunkCacheStates[cacheIndex] = 1;
        return chunk;
    }

    private static int ResolveLocalIndex(int tileX, int tileY)
    {
        var localX = tileX % GameConstants.ChunkSize;
        var localY = tileY % GameConstants.ChunkSize;
        return localY * GameConstants.ChunkSize + localX;
    }

    private void EnsureInBounds(int x, int y)
    {
        if (!IsInBounds(x, y))
        {
            throw new ArgumentOutOfRangeException(
                nameof(x),
                $"Tile ({x}, {y}) is outside the finite generation workspace bounds.");
        }
    }

    private static int DivideRoundUp(int value, int divisor)
    {
        return checked(((value - 1) / divisor) + 1);
    }
}
