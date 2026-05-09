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
}
