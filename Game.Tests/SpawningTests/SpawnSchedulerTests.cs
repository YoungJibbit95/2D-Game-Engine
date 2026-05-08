using Game.Core;
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

public sealed class SpawnSchedulerTests
{
    [Fact]
    public void Update_SpawnsEnemiesAroundPlayerAfterInterval()
    {
        var world = CreateWorldWithGround(width: 64, height: 16);
        var entities = new EntityManager(spatialCellSize: 16);
        var time = new WorldTime();
        time.SetNight();
        var scheduler = new SpawnScheduler(new Random(4));

        var result = scheduler.Update(
            world,
            entities,
            CreateContent(chance: 1f, maxActive: 8),
            new BiomeMap("forest"),
            time,
            new TilePos(32, 4),
            deltaSeconds: 1,
            new SpawnSchedulerOptions
            {
                SpawnIntervalSeconds = 0.25f,
                AttemptsPerInterval = 20,
                MinDistanceTiles = 2,
                MaxDistanceTiles = 6,
                VerticalSearchRadiusTiles = 4,
                MaxTotalActiveEnemies = 8,
                DespawnDistanceTiles = 40
            });

        Assert.True(result.Attempts > 0);
        Assert.True(result.Spawned > 0);
        Assert.NotEmpty(entities.Entities.OfType<EnemyEntity>());
    }

    [Fact]
    public void Update_DespawnsEnemiesFarFromPlayer()
    {
        var world = CreateWorldWithGround(width: 128, height: 16);
        var entities = new EntityManager(spatialCellSize: 16);
        entities.Add(new EntityFactory(new Game.Core.Physics.TileCollisionResolver()).CreateEnemy(CreateSlimeDefinition(), new System.Numerics.Vector2(110 * GameConstants.TileSize, 4 * GameConstants.TileSize)));

        var result = new SpawnScheduler(new Random(1)).Update(
            world,
            entities,
            CreateContent(chance: 0, maxActive: 8),
            new BiomeMap("forest"),
            new WorldTime(),
            new TilePos(8, 4),
            deltaSeconds: 0,
            new SpawnSchedulerOptions { DespawnDistanceTiles = 20 });

        Assert.Equal(1, result.Despawned);
        Assert.Empty(entities.Entities.OfType<EnemyEntity>());
    }

    private static World CreateWorldWithGround(int width, int height)
    {
        var world = new World(width, height, WorldMetadata.CreateDefault(seed: 1));
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
                    Hardness = 1
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
            EntityDefinitionRegistry.Create(new[] { CreateSlimeDefinition() }),
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

    private static EntityDefinition CreateSlimeDefinition()
    {
        return new EntityDefinition
        {
            Id = "slime",
            DisplayName = "Slime",
            TexturePath = "entities/slime",
            MaxHealth = 20
        };
    }
}
