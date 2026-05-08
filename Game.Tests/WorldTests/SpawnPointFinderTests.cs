using Game.Core.World;
using Game.Core.World.Generation;
using Xunit;

namespace Game.Tests.WorldTests;

public sealed class SpawnPointFinderTests
{
    [Fact]
    public void FindSurfaceSpawn_ReturnsAirAboveFirstValidSurfaceNearCenter()
    {
        var world = new World(16, 16, WorldMetadata.CreateDefault(seed: 1));
        for (var x = 0; x < world.WidthTiles; x++)
        {
            world.SetTile(x, 8, KnownTileIds.Grass);
            for (var y = 9; y < world.HeightTiles; y++)
            {
                world.SetTile(x, y, KnownTileIds.Stone);
            }
        }

        var spawn = new SpawnPointFinder().FindSurfaceSpawn(world);

        Assert.Equal(new TilePos(8, 5), spawn);
        Assert.False(world.IsSolid(spawn.X, spawn.Y));
        Assert.True(world.IsSolid(spawn.X, spawn.Y + 3));
    }

    [Fact]
    public void FindSurfaceSpawn_SkipsBlockedColumns()
    {
        var world = new World(16, 16, WorldMetadata.CreateDefault(seed: 1));
        for (var x = 0; x < world.WidthTiles; x++)
        {
            world.SetTile(x, 8, KnownTileIds.Grass);
            for (var y = 9; y < world.HeightTiles; y++)
            {
                world.SetTile(x, y, KnownTileIds.Stone);
            }
        }

        world.SetTile(8, 6, KnownTileIds.Stone);

        var spawn = new SpawnPointFinder().FindSurfaceSpawn(world, preferredTileX: 8);

        Assert.Equal(9, spawn.X);
        Assert.Equal(5, spawn.Y);
    }
}
