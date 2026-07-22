using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core.World;

public sealed class World
{
    private readonly Dictionary<ChunkPos, Chunk> _chunks = new();

    public World(
        int widthTiles,
        int heightTiles,
        WorldMetadata metadata,
        bool isHorizontallyInfinite = false)
    {
        if (widthTiles < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(widthTiles),
                "World width must not be negative.");
        }

        if (!isHorizontallyInfinite && widthTiles == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(widthTiles),
                "A finite world width must be greater than zero.");
        }

        if (heightTiles <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(heightTiles),
                "World height must be greater than zero.");
        }

        ArgumentNullException.ThrowIfNull(metadata);

        IsHorizontallyInfinite = isHorizontallyInfinite;

        // Bei einer unendlichen Welt ist WidthTiles nur die anfängliche
        // beziehungsweise nominelle Breite, keine horizontale Grenze.
        WidthTiles = isHorizontallyInfinite
            ? Math.Max(widthTiles, GameConstants.ChunkSize)
            : widthTiles;

        HeightTiles = heightTiles;
        Metadata = metadata;
    }

    /// <summary>
    /// Bei endlichen Welten ist dies die tatsächliche Breite.
    /// Bei horizontal unendlichen Welten ist es nur die anfängliche
    /// beziehungsweise nominelle Breite.
    /// </summary>
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

    /// <summary>
    /// Gibt bei einer endlichen Welt alle möglichen Chunkpositionen zurück.
    /// Bei einer unendlichen Welt werden nur aktuell geladene Chunks
    /// zurückgegeben.
    /// </summary>
    public IEnumerable<ChunkPos> GetChunkPositions()
    {
        if (IsHorizontallyInfinite)
        {
            // Snapshot erstellen, damit eine spätere Änderung an _chunks
            // die laufende Enumeration nicht ungültig macht.
            return _chunks.Keys
                .OrderBy(position => position.Y)
                .ThenBy(position => position.X)
                .ToArray();
        }

        var maxChunkX = (WidthTiles - 1) / GameConstants.ChunkSize;
        var maxChunkY = (HeightTiles - 1) / GameConstants.ChunkSize;

        return EnumerateFiniteChunkPositions(maxChunkX, maxChunkY);
    }

    /// <summary>
    /// Gibt ein Tile zurück. Nicht geladene oder ungültige Positionen
    /// werden aus Kompatibilitätsgründen als Luft behandelt.
    /// Verwende TryGetTile, wenn zwischen Luft und ungeladenen Chunks
    /// unterschieden werden muss.
    /// </summary>
    public TileInstance GetTile(int x, int y)
    {
        return TryGetTile(x, y, out var tile)
            ? tile
            : TileInstance.Air;
    }

    /// <summary>
    /// Gibt false zurück, wenn die Position außerhalb der Welt liegt
    /// oder der entsprechende Chunk noch nicht geladen ist.
    /// </summary>
    public bool TryGetTile(int x, int y, out TileInstance tile)
    {
        tile = TileInstance.Air;

        if (!IsInBounds(x, y))
        {
            return false;
        }

        var chunkPosition = CoordinateUtils.TileToChunk(x, y);

        if (!_chunks.TryGetValue(chunkPosition, out var chunk))
        {
            return false;
        }

        var localPosition = CoordinateUtils.LocalTileInChunk(x, y);

        tile = chunk.GetTile(
            localPosition.X,
            localPosition.Y);

        return true;
    }

    public void SetTile(int x, int y, ushort tileId)
    {
        SetTile(
            x,
            y,
            TileInstance.FromTileId(tileId));
    }

    public void SetTile(int x, int y, TileInstance tile)
    {
        EnsureInBounds(x, y);
        SetTileCore(x, y, tile);
    }

    public bool TrySetTile(int x, int y, ushort tileId)
    {
        return TrySetTile(
            x,
            y,
            TileInstance.FromTileId(tileId));
    }

    /// <summary>
    /// Gibt false zurück, wenn die Position ungültig ist oder sich das
    /// Tile nicht verändert hat.
    /// </summary>
    public bool TrySetTile(int x, int y, TileInstance tile)
    {
        if (!IsInBounds(x, y))
        {
            return false;
        }

        return SetTileCore(x, y, tile);
    }

    public void RemoveTile(int x, int y)
    {
        SetTile(x, y, TileInstance.Air);
    }

    public bool TryRemoveTile(int x, int y)
    {
        return TrySetTile(x, y, TileInstance.Air);
    }

    public void SetWall(int x, int y, ushort wallId)
    {
        if (!TrySetWall(x, y, wallId))
        {
            EnsureInBounds(x, y);
        }
    }

    public bool TrySetWall(int x, int y, ushort wallId)
    {
        if (!IsInBounds(x, y))
        {
            return false;
        }

        var tile = GetTile(x, y);
        tile.WallId = wallId;
        if (wallId == 0)
        {
            tile.Flags &= ~TileFlags.HasWall;
        }
        else
        {
            tile.Flags |= TileFlags.HasWall;
        }

        return SetTileCore(x, y, tile);
    }

    public TileEditBatchResult ApplyTileEdits(
        IEnumerable<TileEdit> edits,
        int dirtyPaddingTiles = 1)
    {
        ArgumentNullException.ThrowIfNull(edits);

        if (dirtyPaddingTiles < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(dirtyPaddingTiles),
                "Dirty padding must not be negative.");
        }

        var requestedEdits = 0;
        var pending = new Dictionary<TilePos, TileInstance>();

        /*
         * Zuerst alle Edits validieren und deduplizieren.
         * Dadurch wird verhindert, dass nur ein Teil eines ungültigen
         * Batchs angewendet wird.
         */
        foreach (var edit in edits)
        {
            requestedEdits = checked(requestedEdits + 1);

            EnsureInBounds(
                edit.Position.X,
                edit.Position.Y);

            // Der letzte Edit für eine Position gewinnt.
            pending[edit.Position] = edit.Tile;
        }

        if (pending.Count == 0)
        {
            return TileEditBatchResult.Empty;
        }

        var changedPositions = new List<TilePos>(pending.Count);
        var dirtyChunks = new HashSet<ChunkPos>();

        var minX = int.MaxValue;
        var minY = int.MaxValue;
        var maxX = int.MinValue;
        var maxY = int.MinValue;

        foreach (var (position, tile) in pending)
        {
            var chunkPosition =
                CoordinateUtils.TileToChunk(position);

            var chunk = GetOrCreateChunk(chunkPosition);

            var localPosition =
                CoordinateUtils.LocalTileInChunk(position);

            if (!chunk.SetTile(
                    localPosition.X,
                    localPosition.Y,
                    tile))
            {
                continue;
            }

            changedPositions.Add(position);
            dirtyChunks.Add(chunkPosition);

            minX = Math.Min(minX, position.X);
            minY = Math.Min(minY, position.Y);
            maxX = Math.Max(maxX, position.X);
            maxY = Math.Max(maxY, position.Y);
        }

        if (changedPositions.Count == 0)
        {
            return new TileEditBatchResult(
                requestedEdits,
                0,
                new RectI(0, 0, 0, 0),
                Array.Empty<TilePos>(),
                Array.Empty<ChunkPos>());
        }

        foreach (var position in changedPositions)
        {
            var changedRegion = new RectI(
                position.X,
                position.Y,
                1,
                1);

            foreach (var dirtyChunk in MarkDirtyRegion(
                         changedRegion,
                         dirtyPaddingTiles))
            {
                dirtyChunks.Add(dirtyChunk);
            }
        }

        var changedBounds = CreateInclusiveBounds(
            minX,
            minY,
            maxX,
            maxY);

        return new TileEditBatchResult(
            requestedEdits,
            changedPositions.Count,
            changedBounds,
            changedPositions,
            dirtyChunks
                .OrderBy(chunk => chunk.Y)
                .ThenBy(chunk => chunk.X)
                .ToArray());
    }

    public void SetTileLight(int x, int y, byte light)
    {
        if (!IsInBounds(x, y))
        {
            return;
        }

        var chunkPosition =
            CoordinateUtils.TileToChunk(x, y);

        var chunk = GetOrCreateChunk(chunkPosition);

        var localPosition =
            CoordinateUtils.LocalTileInChunk(x, y);

        chunk.SetTileLight(
            localPosition.X,
            localPosition.Y,
            light);
    }

    public bool IsSolid(int x, int y)
    {
        return TryGetTile(x, y, out var tile) &&
               tile.IsSolid;
    }

    /// <summary>
    /// Bei horizontal unendlichen Welten ist jede int-X-Koordinate gültig.
    /// Die vertikale Ausdehnung bleibt immer begrenzt.
    /// </summary>
    public bool IsInBounds(int x, int y)
    {
        if ((uint)y >= (uint)HeightTiles)
        {
            return false;
        }

        return IsHorizontallyInfinite ||
               (uint)x < (uint)WidthTiles;
    }

    /// <summary>
    /// Prüft, ob ein Chunk die gültige Weltfläche überschneidet.
    /// </summary>
    public bool IsChunkInBounds(ChunkPos position)
    {
        var verticalChunkCount =
            ((HeightTiles - 1) / GameConstants.ChunkSize) + 1;

        if ((uint)position.Y >= (uint)verticalChunkCount)
        {
            return false;
        }

        if (IsHorizontallyInfinite)
        {
            return true;
        }

        var horizontalChunkCount =
            ((WidthTiles - 1) / GameConstants.ChunkSize) + 1;

        return (uint)position.X < (uint)horizontalChunkCount;
    }

    public void MarkDirtyAround(int x, int y)
    {
        if (!IsInBounds(x, y))
        {
            return;
        }

        MarkDirtyRegion(
            new RectI(x, y, 1, 1),
            paddingTiles: 1);
    }

    public IReadOnlyCollection<ChunkPos> MarkDirtyRegion(
        RectI region,
        int paddingTiles = 0)
    {
        if (paddingTiles < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(paddingTiles),
                "Dirty padding must not be negative.");
        }

        if (region.IsEmpty)
        {
            return Array.Empty<ChunkPos>();
        }

        var inflatedRegion = paddingTiles == 0
            ? region
            : region.Inflate(paddingTiles);

        var dirtyRegion =
            ClampRegionToBounds(inflatedRegion);

        if (dirtyRegion.IsEmpty)
        {
            return Array.Empty<ChunkPos>();
        }

        var marked = new HashSet<ChunkPos>();

        foreach (var chunkPosition in
                 CoordinateUtils.EnumerateChunksOverlapping(dirtyRegion))
        {
            if (!IsChunkInBounds(chunkPosition))
            {
                continue;
            }

            if (!_chunks.TryGetValue(
                    chunkPosition,
                    out var chunk))
            {
                continue;
            }

            chunk.MarkDirty(
                needsMeshRebuild: true,
                needsLightUpdate: true);

            marked.Add(chunkPosition);
        }

        return marked
            .OrderBy(position => position.Y)
            .ThenBy(position => position.X)
            .ToArray();
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

        int left;
        int right;

        if (IsHorizontallyInfinite)
        {
            left = region.Left;
            right = region.Right;
        }
        else
        {
            left = Math.Max(0, region.Left);
            right = Math.Min(WidthTiles, region.Right);
        }

        if (right <= left)
        {
            return new RectI(0, 0, 0, 0);
        }

        return new RectI(
            left,
            top,
            right - left,
            bottom - top);
    }

    public bool TryGetChunk(
        ChunkPos position,
        out Chunk? chunk)
    {
        if (!IsChunkInBounds(position))
        {
            chunk = null;
            return false;
        }

        return _chunks.TryGetValue(position, out chunk);
    }

    public Chunk GetOrCreateChunk(ChunkPos position)
    {
        EnsureChunkInBounds(position);

        if (_chunks.TryGetValue(
                position,
                out var existingChunk))
        {
            return existingChunk;
        }

        var chunk = new Chunk(position);
        _chunks.Add(position, chunk);

        return chunk;
    }

    public bool UnloadChunk(
        ChunkPos position,
        bool requireClean = true)
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

    private bool SetTileCore(
        int x,
        int y,
        TileInstance tile)
    {
        var chunkPosition =
            CoordinateUtils.TileToChunk(x, y);

        var chunk =
            GetOrCreateChunk(chunkPosition);

        var localPosition =
            CoordinateUtils.LocalTileInChunk(x, y);

        if (!chunk.SetTile(
                localPosition.X,
                localPosition.Y,
                tile))
        {
            return false;
        }

        MarkDirtyAround(x, y);
        return true;
    }

    private void EnsureInBounds(int x, int y)
    {
        if (IsInBounds(x, y))
        {
            return;
        }

        var horizontalDescription = IsHorizontallyInfinite
            ? "unbounded"
            : $"0..{WidthTiles - 1}";

        throw new ArgumentOutOfRangeException(
            nameof(x),
            $"Tile ({x}, {y}) is outside the world bounds. " +
            $"Valid X range: {horizontalDescription}; " +
            $"valid Y range: 0..{HeightTiles - 1}.");
    }

    private void EnsureChunkInBounds(ChunkPos position)
    {
        if (IsChunkInBounds(position))
        {
            return;
        }

        throw new ArgumentOutOfRangeException(
            nameof(position),
            $"Chunk ({position.X}, {position.Y}) is outside " +
            "the valid world chunk bounds.");
    }

    private static RectI CreateInclusiveBounds(
        int minX,
        int minY,
        int maxX,
        int maxY)
    {
        var width = (long)maxX - minX + 1L;
        var height = (long)maxY - minY + 1L;

        if (width <= 0 ||
            height <= 0 ||
            width > int.MaxValue ||
            height > int.MaxValue)
        {
            throw new InvalidOperationException(
                $"Tile edit bounds cannot be represented by RectI: " +
                $"({minX}, {minY}) to ({maxX}, {maxY}).");
        }

        return new RectI(
            minX,
            minY,
            (int)width,
            (int)height);
    }

    private static IEnumerable<ChunkPos>
        EnumerateFiniteChunkPositions(
            int maxChunkX,
            int maxChunkY)
    {
        for (var chunkY = 0;
             chunkY <= maxChunkY;
             chunkY++)
        {
            for (var chunkX = 0;
                 chunkX <= maxChunkX;
                 chunkX++)
            {
                yield return new ChunkPos(
                    chunkX,
                    chunkY);
            }
        }
    }
}
