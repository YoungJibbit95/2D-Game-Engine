using Game.Core.Biomes;
using Game.Core.Crafting;
using Game.Core.Data;
using Game.Core.Entities;
using Game.Core.Items;
using Game.Core.Loot;
using Game.Core.Projectiles;
using Game.Core.Spawning;
using Game.Core.Time;
using Game.Core.Tiles;
using Game.Core.World;
using Xunit;

namespace Game.Tests.SpawningTests;

public sealed class SpawnSystemTests
{
    [Fact]
    public void Loader_ReadsSpawnRuleJson()
    {
        const string json = """
        {
          "id": "forest_night_slime",
          "entityId": "slime",
          "biomeId": "forest",
          "requiresNight": true,
          "minTileY": 0,
          "maxTileY": 120,
          "chance": 1.0,
          "maxActive": 8
        }
        """;

        var rule = new SpawnRuleJsonLoader().LoadDefinitionFromJson(json);

        Assert.Equal("forest_night_slime", rule.Id);
        Assert.True(rule.RequiresNight);
        Assert.Equal("slime", rule.EntityId);
    }

    [Fact]
    public void TrySpawn_SpawnsMatchingNightBiomeRule()
    {
        var world = CreateWorldWithGround();
        var entities = new EntityManager();
        var time = new WorldTime();
        time.SetNight();

        var result = new SpawnSystem(new Random(1)).TrySpawn(
            world,
            entities,
            CreateContent(chance: 1f, maxActive: 8),
            new BiomeMap("forest"),
            time,
            new TilePos(4, 4));

        Assert.True(result.Spawned);
        Assert.Equal("forest_night_slime", result.RuleId);
        Assert.Single(entities.Entities.OfType<EnemyEntity>());
    }

    [Fact]
    public void TrySpawn_RejectsNightRuleDuringDay()
    {
        var world = CreateWorldWithGround();
        var time = new WorldTime();
        time.SetDay();

        var result = new SpawnSystem(new Random(1)).TrySpawn(
            world,
            new EntityManager(),
            CreateContent(chance: 1f, maxActive: 8),
            new BiomeMap("forest"),
            time,
            new TilePos(4, 4));

        Assert.False(result.Spawned);
    }

    [Fact]
    public void TrySpawn_RespectsMaxActiveCap()
    {
        var world = CreateWorldWithGround();
        var entities = new EntityManager();
        var content = CreateContent(chance: 1f, maxActive: 1);
        var time = new WorldTime();
        time.SetNight();

        var first = new SpawnSystem(new Random(1)).TrySpawn(world, entities, content, new BiomeMap("forest"), time, new TilePos(4, 4));
        var second = new SpawnSystem(new Random(1)).TrySpawn(world, entities, content, new BiomeMap("forest"), time, new TilePos(5, 4));

        Assert.True(first.Spawned);
        Assert.False(second.Spawned);
        Assert.Single(entities.Entities.OfType<EnemyEntity>());
    }

    private static World CreateWorldWithGround()
    {
        var world = new World(16, 16, WorldMetadata.CreateDefault(seed: 1));
        for (var x = 0; x < world.WidthTiles; x++)
        {
            world.SetTile(x, 5, KnownTileIds.Dirt);
        }

        return world;
    }

    private static GameContentDatabase CreateContent(float chance, int maxActive)
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
                    Solid = true,
                    BlocksLight = true,
                    Hardness = 1,
                    MiningPowerRequired = 0
                }
            }),
            ItemRegistry.Create(Array.Empty<ItemDefinition>()),
            RecipeRegistry.Create(Array.Empty<RecipeDefinition>()),
            LootTableRegistry.Create(Array.Empty<LootTableDefinition>()),
            BiomeRegistry.Create(new[]
            {
                new BiomeDefinition
                {
                    Id = "forest",
                    DisplayName = "Forest",
                    SurfaceTile = "dirt",
                    UndergroundTile = "dirt"
                }
            }),
            ProjectileRegistry.Create(Array.Empty<ProjectileDefinition>()),
            EntityDefinitionRegistry.Create(new[]
            {
                new EntityDefinition
                {
                    Id = "slime",
                    DisplayName = "Slime",
                    TexturePath = "entities/slime",
                    MaxHealth = 20
                }
            }),
            SpawnRuleRegistry.Create(new[]
            {
                new SpawnRuleDefinition
                {
                    Id = "forest_night_slime",
                    EntityId = "slime",
                    BiomeId = "forest",
                    RequiresNight = true,
                    Chance = chance,
                    MaxActive = maxActive
                }
            }));
    }
}
