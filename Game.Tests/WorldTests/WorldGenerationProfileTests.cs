using Game.Core.World.Generation;
using Xunit;

namespace Game.Tests.WorldTests;

public sealed class WorldGenerationProfileTests
{
    [Fact]
    public void GenerateDetailed_UsesProfileDimensions()
    {
        var profile = WorldGenerationProfile.Small with
        {
            Id = "test",
            WidthTiles = 96,
            HeightTiles = 64
        };

        var result = new AdvancedWorldGenerator().GenerateDetailed(profile, seed: 123);

        Assert.Equal(96, result.World.WidthTiles);
        Assert.Equal(64, result.World.HeightTiles);
        Assert.True(result.SpawnTile.X >= 0);
        Assert.True(result.SpawnTile.Y >= 0);
    }

    [Fact]
    public void GenerateDetailed_ProfileCanDisableOptionalFeatures()
    {
        var profile = WorldGenerationProfile.Small with
        {
            Id = "flat_test",
            WidthTiles = 96,
            HeightTiles = 64,
            CaveWalkerCount = 0,
            CaveWalkLength = 0,
            CopperVeinCount = 0,
            IronVeinCount = 0,
            Ores = Array.Empty<OreGenerationDefinition>(),
            TreeAttempts = 0,
            WaterPocketAttempts = 0
        };

        var result = new AdvancedWorldGenerator().GenerateDetailed(profile, seed: 123);
        var analysis = new WorldAnalyzer().Analyze(result.World);

        Assert.False(analysis.TileCounts.ContainsKey(Game.Core.World.KnownTileIds.CopperOre));
        Assert.False(analysis.TileCounts.ContainsKey(Game.Core.World.KnownTileIds.IronOre));
        Assert.Equal(0, analysis.LiquidTileCount);
    }

    [Fact]
    public void GenerateDetailed_UsesDataDrivenOreDefinitions()
    {
        var profile = WorldGenerationProfile.Small with
        {
            Id = "ore_test",
            WidthTiles = 96,
            HeightTiles = 80,
            CaveWalkerCount = 0,
            CaveWalkLength = 0,
            CopperVeinCount = 0,
            IronVeinCount = 0,
            TreeAttempts = 0,
            WaterPocketAttempts = 0,
            Ores = new[]
            {
                new OreGenerationDefinition
                {
                    TileId = Game.Core.World.KnownTileIds.IronOre,
                    VeinCount = 8,
                    MinDepthOffset = 8,
                    Radius = 2,
                    MinLength = 8,
                    MaxLength = 8
                }
            }
        };

        var result = new AdvancedWorldGenerator().GenerateDetailed(profile, seed: 77);
        var analysis = new WorldAnalyzer().Analyze(result.World);

        Assert.False(analysis.TileCounts.ContainsKey(Game.Core.World.KnownTileIds.CopperOre));
        Assert.True(analysis.TileCounts[Game.Core.World.KnownTileIds.IronOre] > 0);
    }

    [Fact]
    public void ProfileRegistry_RejectsDuplicateIds()
    {
        var first = WorldGenerationProfile.Small with { Id = "duplicate" };
        var second = WorldGenerationProfile.Small with { Id = "duplicate" };

        Assert.Throws<Game.Core.Data.RegistryValidationException>(() =>
            WorldGenerationProfileRegistry.Create(new[] { first, second }));
    }
}
