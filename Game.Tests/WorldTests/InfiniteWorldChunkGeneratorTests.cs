using Game.Core.World;
using Game.Core.World.Generation;
using Xunit;

namespace Game.Tests.WorldTests;

public sealed class InfiniteWorldChunkGeneratorTests
{
    [Fact]
    public void CreateWorld_CreatesHorizontallyInfiniteWorldWithFiniteHeight()
    {
        var profile = WorldGenerationProfile.Small with { HeightTiles = 96 };

        var world = new InfiniteWorldChunkGenerator().CreateWorld(profile, seed: 42);

        Assert.True(world.IsHorizontallyInfinite);
        Assert.Equal(96, world.HeightTiles);
        Assert.True(world.IsInBounds(-100_000, 12));
        Assert.False(world.IsInBounds(0, 96));
    }

    [Fact]
    public void EnsureChunk_GeneratesNegativeChunkDeterministically()
    {
        var profile = WorldGenerationProfile.Small with
        {
            HeightTiles = 96,
            SurfaceBaseY = 34,
            SurfaceAmplitude = 5
        };
        var generator = new InfiniteWorldChunkGenerator();
        var first = generator.CreateWorld(profile, seed: 99);
        var second = generator.CreateWorld(profile, seed: 99);

        generator.EnsureChunk(first, profile, new ChunkPos(-2, 1));
        generator.EnsureChunk(second, profile, new ChunkPos(-2, 1));

        var firstChunk = first.Chunks[new ChunkPos(-2, 1)];
        var secondChunk = second.Chunks[new ChunkPos(-2, 1)];
        Assert.Equal(firstChunk.Tiles.Select(tile => tile.TileId), secondChunk.Tiles.Select(tile => tile.TileId));
        Assert.Contains(firstChunk.Tiles, tile => !tile.IsAir);
    }

    [Fact]
    public void GetDimensionAt_UsesProfileDimensionBands()
    {
        var profile = WorldGenerationProfile.Small with
        {
            HeightTiles = 96,
            Dimensions = new[]
            {
                new WorldDimensionDefinition { Id = "sky", MinTileY = 0, MaxTileYInclusive = 20 },
                new WorldDimensionDefinition { Id = "surface", MinTileY = 21, MaxTileYInclusive = 60 },
                new WorldDimensionDefinition { Id = "deep", MinTileY = 61, MaxTileYInclusive = 95 }
            }
        };

        var generator = new InfiniteWorldChunkGenerator();

        Assert.Equal("sky", generator.GetDimensionAt(profile, 10).Id);
        Assert.Equal("surface", generator.GetDimensionAt(profile, 32).Id);
        Assert.Equal("deep", generator.GetDimensionAt(profile, 90).Id);
    }

    [Fact]
    public void EnsureChunk_GeneratesPassThroughMineableTrees()
    {
        var profile = WorldGenerationProfile.Small with
        {
            WidthTiles = 128,
            HeightTiles = 128,
            SurfaceBaseY = 42,
            SurfaceAmplitude = 2,
            TreeAttempts = 128,
            TreeAttemptChance = 1f,
            TreeMinHeight = 4,
            TreeMaxHeight = 6
        };
        var generator = new InfiniteWorldChunkGenerator();
        var world = generator.CreateWorld(profile, seed: 12);

        for (var cy = 0; cy <= 2; cy++)
        {
            for (var cx = -4; cx <= 4; cx++)
            {
                generator.EnsureChunk(world, profile, new ChunkPos(cx, cy));
            }
        }

        var treeTiles = world.Chunks.Values
            .SelectMany(chunk => chunk.Tiles)
            .Where(tile => tile.TileId is KnownTileIds.Wood or KnownTileIds.Leaves)
            .ToArray();

        Assert.NotEmpty(treeTiles);
        Assert.All(treeTiles, tile => Assert.False(tile.IsSolid));
    }

    [Fact]
    public void EnsureChunk_GeneratesDeterministicUndergroundWaterPockets()
    {
        var profile = WorldGenerationProfile.Small with
        {
            WidthTiles = 128,
            HeightTiles = 128,
            SurfaceBaseY = 30,
            SurfaceAmplitude = 2,
            WaterPocketAttempts = 480,
            WaterMinDepthOffset = 8,
            WaterMinRadiusX = 8,
            WaterMaxRadiusX = 12,
            WaterMinRadiusY = 4,
            WaterMaxRadiusY = 7
        };
        var generator = new InfiniteWorldChunkGenerator();
        var first = generator.CreateWorld(profile, seed: 444);
        var second = generator.CreateWorld(profile, seed: 444);

        for (var cy = 1; cy <= 3; cy++)
        {
            for (var cx = -6; cx <= 6; cx++)
            {
                generator.EnsureChunk(first, profile, new ChunkPos(cx, cy));
                generator.EnsureChunk(second, profile, new ChunkPos(cx, cy));
            }
        }

        Assert.Contains(first.Chunks.Values.SelectMany(chunk => chunk.Tiles), tile => tile.HasLiquid);
        Assert.Equal(
            first.Chunks.OrderBy(pair => pair.Key.Y).ThenBy(pair => pair.Key.X).SelectMany(pair => pair.Value.Tiles.Select(tile => tile.LiquidAmount)),
            second.Chunks.OrderBy(pair => pair.Key.Y).ThenBy(pair => pair.Key.X).SelectMany(pair => pair.Value.Tiles.Select(tile => tile.LiquidAmount)));
    }
}
