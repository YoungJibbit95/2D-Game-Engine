using Game.Core.World;
using Game.Core.World.Queries;
using Xunit;

namespace Game.Tests.WorldTests;

public sealed class TileFloodFillServiceTests
{
    [Fact]
    public void FloodFill_ReturnsConnectedMatchingTilesOnly()
    {
        var world = new World(8, 8, WorldMetadata.CreateDefault(seed: 1));
        world.SetTile(1, 1, KnownTileIds.Dirt);
        world.SetTile(2, 1, KnownTileIds.Dirt);
        world.SetTile(3, 1, KnownTileIds.Stone);
        world.SetTile(7, 7, KnownTileIds.Dirt);

        var fill = new TileFloodFillService().FloodFill(
            world,
            new TilePos(1, 1),
            tile => tile.TileId == KnownTileIds.Dirt);

        Assert.Equal(2, fill.Count);
        Assert.Contains(new TilePos(1, 1), fill);
        Assert.Contains(new TilePos(2, 1), fill);
        Assert.DoesNotContain(new TilePos(7, 7), fill);
    }

    [Fact]
    public void FloodFill_RespectsMaxTiles()
    {
        var world = new World(8, 8, WorldMetadata.CreateDefault(seed: 1));
        for (var y = 0; y < 8; y++)
        {
            for (var x = 0; x < 8; x++)
            {
                world.SetTile(x, y, KnownTileIds.Dirt);
            }
        }

        var fill = new TileFloodFillService().FloodFill(
            world,
            TilePos.Zero,
            tile => tile.TileId == KnownTileIds.Dirt,
            maxTiles: 5);

        Assert.Equal(5, fill.Count);
    }
}
