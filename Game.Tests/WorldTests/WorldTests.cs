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
    public void SetTile_RejectsOutOfBoundsChanges()
    {
        var world = CreateWorld();

        Assert.Throws<ArgumentOutOfRangeException>(() => world.SetTile(-1, 0, KnownTileIds.Dirt));
        Assert.Throws<ArgumentOutOfRangeException>(() => world.SetTile(0, 64, KnownTileIds.Dirt));
    }

    private static World CreateWorld()
    {
        return new World(64, 64, WorldMetadata.CreateDefault(seed: 1234));
    }
}
