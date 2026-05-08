using Game.Core.World;
using Game.Core.World.Generation;
using Xunit;

namespace Game.Tests.WorldTests;

public sealed class AdvancedWorldGeneratorTests
{
    [Fact]
    public void Generate_ProducesSameWorldForSameSeed()
    {
        var generator = new AdvancedWorldGenerator();

        var first = generator.Generate(160, 96, seed: 4242);
        var second = generator.Generate(160, 96, seed: 4242);

        AssertWorldTilesEqual(first, second);
    }

    [Fact]
    public void Generate_CreatesCavesBelowSurface()
    {
        var world = new AdvancedWorldGenerator().Generate(160, 96, seed: 101);

        var undergroundAir = CountTilesMatching(world, (x, y, tile) => y > world.HeightTiles / 2 && tile.IsAir);

        Assert.True(undergroundAir > 50);
    }

    [Fact]
    public void Generate_CreatesCopperAndIronOre()
    {
        var world = new AdvancedWorldGenerator().Generate(192, 128, seed: 202);

        Assert.True(CountTile(world, KnownTileIds.CopperOre) > 0);
        Assert.True(CountTile(world, KnownTileIds.IronOre) > 0);
    }

    [Fact]
    public void Generate_CreatesTreesWithWoodAndLeaves()
    {
        var world = new AdvancedWorldGenerator().Generate(192, 96, seed: 303);

        Assert.True(CountTile(world, KnownTileIds.Wood) > 0);
        Assert.True(CountTile(world, KnownTileIds.Leaves) > 0);
    }

    [Fact]
    public void Generate_RunsStructureGenerationStep()
    {
        var generator = new AdvancedWorldGenerator(
            new IWorldGenerationStep[]
            {
                new BiomeAssignmentStep(),
                new TerrainGenerationStep(),
                new StructureGenerationStep()
            },
            seed => new FastNoiseLiteNoiseService(seed));

        var world = generator.Generate(160, 96, seed: 505);

        Assert.True(CountTile(world, KnownTileIds.Wood) > 0);
    }

    [Fact]
    public void Generate_ClearsDirtyFlagsAfterGeneration()
    {
        var world = new AdvancedWorldGenerator().Generate(96, 64, seed: 7);

        Assert.All(world.Chunks.Values, chunk =>
        {
            Assert.False(chunk.IsDirty);
            Assert.False(chunk.NeedsMeshRebuild);
            Assert.False(chunk.NeedsLightUpdate);
        });
    }

    [Fact]
    public void GenerateDetailed_ReturnsForestBiomeMapAndSpawnPoint()
    {
        var result = new AdvancedWorldGenerator().GenerateDetailed(128, 80, seed: 404);

        Assert.Equal("forest", result.Biomes.GetBiomeAt(0, 0));
        Assert.Equal("forest", result.Biomes.GetBiomeAt(127, 0));
        Assert.Contains(result.Biomes.GetRegions(), region =>
            region.StartTileX == 0 &&
            region.EndTileXInclusive == 127 &&
            region.BiomeId == "forest");
        Assert.Equal(result.SpawnTile, result.World.Metadata.SpawnTile);
        Assert.False(result.World.IsSolid(result.SpawnTile.X, result.SpawnTile.Y));
    }

    private static int CountTile(World world, ushort tileId)
    {
        return CountTilesMatching(world, (_, _, tile) => tile.TileId == tileId);
    }

    private static int CountTilesMatching(World world, Func<int, int, TileInstance, bool> predicate)
    {
        var count = 0;
        for (var y = 0; y < world.HeightTiles; y++)
        {
            for (var x = 0; x < world.WidthTiles; x++)
            {
                if (predicate(x, y, world.GetTile(x, y)))
                {
                    count++;
                }
            }
        }

        return count;
    }

    private static void AssertWorldTilesEqual(World first, World second)
    {
        Assert.Equal(first.WidthTiles, second.WidthTiles);
        Assert.Equal(first.HeightTiles, second.HeightTiles);

        for (var y = 0; y < first.HeightTiles; y++)
        {
            for (var x = 0; x < first.WidthTiles; x++)
            {
                Assert.Equal(first.GetTile(x, y).TileId, second.GetTile(x, y).TileId);
            }
        }
    }
}
