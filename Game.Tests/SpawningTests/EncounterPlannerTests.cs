using Game.Core;
using Game.Core.Biomes;
using Game.Core.Crafting;
using Game.Core.Data;
using Game.Core.Entities;
using Game.Core.Entities.AI;
using Game.Core.Items;
using Game.Core.Loot;
using Game.Core.Physics;
using Game.Core.Projectiles;
using Game.Core.Randomness;
using Game.Core.Spawning;
using Game.Core.Time;
using Game.Core.Tiles;
using Game.Core.World;
using Xunit;

namespace Game.Tests.SpawningTests;

public sealed class EncounterPlannerTests
{
    [Fact]
    public void WeightedSelection_UsesRepeatableEncounterStreamAndRoleCounts()
    {
        var encounters = EncounterDefinitionRegistry.Create(new[]
        {
            CreateEncounter("foragers", "rule-a", "rule-b") with { Weight = 1.5f },
            CreateEncounter("patrol", "rule-c", "rule-d") with { Weight = 0.75f }
        });
        var first = new EncounterPlanner(
            new SessionRandomRegistry(4_241).GetStream("spawning.encounters"));
        var second = new EncounterPlanner(
            new SessionRandomRegistry(4_241).GetStream("spawning.encounters"));
        var entities = new EntityManager();
        var time = new WorldTime();
        time.SetDay();
        var environment = new SpawnEnvironment("forest", "surface", "Clear", null, 1f);

        var firstPlans = Enumerable.Range(0, 24)
            .Select(_ => first.TryPlan(encounters, entities, environment, time, new TilePos(-33, 20)))
            .ToArray();
        var secondPlans = Enumerable.Range(0, 24)
            .Select(_ => second.TryPlan(encounters, entities, environment, time, new TilePos(-33, 20)))
            .ToArray();
        var firstTrace = firstPlans.Select(Describe).ToArray();
        var secondTrace = secondPlans.Select(Describe).ToArray();

        Assert.Equal(firstTrace, secondTrace);
        Assert.All(firstTrace, trace => Assert.NotEqual("<none>", trace));
        Assert.Equal(2, firstPlans.Select(plan => plan!.Definition.Id).Distinct().Count());
        Assert.All(firstPlans, plan =>
        {
            var resolved = Assert.IsType<EncounterPlan>(plan);
            Assert.InRange(resolved.Spawns.Count, 1, 4);
            Assert.All(
                resolved.Spawns.GroupBy(spawn => spawn.RoleId),
                role => Assert.InRange(role.Count(), 1, 2));
        });
    }

    [Fact]
    public void CooldownAndGlobalRegionalCaps_BlockPlansUntilAvailable()
    {
        var encounter = CreateEncounter("bounded", "rule-a") with
        {
            CooldownSeconds = 5f,
            MaxActiveGlobal = 2,
            MaxActiveInRegion = 1,
            PopulationRegionSizeTiles = 32
        };
        var registry = EncounterDefinitionRegistry.Create(new[] { encounter });
        var planner = new EncounterPlanner(
            new SessionRandomRegistry(9_113).GetStream("spawning.encounters"));
        var entities = new EntityManager();
        var time = new WorldTime();
        time.SetDay();
        var environment = new SpawnEnvironment("forest", "surface", "Clear", null, 1f);
        var origin = new TilePos(-5, 12);

        var initial = Assert.IsType<EncounterPlan>(planner.TryPlan(registry, entities, environment, time, origin));
        planner.CommitSpawned(initial);

        Assert.Equal(5f, planner.GetCooldownRemaining(encounter.Id));
        Assert.Null(planner.TryPlan(registry, entities, environment, time, origin));
        planner.AdvanceCooldowns(4.9f);
        Assert.Null(planner.TryPlan(registry, entities, environment, time, origin));
        planner.AdvanceCooldowns(0.1f);

        entities.Add(CreateActor("rule-a", new TilePos(-5, 12)));
        Assert.Null(planner.TryPlan(registry, entities, environment, time, origin));
        Assert.NotNull(planner.TryPlan(registry, entities, environment, time, new TilePos(40, 12)));

        entities.Add(CreateActor("rule-a", new TilePos(40, 12)));
        Assert.Null(planner.TryPlan(registry, entities, environment, time, new TilePos(80, 12)));
    }

