using Game.Core;
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

public sealed class ActivityAreaSpawnTests
{
    [Fact]
    public void ViewportIngress_PrefersLoadedBandImmediatelyOutsideVisibleArea()
    {
        var world = CreateInfiniteGroundWorld(-128, 128, groundY: 40);
        var source = SpawnActivitySource.ForPlayer(
            1,
            new TilePos(0, 39),
            RectI.FromInclusiveTileBounds(-30, 22, 30, 48),
            new SpawnEnvironment("forest", "surface", "Clear", null, 1f));
        var options = CreateScaleOptions(maxActive: 24) with
        {
            AttemptsPerInterval = 96,
            MinDistanceTiles = 33,
            MaxDistanceTiles = 94,
            PlacementSearchRadiusTiles = 24,
            ViewportIngressBandTiles = 8,
            ViewportIngressAttemptCycle = 4,
            ViewportIngressAttemptsPerCycle = 3
        };
        var entities = new EntityManager();

        var result = new SpawnScheduler(new Random(991)).Update(
            world,
            entities,
            CreateContent(maxActive: 24),
            new BiomeMap("forest"),
            new WorldTime(),
            new[] { source },
            0.1f,
            options);

        var actors = entities.Entities.OfType<EnemyEntity>().ToArray();
        var nearIngress = actors.Count(actor =>
        {
            var tile = CoordinateUtils.WorldToTile(actor.Body.Center.X, actor.Body.Center.Y);
            var horizontalDistance = Math.Abs(tile.X - source.CenterTile.X);
            return horizontalDistance >= options.MinDistanceTiles &&
                   horizontalDistance <= options.MinDistanceTiles + options.ViewportIngressBandTiles + 3;
        });
        Assert.True(result.Spawned >= 12, $"spawned={result.Spawned}");
        Assert.True(nearIngress >= 8, $"nearIngress={nearIngress}, total={actors.Length}");
        Assert.All(actors, actor =>
        {
            var tile = CoordinateUtils.WorldToTile(actor.Body.Center.X, actor.Body.Center.Y);
            Assert.False(source.VisibleTileBounds.Inflate(options.OnScreenExclusionPaddingTiles).Contains(tile));
            Assert.True(world.TryGetChunk(CoordinateUtils.TileToChunk(tile), out _));
        });
    }

    [Fact]
    public void LargeWorldSurfaceHabitat_UsesOpenSkyBeyondLegacyHeightCutoff()
    {
        var world = new World(
            GameConstants.ChunkSize,
            300,
            WorldMetadata.CreateDefault(441),
            isHorizontallyInfinite: true);
        for (var x = -2; x <= 2; x++)
        {
            world.SetTile(x, 108, KnownTileIds.Dirt);
        }

        var result = new SpawnSystem(new Random(7)).TrySpawn(
            world,
            new EntityManager(),
            CreateContent(CreateRule(maxActive: 2) with
            {
                Id = "high_surface_squirrel",
                Habitats = new[] { SpawnHabitat.Surface }
            }),
            "forest",
            new WorldTime(),
            new TilePos(0, 107));

        Assert.True(result.Spawned);
        Assert.Equal(SpawnHabitat.Surface, result.Entity?.SpawnHabitat);
    }

    [Fact]
    public void MultiActivityRing_SpawnsTwoHundredAcrossBothAxesOutsideViewports()
    {
        var world = CreateInfiniteGroundWorld(-240, 240, groundY: 40);
        var entities = new EntityManager(32);
        var sources = new[]
        {
            SpawnActivitySource.ForPlayer(
                11,
                new TilePos(-80, 39),
                RectI.FromInclusiveTileBounds(-92, 28, -68, 48),
                new SpawnEnvironment("forest", "surface", "Clear", null, 1f)),
            SpawnActivitySource.ForCamera(
                22,
                new TilePos(80, 39),
                RectI.FromInclusiveTileBounds(68, 28, 92, 48),
                new SpawnEnvironment("forest", "surface", "Clear", null, 1f))
        };
        var options = CreateScaleOptions(maxActive: 200) with
        {
            AttemptsPerInterval = 4_000,
            MinDistanceTiles = 18,
            MaxDistanceTiles = 128,
            PlacementSearchRadiusTiles = 128,
            SectorCount = 16
        };

        var result = new SpawnScheduler(new Random(773)).Update(
            world,
            entities,
            CreateContent(maxActive: 200),
            new BiomeMap("forest"),
            new WorldTime(),
            sources,
            0.1f,
            options);

        Assert.Equal(200, result.Spawned);
        var negative = 0;
        var positive = 0;
        for (var index = 0; index < entities.Entities.Count; index++)
        {
            var enemy = Assert.IsType<EnemyEntity>(entities.Entities[index]);
            var tile = CoordinateUtils.WorldToTile(enemy.Body.Position);
            negative += tile.X < 0 ? 1 : 0;
            positive += tile.X > 0 ? 1 : 0;
            Assert.DoesNotContain(
                sources,
                source => source.VisibleTileBounds.Inflate(options.OnScreenExclusionPaddingTiles).Contains(tile));
            Assert.True(world.TryGetChunk(CoordinateUtils.TileToChunk(tile), out _));
        }

        Assert.True(negative >= 60, $"negative={negative}, positive={positive}");
        Assert.True(positive >= 60, $"negative={negative}, positive={positive}");
    }

