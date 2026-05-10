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
        TrySetTile(x, y, tile);
    }

    public bool TrySetTile(int x, int y, ushort tileId)
    {
        return TrySetTile(x, y, TileInstance.FromTileId(tileId));
    }

    public bool TrySetTile(int x, int y, TileInstance tile)
    {
        EnsureInBounds(x, y);

        var chunk = GetOrCreateChunk(CoordinateUtils.TileToChunk(x, y));
        var local = CoordinateUtils.LocalTileInChunk(x, y);

        if (!chunk.SetTile(local.X, local.Y, tile))
        {
            return false;
        }

        MarkDirtyAround(x, y);
        return true;
    }

    public void RemoveTile(int x, int y)
    {
        SetTile(x, y, TileInstance.Air);
    }

    public bool TryRemoveTile(int x, int y)
    {
        return TrySetTile(x, y, TileInstance.Air);
    }

    public TileEditBatchResult ApplyTileEdits(IEnumerable<TileEdit> edits, int dirtyPaddingTiles = 1)
    {
        ArgumentNullException.ThrowIfNull(edits);

        if (dirtyPaddingTiles < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dirtyPaddingTiles), "Dirty padding must not be negative.");
        }

        var requestedEdits = 0;
        var pending = new Dictionary<TilePos, TileInstance>();
        foreach (var edit in edits)
        {
            requestedEdits++;
            EnsureInBounds(edit.Position.X, edit.Position.Y);
            pending[edit.Position] = edit.Tile;
        }

        if (pending.Count == 0)
        {
            return TileEditBatchResult.Empty;
        }

        var changed = new List<TilePos>(pending.Count);
        var dirtyChunks = new HashSet<ChunkPos>();
        var minX = int.MaxValue;
        var minY = int.MaxValue;
        var maxX = int.MinValue;
        var maxY = int.MinValue;

        foreach (var (position, tile) in pending)
        {
            var chunkPosition = CoordinateUtils.TileToChunk(position);
            var chunk = GetOrCreateChunk(chunkPosition);
            var local = CoordinateUtils.LocalTileInChunk(position);

            if (!chunk.SetTile(local.X, local.Y, tile))
            {
                continue;
            }

            changed.Add(position);
            minX = Math.Min(minX, position.X);
            minY = Math.Min(minY, position.Y);
            maxX = Math.Max(maxX, position.X);
            maxY = Math.Max(maxY, position.Y);
            dirtyChunks.Add(chunkPosition);
        }

        if (changed.Count == 0)
        {
            return new TileEditBatchResult(
                requestedEdits,
                0,
                new RectI(0, 0, 0, 0),
                Array.Empty<TilePos>(),
                Array.Empty<ChunkPos>());
        }

        foreach (var position in changed)
        {
            foreach (var dirtyChunk in MarkDirtyRegion(new RectI(position.X, position.Y, 1, 1), dirtyPaddingTiles))
            {
                dirtyChunks.Add(dirtyChunk);
            }
        }

        return new TileEditBatchResult(
            requestedEdits,
            changed.Count,
            RectI.FromInclusiveTileBounds(minX, minY, maxX, maxY),
            changed,
            dirtyChunks.OrderBy(chunk => chunk.Y).ThenBy(chunk => chunk.X).ToArray());
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
        MarkDirtyRegion(new RectI(x, y, 1, 1), paddingTiles: 1);
    }

    public IReadOnlyCollection<ChunkPos> MarkDirtyRegion(RectI region, int paddingTiles = 0)
    {
        if (paddingTiles < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(paddingTiles), "Dirty padding must not be negative.");
        }

        var dirtyRegion = ClampRegionToBounds(region.Inflate(paddingTiles));
        if (dirtyRegion.IsEmpty)
        {
            return Array.Empty<ChunkPos>();
        }

        var marked = new List<ChunkPos>();
        foreach (var chunkPosition in CoordinateUtils.EnumerateChunksOverlapping(dirtyRegion))
        {
            if (!_chunks.TryGetValue(chunkPosition, out var chunk))
            {
                continue;
            }

            chunk.MarkDirty(needsMeshRebuild: true, needsLightUpdate: true);
            marked.Add(chunkPosition);
        }

        return marked;
    }

    public RectI ClampRegionToBounds(RectI region)
    {
        if (region.IsEmpty)
        {
            return new RectI(0, 0, 0, 0);
        }

        var top = Math.Max(0, region.Top);
        var bottom = Math.Min(HeightTiles, region.Bottom);
        if (bottom <= top)
        {
            return new RectI(0, 0, 0, 0);
        }

        var left = IsHorizontallyInfinite ? region.Left : Math.Max(0, region.Left);
        var right = IsHorizontallyInfinite ? region.Right : Math.Min(WidthTiles, region.Right);
        return right <= left
            ? new RectI(0, 0, 0, 0)
            : new RectI(left, top, right - left, bottom - top);
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
