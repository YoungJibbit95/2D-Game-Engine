using Game.Core;
using Game.Core.World;
using Game.Core.World.Generation;
using Game.Core.Biomes;
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

    [Theory]
    [InlineData(-4_097)]
    [InlineData(0)]
    [InlineData(6_113)]
    public void GetSurfaceHeightAt_MatchesGeneratedSurfaceAcrossHorizontalWorld(int tileX)
    {
        var profile = WorldGenerationProfile.Small with
        {
            HeightTiles = 128,
            SurfaceBaseY = 42,
            SurfaceAmplitude = 8,
            TreeAttempts = 0,
            WaterPocketAttempts = 0
        };
        var generator = new InfiniteWorldChunkGenerator();
        const int seed = 73_013;
        var surfaceY = generator.GetSurfaceHeightAt(profile, seed, tileX);
        var chunk = generator.GenerateChunk(
            profile,
            seed,
            CoordinateUtils.TileToChunk(new TilePos(tileX, surfaceY)));
        var local = CoordinateUtils.LocalTileInChunk(new TilePos(tileX, surfaceY));
        var aboveLocal = CoordinateUtils.LocalTileInChunk(new TilePos(tileX, surfaceY - 1));
        var aboveChunk = surfaceY / GameConstants.ChunkSize == (surfaceY - 1) / GameConstants.ChunkSize
            ? chunk
            : generator.GenerateChunk(
                profile,
                seed,
                CoordinateUtils.TileToChunk(new TilePos(tileX, surfaceY - 1)));

        Assert.False(chunk.GetTile(local.X, local.Y).IsAir);
        Assert.True(aboveChunk.GetTile(aboveLocal.X, aboveLocal.Y).IsAir);
        Assert.Equal(surfaceY, generator.GetSurfaceHeightAt(profile, seed, tileX));
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

    [Theory]
    [InlineData(-1_000_000)]
    [InlineData(1_000_000)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public void GenerateChunk_ExtremeHorizontalCoordinatesRemainDeterministicAndDoNotOverflow(int chunkX)
    {
        var profile = WorldGenerationProfile.Small with
        {
            HeightTiles = 96,
            SurfaceBaseY = 32,
            SurfaceAmplitude = 5,
            WaterPocketAttempts = 40,
            TreeAttempts = 80
        };
        var generator = new InfiniteWorldChunkGenerator();

        var first = generator.GenerateChunk(profile, 912_445, new ChunkPos(chunkX, 1));
        var second = generator.GenerateChunk(profile, 912_445, new ChunkPos(chunkX, 1));

        Assert.Equal(new ChunkPos(chunkX, 1), first.Position);
        Assert.Equal(first.Tiles, second.Tiles);
    }

    [Fact]
    public void GenerateChunk_RegionalBiomeProfilesChangeTerrainOnBothSidesOfOrigin()
    {
        var profile = WorldGenerationProfile.Small with
        {
            WidthTiles = 256,
            HeightTiles = 128,
            SurfaceBaseY = 42,
            SurfaceAmplitude = 6,
            TreeAttempts = 0,
            WaterPocketAttempts = 0,
            Ores = Array.Empty<OreGenerationDefinition>()
        };
        var regionalProfile = new RegionalGenerationProfile
        {
            Id = "regional-test",
            RegionWidthTiles = 64,
            BiomeSpanRegions = 1,
            WorldHeightTiles = profile.HeightTiles,
            SurfaceBaseY = profile.SurfaceBaseY,
            CaveRegionAttempts = 0
        };
        var biomes = BiomeRegistry.Create(
        [
            new BiomeDefinition
            {
                Id = "forest",
                DisplayName = "Forest",
                SurfaceTile = "grass",
                UndergroundTile = "dirt",
                Terrain = new BiomeTerrainProfile { ElevationMultiplier = 0.6f }
            },
            new BiomeDefinition
            {
                Id = "rocky",
                DisplayName = "Rocky",
                SurfaceTile = "stone",
                UndergroundTile = "stone",
                Terrain = new BiomeTerrainProfile { ElevationMultiplier = 1.8f }
            }
        ]);
        var planner = new WorldRegionPlanner(818, regionalProfile, biomes);
        var generator = new InfiniteWorldChunkGenerator(regionalPlanner: planner);

        long? forestRegion = null;
        long? rockyRegion = null;
        for (var index = -12L; index <= 12 && (forestRegion is null || rockyRegion is null); index++)
        {
            var region = planner.PlanRegion(index);
            if (region.Biome.Id == "forest")
            {
                forestRegion = index;
            }
            else if (region.Biome.Id == "rocky")
            {
                rockyRegion = index;
            }
        }

        Assert.NotNull(forestRegion);
        Assert.NotNull(rockyRegion);
        var forestChunkX = checked((int)(forestRegion.Value * 2));
        var rockyChunkX = checked((int)(rockyRegion.Value * 2));
        var forestChunks = Enumerable.Range(0, 2)
            .Select(cy => generator.GenerateChunk(profile, 818, new ChunkPos(forestChunkX, cy)))
            .ToArray();
        var rockyChunks = Enumerable.Range(0, 2)
            .Select(cy => generator.GenerateChunk(profile, 818, new ChunkPos(rockyChunkX, cy)))
            .ToArray();

        Assert.Contains(forestChunks.SelectMany(chunk => chunk.Tiles), tile => tile.TileId == KnownTileIds.Grass);
        Assert.DoesNotContain(rockyChunks.SelectMany(chunk => chunk.Tiles), tile => tile.TileId == KnownTileIds.Grass);
        Assert.Contains(rockyChunks.SelectMany(chunk => chunk.Tiles), tile => tile.TileId == KnownTileIds.Stone);
    }

    [Fact]
    public void EnsureChunk_MaterializesDataDrivenStructureAcrossChunkBoundaries()
    {
        var profile = WorldGenerationProfile.Small with
        {
            WidthTiles = 128,
            HeightTiles = 96,
            SurfaceBaseY = 42,
            SurfaceAmplitude = 1,
            TreeAttempts = 0,
            WaterPocketAttempts = 0,
            Ores = Array.Empty<OreGenerationDefinition>()
        };
        var regionalProfile = new RegionalGenerationProfile
        {
            Id = "structure-test",
            RegionWidthTiles = 64,
            BiomeSpanRegions = 1,
            WorldHeightTiles = profile.HeightTiles,
            SurfaceBaseY = profile.SurfaceBaseY,
            CaveRegionAttempts = 0
        };
        var biomes = BiomeRegistry.Create(
        [
            new BiomeDefinition
            {
                Id = "forest",
                DisplayName = "Forest",
                SurfaceTile = "grass",
                UndergroundTile = "dirt"
            }
        ]);
        var rows = new[] { "WWWW", "W__W", "SSSS" };
        var definition = new StructurePlanDefinition
        {
            Id = "test_camp",
            TemplateId = "test_camp_v1",
            Placement = "surface",
            ChancePerRegion = 1f,
            MinSpacingRegions = 0,
            WidthTiles = 4,
            HeightTiles = 3,
            MinTileY = 0,
            MaxTileY = 95,
            AllowedBiomeIds = new[] { "forest" },
            Rows = rows,
            Legend = new Dictionary<string, string>
            {
                ["W"] = "wood",
                ["S"] = "stone",
                ["_"] = "air"
            }
        };
        var planner = new WorldRegionPlanner(7331, regionalProfile, biomes, new[] { definition });
        var structure = Assert.Single(planner.PlanRegion(0).Structures);
        var generator = new InfiniteWorldChunkGenerator(_ => new FlatNoiseService(), planner);
        var world = generator.CreateWorld(profile, 7331);

        var firstChunk = CoordinateUtils.TileToChunk(new TilePos((int)structure.TileX, profile.SurfaceBaseY - 3));
        var lastChunk = CoordinateUtils.TileToChunk(new TilePos((int)structure.TileX + 3, profile.SurfaceBaseY - 1));
        for (var chunkY = firstChunk.Y; chunkY <= lastChunk.Y; chunkY++)
        {
            for (var chunkX = firstChunk.X; chunkX <= lastChunk.X; chunkX++)
            {
                generator.EnsureChunk(world, profile, new ChunkPos(chunkX, chunkY));
            }
        }

        for (var y = 0; y < rows.Length; y++)
        {
            for (var x = 0; x < rows[y].Length; x++)
            {
                var expected = rows[y][x] switch
                {
                    'W' => KnownTileIds.Wood,
                    'S' => KnownTileIds.Stone,
                    _ => KnownTileIds.Air
                };
                Assert.Equal(
                    expected,
                    world.GetTile((int)structure.TileX + x, profile.SurfaceBaseY - 3 + y).TileId);
            }
        }
    }

    private sealed class FlatNoiseService : INoiseService
    {
        public float GetNoise(float x, float y)
        {
            return 0f;
        }
    }
}
