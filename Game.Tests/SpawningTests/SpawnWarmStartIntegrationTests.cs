using System.Numerics;
using Game.Core;
using Game.Core.Biomes;
using Game.Core.Crafting;
using Game.Core.Data;
using Game.Core.Entities;
using Game.Core.Entities.AI;
using Game.Core.Items;
using Game.Core.Loot;
using Game.Core.Projectiles;
using Game.Core.Spawning;
using Game.Core.Tiles;
using Game.Core.Time;
using Game.Core.World;
using Xunit;

namespace Game.Tests.SpawningTests;

public sealed class SpawnWarmStartIntegrationTests
{
    private const float FixedDeltaSeconds = 1f / 60f;

    [Fact]
    public void EmptyPopulation_WarmStartSeedsActorAndPreservesPlacementContracts()
    {
        var world = CreateSurfaceWorld(0);
        var entities = new EntityManager();
        var spawnSystem = new SpawnSystem(new Random(19));
        var scheduler = new SpawnScheduler(new Random(27), spawnSystem);
        var source = CreateSource(0);
        var options = CreateOptions() with
        {
            WarmStartTargetPopulation = 1,
            WarmStartAttemptCycles = 1
        };

        var result = scheduler.Update(
            world,
            entities,
            CreateContent(chance: 0.000001f, cooldownSeconds: 7f),
            new BiomeMap("forest"),
            CreateDayTime(),
            new[] { source },
            FixedDeltaSeconds,
            options);

        var actor = Assert.IsType<EnemyEntity>(Assert.Single(entities.Entities));
        var actorTiles = ResolveTileBounds(actor);
        var playerBounds = new RectI(-8, 38 * GameConstants.TileSize, 16, 32);
        Assert.Equal(1, result.Attempts);
        Assert.Equal(1, result.Spawned);
        Assert.False(actorTiles.Intersects(source.VisibleTileBounds.Inflate(options.OnScreenExclusionPaddingTiles)));
        Assert.False(actor.Bounds.Intersects(playerBounds));
        Assert.True(world.TryGetChunk(CoordinateUtils.TileToChunk(actorTiles.X, actorTiles.Y), out _));
        Assert.Equal(7f, spawnSystem.GetCooldownRemaining("warm_start_actor"), precision: 3);
        Assert.Equal(1, scheduler.ActiveIngressCount);
    }

    [Theory]
    [InlineData(-512)]
    [InlineData(512)]
    public void ViewportIngress_ReachesVisibleEdgeWithinSixHundredFixedTicks(int centerX)
    {
        var world = CreateSurfaceWorld(centerX);
        var entities = new EntityManager();
        var scheduler = new SpawnScheduler(new Random(611));
        var source = CreateSource(centerX);
        var sources = new[] { source };
        var options = CreateOptions() with
        {
            WarmStartTargetPopulation = 1,
            WarmStartAttemptCycles = 1
        };
        var content = CreateContent(chance: 0.01f);
        var time = CreateDayTime();

        var first = scheduler.Update(
            world,
            entities,
            content,
            new BiomeMap("forest"),
            time,
            sources,
            FixedDeltaSeconds,
            options);
        var actor = Assert.IsType<EnemyEntity>(Assert.Single(entities.Entities));
        Assert.Equal(1, first.Spawned);
        Assert.False(ResolveTileBounds(actor).Intersects(source.VisibleTileBounds));

        var ingressTick = -1;
        for (var tick = 1; tick <= 600; tick++)
        {
            entities.UpdateAll(world, FixedDeltaSeconds);
            scheduler.Update(
                world,
                entities,
                content,
                new BiomeMap("forest"),
                time,
                sources,
                FixedDeltaSeconds,
                options);
            if (!ResolveTileBounds(actor).Intersects(source.VisibleTileBounds))
            {
                continue;
            }

            ingressTick = tick;
            break;
        }

        Assert.InRange(ingressTick, 1, 600);
        Assert.Equal(0, scheduler.ActiveIngressCount);
    }

