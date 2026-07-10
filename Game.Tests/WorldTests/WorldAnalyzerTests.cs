using Game.Core.World;
using Game.Core.World.Generation;
using Xunit;

namespace Game.Tests.WorldTests;

public sealed class WorldAnalyzerTests
{
    [Fact]
    public void Analyze_CountsTilesLiquidsAndSurfaceHeights()
    {
        var world = new World(4, 4, WorldMetadata.CreateDefault(seed: 1));
        world.SetTile(0, 2, KnownTileIds.Grass);
        world.SetTile(0, 3, KnownTileIds.Stone);
        world.SetTile(1, 1, KnownTileIds.Grass);
        world.SetTile(1, 2, KnownTileIds.Dirt);
        world.SetTile(1, 3, KnownTileIds.Stone);
        world.SetTile(2, 3, TileInstance.Liquid(255));

        var analysis = new WorldAnalyzer().Analyze(world);

        Assert.Equal(16, analysis.WidthTiles * analysis.HeightTiles);
        Assert.Equal(1, analysis.LiquidTileCount);
        Assert.Equal(5, analysis.SolidTileCount);
        Assert.Equal(1, analysis.MinSurfaceY);
        Assert.Equal(2, analysis.MaxSurfaceY);
        Assert.Equal(2, analysis.TileCounts[KnownTileIds.Grass]);
        Assert.Equal(1, analysis.TileCounts[KnownTileIds.Dirt]);
    }

    [Fact]
    public void Analyze_ReportsCavernLiquidAndWallRegions()
    {
        var world = new World(24, 24, WorldMetadata.CreateDefault(seed: 2));
        for (var x = 0; x < world.WidthTiles; x++)
        {
            for (var y = 5; y < world.HeightTiles; y++)
            {
                world.SetTile(x, y, KnownTileIds.Stone);
            }
        }

        for (var x = 5; x <= 16; x++)
        {
            for (var y = 9; y <= 16; y++)
            {
                world.RemoveTile(x, y);
                var tile = world.GetTile(x, y);
                tile.WallId = 2;
                tile.Flags |= TileFlags.HasWall;
                world.SetTile(x, y, tile);
            }
        }

        for (var x = 8; x <= 9; x++)
        {
            for (var y = 15; y <= 16; y++)
            {
                var liquid = TileInstance.Liquid(255);
                liquid.WallId = 2;
                liquid.Flags |= TileFlags.HasWall;
                world.SetTile(x, y, liquid);
            }
        }

        var analysis = new WorldAnalyzer().Analyze(world);

        Assert.Equal(1, analysis.CavernRegionCount);
        Assert.Equal(92, analysis.CavernTileCount);
        Assert.Equal(1, analysis.LiquidBodyCount);
        Assert.Equal(4, analysis.LargestLiquidBodyTileCount);
        Assert.Equal(4, analysis.CaveLiquidTileCount);
        Assert.Equal(96, analysis.ExposedWallTileCount);
        Assert.Equal(96, analysis.WallCounts[2]);
    }
}
