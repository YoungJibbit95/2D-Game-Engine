using Game.Core;
using Game.Core.World;
using Xunit;

namespace Game.Tests.WorldTests;

public sealed class WorldTests
{
    [Fact]
    public void GetTile_ReturnsAirForUnloadedChunk()
    {
        var world = CreateWorld();

        var tile = world.GetTile(4, 4);

        Assert.True(tile.IsAir);
    }

    [Fact]
    public void SetTile_StoresTileInGlobalTileCoordinates()
    {
        var world = CreateWorld();

        world.SetTile(35, 2, KnownTileIds.Stone);

        Assert.Equal(KnownTileIds.Stone, world.GetTile(35, 2).TileId);
        Assert.True(world.IsSolid(35, 2));
    }

    [Fact]
    public void RemoveTile_ReplacesTileWithAir()
    {
        var world = CreateWorld();
        world.SetTile(10, 10, KnownTileIds.Dirt);

        world.RemoveTile(10, 10);

        Assert.True(world.GetTile(10, 10).IsAir);
        Assert.False(world.IsSolid(10, 10));
    }

    [Fact]
    public void SetTile_MarksChangedChunkDirty()
    {
        var world = CreateWorld();

        world.SetTile(3, 3, KnownTileIds.Dirt);
        var chunk = world.Chunks[new ChunkPos(0, 0)];

        Assert.True(chunk.IsDirty);
        Assert.True(chunk.NeedsMeshRebuild);
        Assert.True(chunk.NeedsLightUpdate);
    }

    [Fact]
    public void TrySetTile_DoesNotDirtyUnchangedTile()
    {
        var world = CreateWorld();
        world.SetTile(3, 3, KnownTileIds.Dirt);
        var chunk = world.Chunks[new ChunkPos(0, 0)];
        chunk.ClearDirtyFlags();

        var changed = world.TrySetTile(3, 3, KnownTileIds.Dirt);

        Assert.False(changed);
        Assert.False(chunk.IsDirty);
        Assert.False(chunk.NeedsMeshRebuild);
        Assert.False(chunk.NeedsLightUpdate);
    }

    [Fact]
    public void SetTile_OnChunkEdgeMarksLoadedNeighborChunkDirty()
    {
        var world = CreateWorld();
        var neighbor = world.GetOrCreateChunk(new ChunkPos(1, 0));
        neighbor.ClearDirtyFlags();

        world.SetTile(GameConstants.ChunkSize - 1, 4, KnownTileIds.Dirt);

        Assert.True(neighbor.IsDirty);
        Assert.True(neighbor.NeedsMeshRebuild);
    }

    [Fact]
    public void ApplyTileEdits_CoalescesDuplicatesAndMarksDirtyBoundaryChunks()
    {
        var world = CreateWorld();
        var chunks = new[]
        {
            world.GetOrCreateChunk(new ChunkPos(0, 0)),
            world.GetOrCreateChunk(new ChunkPos(1, 0)),
            world.GetOrCreateChunk(new ChunkPos(0, 1)),
            world.GetOrCreateChunk(new ChunkPos(1, 1))
        };
        foreach (var chunk in chunks)
        {
            chunk.ClearDirtyFlags();
        }

        var result = world.ApplyTileEdits(
            new[]
            {
                TileEdit.Set(GameConstants.ChunkSize - 1, GameConstants.ChunkSize - 1, KnownTileIds.Dirt),
                TileEdit.Set(GameConstants.ChunkSize - 1, GameConstants.ChunkSize - 1, KnownTileIds.Stone)
            },
            dirtyPaddingTiles: 1);

        Assert.True(result.HasChanges);
        Assert.Equal(2, result.RequestedEdits);
        Assert.Equal(1, result.ChangedTiles);
        Assert.Equal(new RectI(GameConstants.ChunkSize - 1, GameConstants.ChunkSize - 1, 1, 1), result.ChangedBounds);
        Assert.Equal(KnownTileIds.Stone, world.GetTile(GameConstants.ChunkSize - 1, GameConstants.ChunkSize - 1).TileId);
        Assert.Equal(new[] { new ChunkPos(0, 0), new ChunkPos(1, 0), new ChunkPos(0, 1), new ChunkPos(1, 1) }, result.DirtyChunks);
        Assert.All(chunks, chunk => Assert.True(chunk.NeedsMeshRebuild));
    }

    [Fact]
    public void ApplyTileEdits_RejectsOutOfBoundsBatchBeforeMutating()
    {
        var world = CreateWorld();

        Assert.Throws<ArgumentOutOfRangeException>(() => world.ApplyTileEdits(
            new[]
            {
                TileEdit.Set(1, 1, KnownTileIds.Dirt),
                TileEdit.Set(-1, 1, KnownTileIds.Stone)
            }));

        Assert.True(world.GetTile(1, 1).IsAir);
    }

    [Fact]
    public void SetTile_RejectsOutOfBoundsChanges()
    {
        var world = CreateWorld();

        Assert.Throws<ArgumentOutOfRangeException>(() => world.SetTile(-1, 0, KnownTileIds.Dirt));
        Assert.Throws<ArgumentOutOfRangeException>(() => world.SetTile(0, 64, KnownTileIds.Dirt));
    }

    [Fact]
    public void HorizontallyInfiniteWorld_AllowsNegativeAndPositiveTileX()
    {
        var world = new World(GameConstants.ChunkSize, 64, WorldMetadata.CreateDefault(seed: 1234), isHorizontallyInfinite: true);

        world.SetTile(-1, 5, KnownTileIds.Dirt);
        world.SetTile(4096, 5, KnownTileIds.Stone);

        Assert.True(world.IsHorizontallyInfinite);
        Assert.Equal(KnownTileIds.Dirt, world.GetTile(-1, 5).TileId);
        Assert.Equal(KnownTileIds.Stone, world.GetTile(4096, 5).TileId);
        Assert.Throws<ArgumentOutOfRangeException>(() => world.SetTile(0, 64, KnownTileIds.Dirt));
    }

    [Fact]
    public void ClampRegionToBounds_PreservesNegativeXForInfiniteWorlds()
    {
        var finite = CreateWorld();
        var infinite = new World(GameConstants.ChunkSize, 64, WorldMetadata.CreateDefault(seed: 1234), isHorizontallyInfinite: true);

        Assert.Equal(new RectI(0, 0, 8, 8), finite.ClampRegionToBounds(new RectI(-8, -4, 16, 12)));
        Assert.Equal(new RectI(-8, 0, 16, 8), infinite.ClampRegionToBounds(new RectI(-8, -4, 16, 12)));
    }

    [Fact]
    public void ClearMeshRebuildFlag_PreservesSaveAndLightDirtyState()
    {
        var chunk = new Chunk(ChunkPos.Zero);
        chunk.MarkDirty(needsMeshRebuild: true, needsLightUpdate: true);

        chunk.ClearMeshRebuildFlag();

        Assert.True(chunk.IsDirty);
        Assert.False(chunk.NeedsMeshRebuild);
        Assert.True(chunk.NeedsLightUpdate);
    }

    private static World CreateWorld()
    {
        return new World(64, 64, WorldMetadata.CreateDefault(seed: 1234));
    }
}