    [Fact]
    public void WarmStartIngress_ReplaysDeterministically()
    {
        var first = RunIngressTrace(seed: 9_731, centerX: -384);
        var second = RunIngressTrace(seed: 9_731, centerX: -384);

        Assert.Equal(first, second);
        Assert.InRange(first.VisibleTick, 1, 600);
        Assert.NotEqual(0UL, first.TraceHash);
    }

    [Fact]
    public void Despawn_RemovesTrackedIngressWithoutImmediateReplacement()
    {
        var world = CreateSurfaceWorld(0);
        var entities = new EntityManager();
        var scheduler = new SpawnScheduler(new Random(43));
        var source = CreateSource(0);
        var sources = new[] { source };
        var options = CreateOptions() with
        {
            WarmStartTargetPopulation = 1,
            WarmStartAttemptCycles = 1,
            DespawnDistanceTiles = 40
        };
        var content = CreateContent(chance: 0.01f);
        var time = CreateDayTime();

        scheduler.Update(
            world,
            entities,
            content,
            new BiomeMap("forest"),
            time,
            sources,
            FixedDeltaSeconds,
            options);
        var actor = Assert.IsType<EnemyEntity>(Assert.Single(entities.Entities));
        actor.Body.Position = new Vector2(200 * GameConstants.TileSize, actor.Body.Position.Y);

        var result = scheduler.Update(
            world,
            entities,
            content,
            new BiomeMap("forest"),
            time,
            sources,
            FixedDeltaSeconds,
            options);

        Assert.Equal(1, result.Despawned);
        Assert.Empty(entities.Entities);
        Assert.Equal(0, scheduler.ActiveIngressCount);
    }

    [Fact]
    public void WarmStart_DoesNotGenerateOrEnterUnloadedChunks()
    {
        var world = CreateSurfaceWorld(0, loadedRadiusTiles: 8);
        var entities = new EntityManager();
        var scheduler = new SpawnScheduler(new Random(71));
        var initialChunks = world.Chunks.Count;
        var options = CreateOptions() with
        {
            MinDistanceTiles = 48,
            MaxDistanceTiles = 54,
            WarmStartTargetPopulation = 1,
            WarmStartAttemptCycles = 1
        };

        var result = scheduler.Update(
            world,
            entities,
            CreateContent(chance: 1f),
            new BiomeMap("forest"),
            CreateDayTime(),
            new[] { CreateSource(0) },
            FixedDeltaSeconds,
            options);

        Assert.Equal(0, result.Spawned);
        Assert.Empty(entities.Entities);
        Assert.Equal(initialChunks, world.Chunks.Count);
    }

    private static IngressTrace RunIngressTrace(int seed, int centerX)
    {
        var world = CreateSurfaceWorld(centerX);
        var entities = new EntityManager();
        var scheduler = new SpawnScheduler(new Random(seed));
        var source = CreateSource(centerX);
        var sources = new[] { source };
        var options = CreateOptions() with
        {
            WarmStartTargetPopulation = 1,
            WarmStartAttemptCycles = 1
        };
        var content = CreateContent(chance: 0.01f);
        var time = CreateDayTime();
        var biomeMap = new BiomeMap("forest");
        var visibleTick = -1;
        var traceHash = 14695981039346656037UL;

        for (var tick = 0; tick <= 600; tick++)
        {
            if (tick > 0)
            {
                entities.UpdateAll(world, FixedDeltaSeconds);
            }

            var result = scheduler.Update(
                world,
                entities,
                content,
                biomeMap,
                time,
                sources,
                FixedDeltaSeconds,
                options);
            traceHash = Hash(traceHash, result.Attempts);
            traceHash = Hash(traceHash, result.Spawned);
            for (var index = 0; index < entities.Entities.Count; index++)
            {
                if (entities.Entities[index] is not EnemyEntity actor)
                {
                    continue;
                }

                traceHash = Hash(traceHash, BitConverter.SingleToInt32Bits(actor.Body.Position.X));
                traceHash = Hash(traceHash, BitConverter.SingleToInt32Bits(actor.Body.Position.Y));
                if (visibleTick < 0 && ResolveTileBounds(actor).Intersects(source.VisibleTileBounds))
                {
                    visibleTick = tick;
                }
            }

            if (visibleTick >= 0 && scheduler.ActiveIngressCount == 0)
            {
                break;
            }
        }

        return new IngressTrace(traceHash, visibleTick, entities.Entities.Count);
    }

