using Game.Core.Biomes;
using Game.Core.Crafting;
using Game.Core.Data;
using Game.Core.Entities;
using Game.Core.Items;
using Game.Core.Loot;
using Game.Core.Projectiles;
using Game.Core.Randomness;
using Game.Core.Spawning;
using Game.Core.Time;
using Game.Core.Tiles;
using Game.Core.World;
using Xunit;

namespace Game.Tests.SpawningTests;

public sealed class AdvancedSpawnRuleTests
{
    [Fact]
    public void Loader_ReadsTimeHabitatGroupAndCooldown()
    {
        const string json = """
        {
          "id": "forest_day_squirrel",
          "entityId": "squirrel",
          "time": "day",
          "habitats": ["surface", "openAir"],
          "minTileY": 3,
          "maxTileY": 90,
          "maxActive": 5,
          "populationGroup": "forest_wildlife",
          "maxActiveInGroup": 9,
          "cooldownSeconds": 4.5
        }
        """;

        var rule = new SpawnRuleJsonLoader().LoadDefinitionFromJson(json);

        Assert.Equal(SpawnTimeCondition.Day, rule.Time);
        Assert.Equal(new[] { SpawnHabitat.Surface, SpawnHabitat.OpenAir }, rule.Habitats);
        Assert.Equal("forest_wildlife", rule.PopulationGroup);
        Assert.Equal(9, rule.MaxActiveInGroup);
        Assert.Equal(4.5f, rule.CooldownSeconds);
    }

    [Fact]
    public void TrySpawn_EnforcesRuleCooldownUntilAdvanced()
    {
        var world = CreateGroundWorld();
        var entities = new EntityManager();
        var system = new SpawnSystem(new Random(5));
        var content = CreateContent(new SpawnRuleDefinition
        {
            Id = "squirrel",
            EntityId = "squirrel",
            Time = SpawnTimeCondition.Day,
            Habitats = new[] { SpawnHabitat.Surface },
            Chance = 1,
            MaxActive = 5,
            CooldownSeconds = 3
        });
        var time = new WorldTime();
        time.SetDay();

        var first = system.TrySpawn(world, entities, content, new BiomeMap("forest"), time, new TilePos(4, 4));
        var blocked = system.TrySpawn(world, entities, content, new BiomeMap("forest"), time, new TilePos(6, 4));
        system.AdvanceCooldowns(3);
        var resumed = system.TrySpawn(world, entities, content, new BiomeMap("forest"), time, new TilePos(6, 4));

        Assert.True(first.Spawned);
        Assert.False(blocked.Spawned);
        Assert.True(resumed.Spawned);
        Assert.Equal(3, system.GetCooldownRemaining("squirrel"));
    }

    [Fact]
    public void TrySpawn_RejectsWrongTimeHabitatAndDepth()
    {
        var world = CreateGroundWorld();
        var content = CreateContent(new SpawnRuleDefinition
        {
            Id = "night_cavern",
            EntityId = "squirrel",
            Time = SpawnTimeCondition.Night,
            Habitats = new[] { SpawnHabitat.Cavern },
            MinTileY = 8,
            Chance = 1,
            MaxActive = 5
        });
        var time = new WorldTime();
        time.SetDay();

        var result = new SpawnSystem(new Random(1)).TrySpawn(
            world,
            new EntityManager(),
            content,
            new BiomeMap("forest"),
            time,
            new TilePos(4, 4));

        Assert.False(result.Spawned);
    }

    [Fact]
    public void TrySpawn_EnforcesPopulationGroupCapAcrossDefinitions()
    {
        var world = CreateGroundWorld();
        var entities = new EntityManager();
        var firstRule = new SpawnRuleDefinition
        {
            Id = "squirrel",
            EntityId = "squirrel",
            Chance = 1,
            MaxActive = 5,
            PopulationGroup = "wildlife",
            MaxActiveInGroup = 1
        };
        var secondRule = firstRule with { Id = "firefly", EntityId = "firefly" };
        var system = new SpawnSystem(new Random(1));
        var content = CreateContent(firstRule, secondRule);

        var first = system.TrySpawn(world, entities, content, new BiomeMap("forest"), new WorldTime(), new TilePos(4, 4));
        var second = system.TrySpawn(world, entities, content, new BiomeMap("forest"), new WorldTime(), new TilePos(6, 4));

        Assert.True(first.Spawned);
        Assert.False(second.Spawned);
        Assert.Single(entities.Entities);
        Assert.Equal("wildlife", Assert.IsType<EnemyEntity>(entities.Entities[0]).SpawnGroup);
    }

