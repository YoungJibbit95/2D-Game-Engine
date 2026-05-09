using Game.Core.Biomes;
using Game.Core.Crafting;
using Game.Core.Data;
using Game.Core.Entities;
using Game.Core.Inventory;
using Game.Core.Items;
using Game.Core.Loot;
using Game.Core.Projectiles;
using Game.Core.Spawning;
using Game.Core.Tiles;
using Game.Core.World;
using Game.Core.World.Generation;
using Xunit;

namespace Game.Tests.WorldTests;

public sealed class WorldGenerationServiceTests
{
    [Fact]
    public void Generate_UsesContentProfileAndReturnsAnalysis()
    {
        var content = CreateContent(WorldGenerationProfile.Small with
        {
            Id = "tiny",
            WidthTiles = 96,
            HeightTiles = 64,
            CaveWalkerCount = 0,
            CaveWalkLength = 0,
            Ores = Array.Empty<OreGenerationDefinition>(),
            TreeAttempts = 0,
            WaterPocketAttempts = 0
        });

        var result = new WorldGenerationService().Generate(content, new WorldGenerationRequest
        {
            ProfileId = "tiny",
            Seed = 123,
            QualityRules = new WorldGenerationQualityRules { MinLiquidTiles = 0, MinSurfaceVariance = 0 }
        });

        Assert.Equal("tiny", result.Profile.Id);
        Assert.Equal(96, result.Generation.World.WidthTiles);
        Assert.Equal(64, result.Analysis.HeightTiles);
        Assert.True(result.Quality.IsAcceptable);
    }

    [Fact]
    public void Generate_CanOverrideProfileDimensions()
    {
        var content = CreateContent(WorldGenerationProfile.Small);

        var result = new WorldGenerationService().Generate(content, new WorldGenerationRequest
        {
            ProfileId = "small",
            Seed = 321,
            WidthTiles = 80,
            HeightTiles = 72,
            QualityRules = new WorldGenerationQualityRules { MinLiquidTiles = 0, MinSurfaceVariance = 0 }
        });

        Assert.Equal(80, result.Profile.WidthTiles);
        Assert.Equal(72, result.Generation.World.HeightTiles);
    }

    [Fact]
    public void Generate_FallsBackToBuiltInProfileWhenContentRegistryIsEmpty()
    {
        var result = new WorldGenerationService().Generate(CreateContent(), new WorldGenerationRequest
        {
            ProfileId = "small",
            Seed = 456,
            WidthTiles = 64,
            HeightTiles = 64,
            QualityRules = new WorldGenerationQualityRules { MinLiquidTiles = 0, MinSurfaceVariance = 0 }
        });

        Assert.Equal(64, result.Generation.World.WidthTiles);
        Assert.Equal("small", result.Profile.Id);
    }

    private static GameContentDatabase CreateContent(params WorldGenerationProfile[] profiles)
    {
        return new GameContentDatabase(
            TileRegistry.Create(new[]
            {
                new TileDefinition
                {
                    NumericId = KnownTileIds.Dirt,
                    Id = "dirt",
                    DisplayName = "Dirt",
                    TexturePath = "tiles/dirt",
                    Solid = true
                },
                new TileDefinition
                {
                    NumericId = KnownTileIds.Grass,
                    Id = "grass",
                    DisplayName = "Grass",
                    TexturePath = "tiles/grass",
                    Solid = true
                },
                new TileDefinition
                {
                    NumericId = KnownTileIds.Stone,
                    Id = "stone",
                    DisplayName = "Stone",
                    TexturePath = "tiles/stone",
                    Solid = true
                },
                new TileDefinition
                {
                    NumericId = KnownTileIds.CopperOre,
                    Id = "copper_ore",
                    DisplayName = "Copper Ore",
                    TexturePath = "tiles/copper_ore",
                    Solid = true
                },
                new TileDefinition
                {
                    NumericId = KnownTileIds.IronOre,
                    Id = "iron_ore",
                    DisplayName = "Iron Ore",
                    TexturePath = "tiles/iron_ore",
                    Solid = true
                }
            }),
            ItemRegistry.Create(Array.Empty<ItemDefinition>()),
            RecipeRegistry.Create(Array.Empty<RecipeDefinition>()),
            LootTableRegistry.Create(Array.Empty<LootTableDefinition>()),
            BiomeRegistry.Create(Array.Empty<BiomeDefinition>()),
            ProjectileRegistry.Create(Array.Empty<ProjectileDefinition>()),
            EntityDefinitionRegistry.Create(Array.Empty<EntityDefinition>()),
            SpawnRuleRegistry.Create(Array.Empty<SpawnRuleDefinition>()))
        {
            WorldGenerationProfiles = WorldGenerationProfileRegistry.Create(profiles)
        };
    }
}
