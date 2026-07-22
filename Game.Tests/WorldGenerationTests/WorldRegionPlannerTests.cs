using Game.Core.Biomes;
using Game.Core.World;
using Game.Core.World.Generation;
using Xunit;

namespace Game.Tests.WorldGenerationTests;

public sealed class WorldRegionPlannerTests
{
    [Theory]
    [InlineData(long.MinValue)]
    [InlineData(int.MinValue)]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(int.MaxValue)]
    [InlineData(long.MaxValue)]
    public void PlanAtTileX_ContainsEveryRepresentableCoordinateAndReplays(long tileX)
    {
        var (profile, biomes, structures) = LoadContracts();
        var first = new WorldRegionPlanner(1337, profile, biomes, structures).PlanAtTileX(tileX);
        var second = new WorldRegionPlanner(1337, profile, biomes, structures).PlanAtTileX(tileX);

        Assert.True(first.ContainsTileX(tileX));
        Assert.Equal(first.RegionIndex, second.RegionIndex);
        Assert.Equal(first.StartTileX, second.StartTileX);
        Assert.Equal(first.EndTileXInclusive, second.EndTileXInclusive);
        Assert.Equal(first.Biome.Id, second.Biome.Id);
        Assert.Equal(first.SubBiome?.Id, second.SubBiome?.Id);
        Assert.Equal(first.Caves, second.Caves);
        Assert.Equal(first.Features, second.Features);
        Assert.Equal(first.Structures, second.Structures);
    }

    [Fact]
    public void ResolveBiome_UsesSurfaceCaveAndDeepLayerProfiles()
    {
        var (profile, biomes, structures) = LoadContracts();
        var planner = new WorldRegionPlanner(4477, profile, biomes, structures);
        var region = planner.PlanRegion(-4);
        var surface = planner.ResolveBiome(region, region.StartTileX, profile.SurfaceBaseY);

        Assert.Contains(surface.Biome.Id, new[] { "amber_grove", "forest", "frostwood", "meadow", "twilight_marsh" });
        Assert.Equal("surface", surface.LayerId);
        Assert.False(surface.IsCave);

        var cave = Assert.Single(region.Caves.Take(1));
        var caveResolution = planner.ResolveBiome(region, cave.CenterTileX, cave.CenterTileY);
        Assert.True(caveResolution.IsCave);
        Assert.Equal(cave.ProfileId, caveResolution.Biome.Id);
        Assert.Contains(
            caveResolution.Biome.Id,
            new[] { "mushroom_cave", "crystal_depths", "deep_cave" });

        var deepX = Enumerable.Range(0, checked((int)(region.EndTileXInclusive - region.StartTileX + 1)))
            .Select(offset => region.StartTileX + offset)
            .First(value => region.Caves.All(cavePlan => !cavePlan.Contains(value, 150)));
        var deep = planner.ResolveBiome(region, deepX, 150);
        Assert.Equal("deep_cave", deep.Biome.Id);
        Assert.Equal("deep_caves", deep.LayerId);
        Assert.False(deep.IsCave);

        var runtime = caveResolution.ToRuntimeProfileSnapshot();
        Assert.Equal(caveResolution.Biome.Id, runtime.BiomeId);
        Assert.False(string.IsNullOrWhiteSpace(runtime.Lighting.ColorGradeId));
        Assert.NotEmpty(runtime.Spawning.HabitatTags);
        Assert.NotEmpty(runtime.Resources.ResourceTableIds);
    }

    [Fact]
    public void FrostwoodRegion_UsesFrozenMaterialsAndPlansRegionalPineGroves()
    {
        var (regionalProfile, biomes, structures) = LoadContracts();
        const int seed = 44_911;
        var planner = new WorldRegionPlanner(seed, regionalProfile, biomes, structures);
        var frostRegions = Enumerable.Range(-128, 257)
            .Select(index => planner.PlanRegion(index))
            .Where(region => region.Biome.Id == "frostwood")
            .ToArray();
        Assert.NotEmpty(frostRegions);
        var region = frostRegions[0];
        Assert.Contains(
            frostRegions.SelectMany(candidate => candidate.Features),
            feature => feature.DefinitionId == "frostwood_pine_groves" &&
                feature.BiomeId == "frostwood");

        var tileX = checked((int)(region.StartTileX +
            (region.EndTileXInclusive - region.StartTileX) / 2));
        var profile = WorldGenerationProfile.Small with
        {
            TreeAttempts = 0,
            WaterPocketAttempts = 0,
            Ores = Array.Empty<OreGenerationDefinition>()
        };
        var generator = new InfiniteWorldChunkGenerator(regionalPlanner: planner);
        var surfaceY = generator.GetSurfaceHeightAt(profile, seed, tileX);
        var surfacePosition = new TilePos(tileX, surfaceY);
        var belowPosition = new TilePos(tileX, surfaceY + 1);
        var surfaceChunk = generator.GenerateChunk(
            profile,
            seed,
            CoordinateUtils.TileToChunk(surfacePosition));
        var belowChunkPosition = CoordinateUtils.TileToChunk(belowPosition);
        var belowChunk = belowChunkPosition == surfaceChunk.Position
            ? surfaceChunk
            : generator.GenerateChunk(profile, seed, belowChunkPosition);
        var surfaceLocal = CoordinateUtils.LocalTileInChunk(surfacePosition);
        var belowLocal = CoordinateUtils.LocalTileInChunk(belowPosition);

        Assert.Equal(KnownTileIds.Snow, surfaceChunk.GetTile(surfaceLocal.X, surfaceLocal.Y).TileId);
        Assert.Equal(KnownTileIds.Ice, belowChunk.GetTile(belowLocal.X, belowLocal.Y).TileId);
    }
    [Fact]
    public void SurfaceHeightResolver_ReusesRegionalPlanWithoutSteadyStateAllocations()
    {
        var (regionalProfile, biomes, structures) = LoadContracts();
        const int seed = 44_911;
        var planner = new WorldRegionPlanner(seed, regionalProfile, biomes, structures);
        var generator = new InfiniteWorldChunkGenerator(regionalPlanner: planner);
        var surfaceProfile = WorldGenerationProfile.Small with
        {
            TreeAttempts = 0,
            WaterPocketAttempts = 0,
            Ores = Array.Empty<OreGenerationDefinition>()
        };
        var resolver = generator.CreateSurfaceHeightResolver(surfaceProfile, seed);
        var region = planner.PlanRegion(0);
        var startX = checked((int)region.StartTileX);
        var sampleCount = Math.Min(64, checked((int)(region.EndTileXInclusive - region.StartTileX + 1)));

        for (var offset = 0; offset < sampleCount; offset++)
        {
            var tileX = startX + offset;
            Assert.Equal(
                generator.GetSurfaceHeightAt(surfaceProfile, seed, tileX),
                resolver(tileX));
        }

        for (var iteration = 0; iteration < 8; iteration++)
        {
            for (var offset = 0; offset < sampleCount; offset++)
            {
                _ = resolver(startX + offset);
            }
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        var checksum = 0;
        for (var iteration = 0; iteration < 128; iteration++)
        {
            for (var offset = 0; offset < sampleCount; offset++)
            {
                checksum += resolver(startX + offset);
            }
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        GC.KeepAlive(checksum);
        Assert.Equal(0, allocated);
    }
    [Fact]
    public void StructurePlans_AreDeterministicAndRespectResolvedProfileFilters()
    {
        var (profile, biomes, structures) = LoadContracts();
        var planner = new WorldRegionPlanner(98765, profile, biomes, structures);
        var plans = Enumerable.Range(-80, 161)
            .SelectMany(index => planner.PlanRegion(index).Structures)
            .ToArray();

        Assert.NotEmpty(plans);
        Assert.Contains(plans, value => value.Placement == "surface");
        Assert.Contains(plans, value => value.Placement.Contains("cave", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(
            plans,
            Enumerable.Range(-80, 161)
                .SelectMany(index => planner.PlanRegion(index).Structures)
                .ToArray());
    }

    private static (RegionalGenerationProfile, BiomeRegistry, IReadOnlyList<StructurePlanDefinition>) LoadContracts()
    {
        var dataRoot = FindGameDataRoot();
        var profile = Assert.Single(
            new RegionalGenerationProfileJsonLoader().LoadProfilesFromDirectory(
                Path.Combine(dataRoot, "worldgen")));
        var biomes = new BiomeJsonLoader().LoadRegistryFromDirectory(Path.Combine(dataRoot, "biomes"));
        var structures = new StructurePlanDefinitionJsonLoader().LoadDefinitionsFromDirectory(
            Path.Combine(dataRoot, "structures"));
        return (profile, biomes, structures);
    }

    private static string FindGameDataRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "Game.Data");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository Game.Data directory.");
    }
}