    [Fact]
    public void ActivityRing_DoesNotGenerateOrSpawnIntoUnloadedChunks()
    {
        var world = CreateInfiniteGroundWorld(-32, 32, groundY: 20);
        var initialChunkCount = world.Chunks.Count;
        var sources = new[]
        {
            SpawnActivitySource.ForCamera(
                7,
                new TilePos(512, 19),
                RectI.FromInclusiveTileBounds(500, 10, 524, 28),
                new SpawnEnvironment("forest", "surface"))
        };

        var result = new SpawnScheduler(new Random(9)).Update(
            world,
            new EntityManager(),
            CreateContent(maxActive: 20),
            new BiomeMap("forest"),
            new WorldTime(),
            sources,
            0.1f,
            CreateScaleOptions(maxActive: 20) with { AttemptsPerInterval = 128 });

        Assert.Equal(0, result.Spawned);
        Assert.Equal(initialChunkCount, world.Chunks.Count);
    }

    [Fact]
    public void NamedCandidateAndRuleStreams_ReplayActivityCandidatesExactly()
    {
        var firstRegistry = new SessionRandomRegistry(928_441);
        var secondRegistry = new SessionRandomRegistry(928_441);
        var sources = new[]
        {
            SpawnActivitySource.ForPlayer(
                1,
                new TilePos(0, 31),
                RectI.FromInclusiveTileBounds(-10, 22, 10, 38),
                new SpawnEnvironment("forest", "surface", "Rain", "none", 1f))
        };
        var firstEntities = new EntityManager();
        var secondEntities = new EntityManager();
        var content = CreateContent(maxActive: 64);
        var options = CreateScaleOptions(maxActive: 64) with { AttemptsPerInterval = 512 };
        var firstScheduler = new SpawnScheduler(
            firstRegistry.GetStream("spawning.candidates"),
            firstRegistry.GetStream("spawning.rules"));
        var secondScheduler = new SpawnScheduler(
            secondRegistry.GetStream("spawning.candidates"),
            secondRegistry.GetStream("spawning.rules"));

        var firstResult = firstScheduler.Update(
            CreateInfiniteGroundWorld(-160, 160, 32),
            firstEntities,
            content,
            new BiomeMap("forest"),
            new WorldTime(),
            sources,
            0.1f,
            options);
        var secondResult = secondScheduler.Update(
            CreateInfiniteGroundWorld(-160, 160, 32),
            secondEntities,
            content,
            new BiomeMap("forest"),
            new WorldTime(),
            sources,
            0.1f,
            options);

        Assert.Equal(firstResult, secondResult);
        Assert.Equal(firstEntities.Entities.Count, secondEntities.Entities.Count);
        for (var index = 0; index < firstEntities.Entities.Count; index++)
        {
            var first = Assert.IsType<EnemyEntity>(firstEntities.Entities[index]);
            var second = Assert.IsType<EnemyEntity>(secondEntities.Entities[index]);
            Assert.Equal(first.DefinitionId, second.DefinitionId);
            Assert.Equal(first.Body.Position, second.Body.Position);
            Assert.Equal(first.SpawnRegion, second.SpawnRegion);
        }
    }

