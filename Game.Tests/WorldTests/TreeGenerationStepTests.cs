using Game.Core.World;
using Game.Core.World.Generation;
using Xunit;

namespace Game.Tests.WorldTests;

public sealed class TreeGenerationStepTests
{
    [Fact]
    public void GeneratedTreeTiles_ArePassThroughButMineable()
    {
        var result = new AdvancedWorldGenerator().GenerateDetailed(WorldGenerationProfile.Small with
        {
            WidthTiles = 128,
            HeightTiles = 80,
            TreeAttempts = 80
        }, seed: 42);

        var treeTile = result.World.Chunks.Values
            .SelectMany(chunk => chunk.Tiles)
            .First(tile => tile.TileId is KnownTileIds.Wood or KnownTileIds.Leaves);

        Assert.False(treeTile.IsSolid);
        Assert.False(treeTile.IsAir);
    }
}