    [Fact]
    public void Scheduler_AdvancesSpawnSystemCooldownOnExistingTickPath()
    {
        var world = CreateGroundWorld(width: 40);
        var entities = new EntityManager();
        var spawnSystem = new SpawnSystem(new Random(3));
        var scheduler = new SpawnScheduler(new Random(3), spawnSystem);
        var content = CreateContent(new SpawnRuleDefinition
        {
            Id = "squirrel",
            EntityId = "squirrel",
            Chance = 1,
            MaxActive = 10,
            CooldownSeconds = 2
        });
        var options = new SpawnSchedulerOptions
        {
            SpawnIntervalSeconds = 0.5f,
            AttemptsPerInterval = 4,
            MinDistanceTiles = 2,
            MaxDistanceTiles = 5,
            VerticalSearchRadiusTiles = 4,
            MaxTotalActiveEnemies = 10,
            DespawnDistanceTiles = 30
        };

        var first = scheduler.Update(world, entities, content, new BiomeMap("forest"), new WorldTime(), new TilePos(20, 4), 0.5f, options);
        var blocked = scheduler.Update(world, entities, content, new BiomeMap("forest"), new WorldTime(), new TilePos(20, 4), 0.5f, options);
        scheduler.Update(world, entities, content, new BiomeMap("forest"), new WorldTime(), new TilePos(20, 4), 1.5f, options);

        Assert.Equal(1, first.Spawned);
        Assert.Equal(0, blocked.Spawned);
        Assert.True(entities.Entities.Count >= 2);
    }

    [Fact]
    public void TrySpawn_EnforcesRegionalCapButAllowsAdjacentRegion()
    {
        var world = CreateGroundWorld(width: 160);
        var entities = new EntityManager();
        var rule = new SpawnRuleDefinition
        {
            Id = "regional_squirrel",
            EntityId = "squirrel",
            Chance = 1,
            MaxActive = 10,
            PopulationGroup = "wildlife",
            MaxActiveInGroup = 10,
            PopulationRegionSizeTiles = 32,
            MaxActiveInRegion = 1,
            MaxActiveInHabitat = 10,
            Habitats = new[] { SpawnHabitat.Surface }
        };
        var system = new SpawnSystem(new Random(3));
        var content = CreateContent(rule);

        var first = system.TrySpawn(world, entities, content, new BiomeMap("forest"), new WorldTime(), new TilePos(4, 4));
        var sameRegion = system.TrySpawn(world, entities, content, new BiomeMap("forest"), new WorldTime(), new TilePos(20, 4));
        var adjacentRegion = system.TrySpawn(world, entities, content, new BiomeMap("forest"), new WorldTime(), new TilePos(40, 4));

        Assert.True(first.Spawned);
        Assert.False(sameRegion.Spawned);
        Assert.True(adjacentRegion.Spawned);
        Assert.NotEqual(first.Entity!.SpawnRegion, adjacentRegion.Entity!.SpawnRegion);
    }

    [Fact]
    public void TrySpawn_EnforcesHabitatCapAcrossRegions()
    {
        var world = CreateGroundWorld(width: 160);
        var entities = new EntityManager();
        var rule = new SpawnRuleDefinition
        {
            Id = "surface_squirrel",
            EntityId = "squirrel",
            Chance = 1,
            MaxActive = 10,
            PopulationGroup = "wildlife",
            MaxActiveInGroup = 10,
            PopulationRegionSizeTiles = 32,
            MaxActiveInRegion = 4,
            MaxActiveInHabitat = 1,
            Habitats = new[] { SpawnHabitat.Surface }
        };
        var system = new SpawnSystem(new Random(4));
        var content = CreateContent(rule);

        var first = system.TrySpawn(world, entities, content, new BiomeMap("forest"), new WorldTime(), new TilePos(4, 4));
        var otherRegion = system.TrySpawn(world, entities, content, new BiomeMap("forest"), new WorldTime(), new TilePos(40, 4));

        Assert.True(first.Spawned);
        Assert.False(otherRegion.Spawned);
    }

    [Fact]
    public void SpawnRegionKey_UsesFloorDivisionForNegativeCoordinates()
    {
        Assert.Equal(new SpawnRegionKey(-1, 0), SpawnRegionKey.FromTile(new TilePos(-1, 4), 32));
        Assert.Equal(new SpawnRegionKey(-2, 0), SpawnRegionKey.FromTile(new TilePos(-33, 4), 32));
    }