    [Fact]
    public void EnvironmentWeights_RequireMatchingLayerWeatherEventAndHabitat()
    {
        var rule = CreateRule(maxActive: 4) with
        {
            VerticalLayerWeights = new Dictionary<string, float> { ["surface"] = 1f },
            WeatherWeights = new Dictionary<string, float> { ["Storm"] = 1f },
            WorldEventWeights = new Dictionary<string, float> { ["blood_moon"] = 1f },
            HabitatWeights = new Dictionary<string, float> { ["Surface"] = 1f }
        };
        var content = CreateContent(rule);
        var world = CreateInfiniteGroundWorld(-64, 64, 20);
        var options = CreateScaleOptions(maxActive: 4) with { AttemptsPerInterval = 64 };
        var rejected = new[]
        {
            SpawnActivitySource.ForPlayer(
                1,
                new TilePos(0, 19),
                RectI.FromInclusiveTileBounds(-5, 14, 5, 24),
                new SpawnEnvironment("forest", "surface", "Clear", "blood_moon", 1f))
        };
        var accepted = new[]
        {
            rejected[0] with
            {
                Environment = new SpawnEnvironment("forest", "surface", "Storm", "blood_moon", 1f)
            }
        };

        var rejectedResult = new SpawnScheduler(new Random(5)).Update(
            world,
            new EntityManager(),
            content,
            new BiomeMap("forest"),
            new WorldTime(),
            rejected,
            0.1f,
            options);
        var acceptedResult = new SpawnScheduler(new Random(5)).Update(
            world,
            new EntityManager(),
            content,
            new BiomeMap("forest"),
            new WorldTime(),
            accepted,
            0.1f,
            options);

        Assert.Equal(0, rejectedResult.Spawned);
        Assert.True(acceptedResult.Spawned > 0);
    }

    [Fact]
    public void DirectPlacement_RejectsLiquidAndEnforcesLocalPopulationCap()
    {
        var world = CreateFiniteGroundWorld(96, 32, groundY: 10);
        var rule = CreateRule(maxActive: 8) with
        {
            PopulationGroup = "wildlife",
            LocalPopulationRadiusTiles = 16,
            MaxActiveInLocalArea = 1
        };
        var content = CreateContent(rule);
        var entities = new EntityManager();
        var system = new SpawnSystem(new Random(3));
        world.SetTile(4, 9, TileInstance.Liquid(255));

        var liquid = system.TrySpawn(
            world,
            entities,
            content,
            "forest",
            new WorldTime(),
            new TilePos(4, 9));
        var first = system.TrySpawn(
            world,
            entities,
            content,
            "forest",
            new WorldTime(),
            new TilePos(20, 9));
        var nearby = system.TrySpawn(
            world,
            entities,
            content,
            "forest",
            new WorldTime(),
            new TilePos(28, 9));
        var far = system.TrySpawn(
            world,
            entities,
            content,
            "forest",
            new WorldTime(),
            new TilePos(52, 9));

        Assert.False(liquid.Spawned);
        Assert.True(first.Spawned);
        Assert.False(nearby.Spawned);
        Assert.True(far.Spawned);
    }

    private static SpawnSchedulerOptions CreateScaleOptions(int maxActive)
    {
        return new SpawnSchedulerOptions
        {
            SpawnIntervalSeconds = 0.05f,
            AttemptsPerInterval = 256,
            MinDistanceTiles = 16,
            MaxDistanceTiles = 96,
            VerticalSearchRadiusTiles = 96,
            PlacementSearchRadiusTiles = 96,
            SectorCount = 12,
            MaxTotalActiveEnemies = maxActive,
            DespawnDistanceTiles = 512,
            OnScreenExclusionPaddingTiles = 3
        };
    }

    private static World CreateInfiniteGroundWorld(int minX, int maxX, int groundY)
    {
        var world = new World(
            GameConstants.ChunkSize,
            192,
            WorldMetadata.CreateDefault(872),
            isHorizontallyInfinite: true);
        for (var x = minX; x <= maxX; x++)
        {
            world.SetTile(x, groundY, KnownTileIds.Dirt);
        }

        return world;
    }

    private static World CreateFiniteGroundWorld(int width, int height, int groundY)
    {
        var world = new World(width, height, WorldMetadata.CreateDefault(872));
        for (var x = 0; x < width; x++)
        {
            world.SetTile(x, groundY, KnownTileIds.Dirt);
        }

        return world;
    }

    private static GameContentDatabase CreateContent(int maxActive)
    {
        return CreateContent(CreateRule(maxActive));
    }

    private static SpawnRuleDefinition CreateRule(int maxActive)
    {
        return new SpawnRuleDefinition
        {
            Id = "activity_squirrel",
            EntityId = "squirrel",
            BiomeId = "forest",
            Habitats = new[] { SpawnHabitat.Surface },
            Chance = 1f,
            Weight = 1f,
            MaxActive = maxActive,
            PopulationGroup = "wildlife",
            MaxActiveInGroup = maxActive,
            PopulationRegionSizeTiles = 64,
            MaxActiveInRegion = maxActive,
            MaxActiveInHabitat = maxActive
        };
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
                new EntityDefinition
                {
                    Id = "squirrel",
                    DisplayName = "Squirrel",
                    TexturePath = "entities/squirrel",
                    MaxHealth = 5,
                    Width = 12,
                    Height = 10,
                    Faction = EntityFaction.Friendly,
                    ContactDamage = 0,
                    ContactKnockback = 0
                }
            }),
            SpawnRuleRegistry.Create(rules));
    }
}
