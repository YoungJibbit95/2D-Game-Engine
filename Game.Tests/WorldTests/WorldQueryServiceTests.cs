using Game.Core.World;
using Game.Core.World.Queries;
using System.Numerics;
using Xunit;

namespace Game.Tests.WorldTests;

public sealed class WorldQueryServiceTests
{
    [Fact]
    public void RaycastTiles_ReturnsFirstSolidTileOnLine()
    {
        var world = CreateWorld();
        world.SetTile(4, 2, KnownTileIds.Stone);
        world.SetTile(6, 2, KnownTileIds.Stone);

        var hit = new WorldQueryService().RaycastTiles(
            world,
            new Vector2(16, 40),
            new Vector2(120, 40));

        Assert.True(hit.Hit);
        Assert.Equal(new TilePos(4, 2), hit.TilePosition);
        Assert.Equal(KnownTileIds.Stone, hit.Tile.TileId);
    }

    [Fact]
    public void HasLineOfSight_ReturnsFalseWhenSolidTileBlocksRay()
    {
        var world = CreateWorld();
        world.SetTile(3, 2, KnownTileIds.Stone);

        var clear = new WorldQueryService().HasLineOfSight(
            world,
            new Vector2(16, 40),
            new Vector2(88, 40));

        Assert.False(clear);
    }

    [Fact]
    public void QueryTiles_ClampsToWorldAndFilters()
    {
        var world = CreateWorld();
        world.SetTile(1, 1, KnownTileIds.Dirt);
        world.SetTile(2, 1, KnownTileIds.Stone);

        var tiles = new WorldQueryService().QueryTiles(
            world,
            new RectI(-4, -4, 10, 10),
            static tile => tile.IsSolid);

        Assert.Equal(2, tiles.Count);
        Assert.Contains(tiles, result => result.Position == new TilePos(1, 1));
        Assert.Contains(tiles, result => result.Position == new TilePos(2, 1));
    }

    private static World CreateWorld()
    {
        return new World(10, 8, WorldMetadata.CreateDefault(seed: 1));
    }
}
