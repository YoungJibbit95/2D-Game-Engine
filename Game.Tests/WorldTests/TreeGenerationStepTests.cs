using Game.Core.World;
using Game.Core.World.Generation;
using Xunit;

namespace Game.Tests.WorldTests;

public sealed class TreeGenerationStepTests
{
    [Fact]
    public void FiniteGeneration_KeepsTreeCentersFarEnoughApartForFullCanopies()
    {
        const int seed = 8173;
        var profile = WorldGenerationProfile.Small with
        {
            WidthTiles = 256,
            HeightTiles = 96,
            SurfaceBaseY = 48,
            SurfaceAmplitude = 0,
            TreeAttempts = 512,
            TreeAttemptChance = 1f
        };
        var generator = new AdvancedWorldGenerator(
            new IWorldGenerationStep[] { new TerrainGenerationStep(), new TreeGenerationStep() },
            value => new FastNoiseLiteNoiseService(value));

        var world = generator.Generate(profile, seed);
        var centers = Enumerable.Range(0, world.WidthTiles)
            .Where(x =>
            {
                var topologyX = x - world.WidthTiles / 2;
                var surfaceY = WorldSurfaceSampler.GetSurfaceHeight(profile, seed, topologyX);
                return surfaceY > 2 &&
                    KnownTileIds.IsTreeTrunk(world.GetTile(x, surfaceY - 1).TileId) &&
                    KnownTileIds.IsTreeTrunk(world.GetTile(x, surfaceY - 2).TileId) &&
                    KnownTileIds.IsTreeTrunk(world.GetTile(x, surfaceY - 3).TileId);
            })
            .ToArray();

        Assert.True(centers.Length >= 8);
        for (var index = 1; index < centers.Length; index++)
        {
            Assert.True(
                centers[index] - centers[index - 1] >= TreeGenerationStep.MinimumTreeCenterSpacing,
                $"Tree centers at {centers[index - 1]} and {centers[index]} overlap their canopy bounds.");
        }
    }

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