    private static SpawnSchedulerOptions CreateOptions()
    {
        return new SpawnSchedulerOptions
        {
            SpawnIntervalSeconds = 5f,
            AttemptsPerInterval = 1,
            MinDistanceTiles = 18,
            MaxDistanceTiles = 32,
            VerticalSearchRadiusTiles = 12,
            PlacementSearchRadiusTiles = 12,
            WarmStartIntervalSeconds = 0.25f,
            ViewportIngressBandTiles = 4,
            ViewportIngressAttemptCycle = 1,
            ViewportIngressAttemptsPerCycle = 1,
            ViewportIngressSpeedTilesPerSecond = 4f,
            ViewportIngressMaxSeconds = 10f,
            MaxTotalActiveEnemies = 4,
            DespawnDistanceTiles = 128,
            OnScreenExclusionPaddingTiles = 2
        };
    }

    private static SpawnActivitySource CreateSource(int centerX)
    {
        return SpawnActivitySource.ForPlayer(
            1,
            new TilePos(centerX, 39),
            RectI.FromInclusiveTileBounds(centerX - 16, 29, centerX + 16, 49),
            new SpawnEnvironment("forest", "surface", "Clear", null, 1f));
    }

    private static World CreateSurfaceWorld(int centerX, int loadedRadiusTiles = 72)
    {
        var world = new World(
            GameConstants.ChunkSize,
            128,
            WorldMetadata.CreateDefault(8_113),
            isHorizontallyInfinite: true);
        for (var x = centerX - loadedRadiusTiles; x <= centerX + loadedRadiusTiles; x++)
        {
            world.SetTile(x, 40, KnownTileIds.Dirt);
        }

        return world;
    }

    private static GameContentDatabase CreateContent(float chance, float cooldownSeconds = 0)
    {
        var rule = new SpawnRuleDefinition
        {
            Id = "warm_start_actor",
            EntityId = "mobile_actor",
            BiomeId = "forest",
            Time = SpawnTimeCondition.Day,
            Habitats = new[] { SpawnHabitat.Surface },
            Chance = chance,
            Weight = 1f,
            MaxActive = 4,
            PopulationGroup = "surface_actors",
            MaxActiveInGroup = 4,
            PopulationRegionSizeTiles = 64,
            MaxActiveInRegion = 4,
            MaxActiveInHabitat = 4,
            CooldownSeconds = cooldownSeconds
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
                new EntityDefinition
                {
                    Id = "mobile_actor",
                    DisplayName = "Mobile Actor",
                    TexturePath = "entities/mobile_actor",
                    MaxHealth = 10,
                    Width = 12,
                    Height = 14,
                    Faction = EntityFaction.Hostile,
                    MovementMode = EntityMovementMode.Ground,
                    Ai = new AiProfileDefinition
                    {
                        Kind = AiBehaviorKind.Hostile,
                        MoveSpeed = 48,
                        PatrolRadius = 128,
                        RequiresLineOfSight = false
                    }
                }
            }),
            SpawnRuleRegistry.Create(new[] { rule }));
    }

    private static WorldTime CreateDayTime()
    {
        var time = new WorldTime();
        time.SetDay();
        return time;
    }

    private static RectI ResolveTileBounds(EnemyEntity actor)
    {
        var min = CoordinateUtils.WorldToTile(actor.Body.Position.X, actor.Body.Position.Y);
        var max = CoordinateUtils.WorldToTile(
            actor.Body.Position.X + actor.Body.Size.X - 0.01f,
            actor.Body.Position.Y + actor.Body.Size.Y - 0.01f);
        return RectI.FromInclusiveTileBounds(min.X, min.Y, max.X, max.Y);
    }

    private static ulong Hash(ulong hash, int value)
    {
        unchecked
        {
            hash ^= (uint)value;
            return hash * 1099511628211UL;
        }
    }

    private readonly record struct IngressTrace(ulong TraceHash, int VisibleTick, int Population);
}
