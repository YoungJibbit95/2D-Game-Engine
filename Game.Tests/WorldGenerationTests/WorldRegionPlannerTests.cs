using Game.Core.Biomes;
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

        Assert.Contains(surface.Biome.Id, new[] { "amber_grove", "forest", "meadow", "twilight_marsh" });
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