    [Fact]
    public void Scheduler_ConsumesEncounterPlanInLoadedOffscreenNegativeXBand()
    {
        var world = CreateInfiniteGroundWorld(-128, 32, groundY: 40);
        var content = CreateSchedulerContent();
        var entities = new EntityManager();
        var time = new WorldTime();
        time.SetDay();
        var visible = RectI.FromInclusiveTileBounds(-50, 28, -30, 48);
        var sources = new[]
        {
            SpawnActivitySource.ForPlayer(
                1,
                new TilePos(-40, 39),
                visible,
                new SpawnEnvironment("forest", "surface", "Clear", null, 1f))
        };
        var options = new SpawnSchedulerOptions
        {
            SpawnIntervalSeconds = 0.1f,
            AttemptsPerInterval = 8,
            MinDistanceTiles = 14,
            MaxDistanceTiles = 24,
            VerticalSearchRadiusTiles = 8,
            PlacementSearchRadiusTiles = 8,
            ViewportIngressBandTiles = 8,
            MaxTotalActiveEnemies = 8,
            DespawnDistanceTiles = 128,
            OnScreenExclusionPaddingTiles = 2
        };
        var streams = new SessionRandomRegistry(7_733);
        var scheduler = new SpawnScheduler(
            streams.GetStream("spawning.candidates"),
            streams.GetStream("spawning.rules"),
            streams.GetStream("spawning.encounters"));

        var first = scheduler.Update(
            world,
            entities,
            content,
            new BiomeMap("forest"),
            time,
            sources,
            0.1f,
            options);

        Assert.Equal(options.AttemptsPerInterval, first.Attempts);
        Assert.Equal(2, first.Spawned);
        var actors = entities.Entities.OfType<EnemyEntity>().ToArray();
        Assert.Equal(2, actors.Length);
        Assert.Equal(
            new[] { "encounter-bat", "encounter-squirrel" },
            actors.Select(actor => actor.SpawnRuleId!).Order().ToArray());
        Assert.All(actors, actor => Assert.Equal("negative-x-pair", actor.SpawnEncounterId));
        Assert.All(actors, actor =>
        {
            var tile = CoordinateUtils.WorldToTile(actor.Body.Center.X, actor.Body.Center.Y);
            Assert.True(tile.X < 0, $"Expected negative X placement, got {tile}.");
            Assert.False(visible.Contains(tile.X, tile.Y));
            Assert.InRange(
                Math.Abs(tile.X - sources[0].CenterTile.X),
                options.MinDistanceTiles,
                options.MaxDistanceTiles);
            Assert.True(world.TryGetChunk(CoordinateUtils.TileToChunk(tile), out _));
            var definition = content.Entities.GetById(actor.DefinitionId);
            Assert.NotNull(definition.Ai);
            Assert.NotEqual(AiBehaviorKind.None, definition.Ai.Kind);
        });
        Assert.True(scheduler.GetEncounterCooldownRemaining("negative-x-pair") > 0);

        var blocked = scheduler.Update(
            world,
            entities,
            content,
            new BiomeMap("forest"),
            time,
            sources,
            0.1f,
            options);

        Assert.Equal(options.AttemptsPerInterval, blocked.Attempts);
        Assert.Equal(0, blocked.Spawned);
    }

    [Fact]
    public void Scheduler_FallsBackToNormalRulesWhenMatchingEncounterIsCapped()
    {
        var world = CreateInfiniteGroundWorld(-96, 96, groundY: 40);
        var content = CreateSchedulerContentWithFallback();
        var entities = new EntityManager();
        entities.Add(CreateActor("encounter-squirrel", new TilePos(0, 39)));
        var time = new WorldTime();
        time.SetDay();
        var source = SpawnActivitySource.ForPlayer(
            1,
            new TilePos(0, 39),
            RectI.FromInclusiveTileBounds(-12, 28, 12, 48),
            new SpawnEnvironment("forest", "surface", "Clear", null, 1f));
        var options = new SpawnSchedulerOptions
        {
            SpawnIntervalSeconds = 0.1f,
            AttemptsPerInterval = 8,
            MinDistanceTiles = 14,
            MaxDistanceTiles = 24,
            VerticalSearchRadiusTiles = 8,
            PlacementSearchRadiusTiles = 8,
            MaxTotalActiveEnemies = 6,
            DespawnDistanceTiles = 128,
            OnScreenExclusionPaddingTiles = 2
        };

        var result = new SpawnScheduler(new Random(3_771)).Update(
            world,
            entities,
            content,
            new BiomeMap("forest"),
            time,
            new[] { source },
            0.1f,
            options);

        Assert.True(result.Attempts > 0);
        Assert.True(result.Spawned > 0);
        Assert.Contains(
            entities.Entities,
            entity => entity is EnemyEntity { SpawnRuleId: "fallback-bat" });
    }

    private static string Describe(EncounterPlan? plan)
    {
        return plan is null
            ? "<none>"
            : $"{plan.Definition.Id}:{string.Join(',', plan.Spawns.Select(spawn => $"{spawn.RoleId}/{spawn.SpawnRuleId}/{spawn.RoleOrdinal}"))}";
    }

    private static EncounterDefinition CreateEncounter(string id, params string[] spawnRuleIds)
    {
        return new EncounterDefinition
        {
            Id = id,
            BiomeIds = new[] { "forest" },
            VerticalLayerIds = new[] { "surface" },
            Time = SpawnTimeCondition.Day,
            MinDistanceTiles = 14,
            MaxDistanceTiles = 24,
            CooldownSeconds = 1f,
            MaxActiveGlobal = 12,
            PopulationRegionSizeTiles = 64,
            MaxActiveInRegion = 8,
            MinRoleSelections = 1,
            MaxRoleSelections = spawnRuleIds.Length,
            Roles = spawnRuleIds.Select((ruleId, index) => new EncounterRoleDefinition
            {
                Id = $"role-{index}",
                SpawnRuleId = ruleId,
                Weight = index + 1,
                MinCount = 1,
                MaxCount = 2
            }).ToArray()
        };
    }