    [Fact]
    public void Scheduler_AcceptsSeparatedNamedCandidateAndRuleStreams()
    {
        var firstRegistry = new SessionRandomRegistry(7331);
        var secondRegistry = new SessionRandomRegistry(7331);
        var options = new SpawnSchedulerOptions
        {
            SpawnIntervalSeconds = 0.5f,
            AttemptsPerInterval = 3,
            MinDistanceTiles = 2,
            MaxDistanceTiles = 8,
            VerticalSearchRadiusTiles = 4,
            MaxTotalActiveEnemies = 8,
            DespawnDistanceTiles = 40
        };
        var content = CreateContent(new SpawnRuleDefinition
        {
            Id = "squirrel",
            EntityId = "squirrel",
            Chance = 0.5f,
            MaxActive = 8
        });

        var firstEntities = new EntityManager();
        var secondEntities = new EntityManager();
        var first = new SpawnScheduler(
            firstRegistry.GetStream("spawning.candidates"),
            firstRegistry.GetStream("spawning.rules"));
        var second = new SpawnScheduler(
            secondRegistry.GetStream("spawning.candidates"),
            secondRegistry.GetStream("spawning.rules"));

        var firstResult = first.Update(
            CreateGroundWorld(64),
            firstEntities,
            content,
            new BiomeMap("forest"),
            new WorldTime(),
            new TilePos(32, 4),
            0.5f,
            options);
        var secondResult = second.Update(
            CreateGroundWorld(64),
            secondEntities,
            content,
            new BiomeMap("forest"),
            new WorldTime(),
            new TilePos(32, 4),
            0.5f,
            options);

        Assert.Equal(firstResult, secondResult);
        Assert.Equal(
            firstEntities.Entities.OfType<EnemyEntity>().Select(entity => entity.Position),
            secondEntities.Entities.OfType<EnemyEntity>().Select(entity => entity.Position));
    }

    [Fact]
    public void ExplicitBiomeOverloads_MatchWithoutAllocatingOnStableRejectedTicks()
    {
        var world = CreateGroundWorld(64);
        var entities = new EntityManager();
        var content = CreateContent(new SpawnRuleDefinition
        {
            Id = "forest_squirrel",
            EntityId = "squirrel",
            BiomeId = "forest",
            Chance = 1,
            MaxActive = 4
        });
        var time = new WorldTime();
        var spawnSystem = new SpawnSystem(new Random(5));
        var scheduler = new SpawnScheduler(new Random(6), spawnSystem);
        var options = new SpawnSchedulerOptions
        {
            SpawnIntervalSeconds = 0.001f,
            AttemptsPerInterval = 1,
            MinDistanceTiles = 2,
            MaxDistanceTiles = 4,
            VerticalSearchRadiusTiles = 4,
            MaxTotalActiveEnemies = 4,
            DespawnDistanceTiles = 40
        };

        Assert.False(spawnSystem.TrySpawn(world, entities, content, "desert", time, new TilePos(4, 4)).Spawned);
        scheduler.Update(world, entities, content, "desert", time, new TilePos(32, 4), 0.001f, options);
        for (var warmup = 0; warmup < 128; warmup++)
        {
            spawnSystem.TrySpawn(world, entities, content, "desert", time, new TilePos(4, 4));
            scheduler.Update(world, entities, content, "desert", time, new TilePos(32, 4), 0.001f, options);
        }

        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        for (var iteration = 0; iteration < 1_000; iteration++)
        {
            spawnSystem.TrySpawn(world, entities, content, "desert", time, new TilePos(4, 4));
        }

        var spawnAllocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        for (var iteration = 0; iteration < 1_000; iteration++)
        {
            scheduler.Update(world, entities, content, "desert", time, new TilePos(32, 4), 0.001f, options);
        }

        var schedulerAllocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

        Assert.True(
            spawnAllocated == 0 &&
            schedulerAllocated == 0,
            $"TrySpawn={spawnAllocated}, Update={schedulerAllocated}");
    }

    private static World CreateGroundWorld(int width = 16)
    {
        var world = new World(width, 16, WorldMetadata.CreateDefault(11));
        for (var x = 0; x < world.WidthTiles; x++)
        {
            world.SetTile(x, 5, KnownTileIds.Dirt);
        }

        return world;
    }

    private static GameContentDatabase CreateContent(params SpawnRuleDefinition[] rules)
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
            EntityDefinitionRegistry.Create(new[]
            {
                CreateEntity("squirrel"),
                CreateEntity("firefly")
            }),
            SpawnRuleRegistry.Create(rules));
    }

    private static EntityDefinition CreateEntity(string id)
    {
        return new EntityDefinition
        {
            Id = id,
            DisplayName = id,
            TexturePath = $"entities/{id}",
            MaxHealth = 5,
            Faction = EntityFaction.Friendly,
            ContactDamage = 0,
            ContactKnockback = 0
        };
    }
}
