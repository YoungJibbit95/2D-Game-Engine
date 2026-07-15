namespace Game.Core.World;

public sealed class Chunk
{
    public Chunk(ChunkPos position)
    {
        Position = position;
        Tiles = new TileInstance[GameConstants.ChunkSize * GameConstants.ChunkSize];
    }

    public ChunkPos Position { get; }

    public TileInstance[] Tiles { get; }

    public bool IsDirty { get; private set; }

    public bool NeedsMeshRebuild { get; private set; }

    public bool NeedsLightUpdate { get; private set; }

    public ChunkMetadata Metadata { get; private set; } = ChunkMetadata.Empty;

    public TileInstance GetTile(int localX, int localY)
    {
        ValidateLocalPosition(localX, localY);
        return Tiles[ToIndex(localX, localY)];
    }

    public bool SetTile(int localX, int localY, TileInstance tile)
    {
        ValidateLocalPosition(localX, localY);

        var index = ToIndex(localX, localY);
        if (Tiles[index].Equals(tile))
        {
            return false;
        }

        Tiles[index] = tile;
        MarkDirty(needsMeshRebuild: true, needsLightUpdate: true);
        return true;
    }

    public void SetTileLight(int localX, int localY, byte light)
    {
        ValidateLocalPosition(localX, localY);

        var index = ToIndex(localX, localY);
        if (Tiles[index].Light == light)
        {
            return;
        }

        Tiles[index].Light = light;
    }

    public void MarkLightDirty()
    {
        NeedsLightUpdate = true;
    }

    public void LoadTiles(IReadOnlyList<TileInstance> tiles)
    {
        ArgumentNullException.ThrowIfNull(tiles);

        if (tiles.Count != Tiles.Length)
        {
            throw new ArgumentException($"Chunk tile payload must contain exactly {Tiles.Length} tiles.", nameof(tiles));
        }

        for (var index = 0; index < Tiles.Length; index++)
        {
            Tiles[index] = tiles[index];
        }

        ClearDirtyFlags();
        MarkLightDirty();
    }

    public void MarkDirty(bool needsMeshRebuild = true, bool needsLightUpdate = true)
    {
        IsDirty = true;
        NeedsMeshRebuild |= needsMeshRebuild;
        NeedsLightUpdate |= needsLightUpdate;
    }

    public void ClearDirtyFlags()
    {
        IsDirty = false;
        NeedsMeshRebuild = false;
        NeedsLightUpdate = false;
    }

    public void ClearMeshRebuildFlag()
    {
        NeedsMeshRebuild = false;
    }

    public void ClearLightUpdateFlag()
    {
        NeedsLightUpdate = false;
    }

    public void UpdateMetadata(ChunkMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        Metadata = metadata;
    }

    private static int ToIndex(int localX, int localY)
    {
        return localY * GameConstants.ChunkSize + localX;
    }

    private static void ValidateLocalPosition(int localX, int localY)
    {
        if (localX < 0 || localX >= GameConstants.ChunkSize)
        {
            throw new ArgumentOutOfRangeException(nameof(localX), "Local X must be inside the chunk.");
        }

        if (localY < 0 || localY >= GameConstants.ChunkSize)
        {
            throw new ArgumentOutOfRangeException(nameof(localY), "Local Y must be inside the chunk.");
        }
    }
}