    private static EnemyEntity CreateActor(string spawnRuleId, TilePos tile)
    {
        var actor = new EntityFactory(new TileCollisionResolver()).CreateEnemy(
            CreateEntity("bounded-actor", EntityFaction.Friendly, EntityMovementMode.Ground, AiBehaviorKind.Critter),
            CoordinateUtils.TileToWorld(tile));
        actor.AssignSpawnMetadata(spawnRuleId, null);
        return actor;
    }

    private static World CreateInfiniteGroundWorld(int minX, int maxX, int groundY)
    {
        var world = new World(
            GameConstants.ChunkSize,
            96,
            WorldMetadata.CreateDefault(733),
            isHorizontallyInfinite: true);
        for (var x = minX; x <= maxX; x++)
        {
            world.SetTile(x, groundY, KnownTileIds.Dirt);
        }

        return world;
    }

    private static GameContentDatabase CreateSchedulerContent()
    {
        var rules = new[]
        {
            new SpawnRuleDefinition
            {
                Id = "encounter-squirrel",
                EntityId = "squirrel",
                BiomeId = "forest",
                Time = SpawnTimeCondition.Day,
                Habitats = new[] { SpawnHabitat.Surface },
                Chance = 0,
                MaxActive = 8
            },
            new SpawnRuleDefinition
            {
                Id = "encounter-bat",
                EntityId = "bat",
                BiomeId = "forest",
                Time = SpawnTimeCondition.Day,
                Habitats = new[] { SpawnHabitat.Surface },
                Chance = 0,
                MaxActive = 8
            }
        };
        var encounter = new EncounterDefinition
        {
            Id = "negative-x-pair",
            BiomeIds = new[] { "forest" },
            VerticalLayerIds = new[] { "surface" },
            Time = SpawnTimeCondition.Day,
            MinDistanceTiles = 14,
            MaxDistanceTiles = 24,
            CooldownSeconds = 4f,
            MaxActiveGlobal = 2,
            PopulationRegionSizeTiles = 64,
            MaxActiveInRegion = 2,
            MinRoleSelections = 2,
            MaxRoleSelections = 2,
            Roles = new[]
            {
                new EncounterRoleDefinition
                {
                    Id = "forager",
                    SpawnRuleId = "encounter-squirrel"
                },
                new EncounterRoleDefinition
                {
                    Id = "hunter",
                    SpawnRuleId = "encounter-bat"
                }
            }
        };

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
                CreateEntity("squirrel", EntityFaction.Friendly, EntityMovementMode.Ground, AiBehaviorKind.Critter),
                CreateEntity("bat", EntityFaction.Hostile, EntityMovementMode.Flying, AiBehaviorKind.Hostile)
            }),
            SpawnRuleRegistry.Create(rules))
        {
            Encounters = EncounterDefinitionRegistry.Create(new[] { encounter })
        };
    }

    private static GameContentDatabase CreateSchedulerContentWithFallback()
    {
        var content = CreateSchedulerContent();
        Assert.True(content.SpawnRules.TryGetById("encounter-squirrel", out var encounterRule));
        return new GameContentDatabase(
            content.Tiles,
            content.Items,
            content.Recipes,
            content.LootTables,
            content.Biomes,
            content.Projectiles,
            content.Entities,
            SpawnRuleRegistry.Create(new[]
            {
                encounterRule with { MaxActive = 1 },
                new SpawnRuleDefinition
                {
                    Id = "fallback-bat",
                    EntityId = "bat",
                    BiomeId = "forest",
                    Time = SpawnTimeCondition.Day,
                    Habitats = new[] { SpawnHabitat.Surface },
                    Chance = 1,
                    MaxActive = 5
                }
            }))
        {
            Encounters = EncounterDefinitionRegistry.Create(new[]
            {
                CreateEncounter("capped-foragers", "encounter-squirrel") with
                {
                    MaxActiveGlobal = 1,
                    MaxActiveInRegion = 1
                }
            })
        };
    }

    private static EntityDefinition CreateEntity(
        string id,
        EntityFaction faction,
        EntityMovementMode movementMode,
        AiBehaviorKind aiKind)
    {
        return new EntityDefinition
        {
            Id = id,
            DisplayName = id,
            TexturePath = $"entities/{id}",
            MaxHealth = 10,
            Width = 12,
            Height = 10,
            Faction = faction,
            MovementMode = movementMode,
            ContactDamage = faction == EntityFaction.Hostile ? 4 : 0,
            Ai = new AiProfileDefinition
            {
                Kind = aiKind,
                DecisionInterval = 0.5f
            }
        };
    }
}
