using Game.Core.Biomes;
using Game.Core.World.Generation;
using Xunit;

namespace Game.Tests.BiomeTests;

public sealed class LivingWorldProfileTests
{
    [Fact]
    public void RepositoryProfiles_LoadEightRuntimeBiomesAndRegionalContracts()
    {
        var dataRoot = FindGameDataRoot();
        var biomes = new BiomeJsonLoader().LoadDefinitionsFromDirectory(Path.Combine(dataRoot, "biomes"));
        var registry = BiomeRegistry.Create(biomes);
        var profile = Assert.Single(
            new RegionalGenerationProfileJsonLoader().LoadProfilesFromDirectory(
                Path.Combine(dataRoot, "worldgen")));
        var structures = new StructurePlanDefinitionJsonLoader().LoadDefinitionsFromDirectory(
            Path.Combine(dataRoot, "structures"));

        Assert.Equal(
            ["amber_grove", "crystal_depths", "deep_cave", "forest", "frostwood", "meadow", "mushroom_cave", "twilight_marsh"],
            registry.Definitions.Select(value => value.Id).Order(StringComparer.Ordinal).ToArray());
        Assert.Equal(["amber_grove", "forest", "frostwood", "meadow", "twilight_marsh"], registry.Definitions
            .Where(value => value.IsRegionalBiome)
            .Select(value => value.Id)
            .Order(StringComparer.Ordinal)
            .ToArray());
        Assert.Equal(
            ["crystal_resonance", "deep_tremor", "firefly_bloom", "meadow_bloom", "spore_bloom"],
            profile.WorldEvents.Select(value => value.Id).Order(StringComparer.Ordinal).ToArray());
        Assert.Contains(profile.BiomeLayers, value => value.Id == "mushroom_caves");
        Assert.Contains(profile.BiomeLayers, value => value.Id == "crystal_depths");
        Assert.Contains(profile.BiomeLayers, value => value.Id == "deep_caverns");
        Assert.Contains(profile.BiomeLayers, value => value.Id == "deep_caves");
        Assert.Equal(7, structures.Count);
        Assert.Contains(profile.Features, value => value.Id == "amber_grove_workshops");
        Assert.Contains(profile.Features, value => value.Id == "twilight_marsh_roots");
        Assert.Contains(profile.Features, value => value.Id == "frostwood_pine_groves");
        var frostwood = registry.GetById("frostwood");
        Assert.True(frostwood.Weather.AllowsFrozenPrecipitation);
        Assert.True(frostwood.Weather.SnowWeight > 0);
        Assert.True(frostwood.Weather.BlizzardWeight > 0);
        Assert.All(
            registry.Definitions.Where(value => value.Id != "frostwood"),
            biome =>
            {
                Assert.False(biome.Weather.AllowsFrozenPrecipitation);
                Assert.Equal(0, biome.Weather.SnowWeight);
                Assert.Equal(0, biome.Weather.BlizzardWeight);
            });

        foreach (var biome in registry.Definitions)
        {
            Assert.False(string.IsNullOrWhiteSpace(biome.Ambient.SurfaceSoundscapeId));
            Assert.False(string.IsNullOrWhiteSpace(biome.Lighting.ColorGradeId));
            Assert.NotEmpty(biome.Spawning.HabitatTags);
            Assert.NotEmpty(biome.Resources.ResourceTableIds);
        }
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
