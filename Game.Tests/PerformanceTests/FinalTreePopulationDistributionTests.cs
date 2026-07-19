using System.Diagnostics;
using System.Numerics;
using Game.Core;
using Game.Core.Biomes;
using Game.Core.Data;
using Game.Core.Diagnostics.Performance;
using Game.Core.Entities;
using Game.Core.Physics;
using Game.Core.Spawning;
using Game.Core.Time;
using Game.Core.World;
using Game.Core.World.Generation;
using Xunit;

namespace Game.Tests.PerformanceTests;

[Collection(LongSessionPerformanceCollection.Name)]
public sealed class FinalTreePopulationDistributionTests
{
    private const int EntityCount = 200;
    private const int WarmupTicks = 180;
    private const int MeasuredTicks = 720;
    private const float FixedDeltaSeconds = 1f / 60f;

    [Fact]
    public void TwoHundredEntities_SpawnMaintenanceAndAiStayInsideDistributionBudgets()
    {
        var content = LoadFinalTreeContent();
        var profile = WorldGenerationProfile.Small;
        const int seed = 81772;
        var generator = new InfiniteWorldChunkGenerator();
        var world = generator.CreateWorld(profile, seed, "Final Tree Population Distribution");
        GeneratePopulationArea(generator, world, profile);

        var collision = new TileCollisionResolver();
        var entities = CreatePopulation(content, generator, profile, seed, collision);
        var playerSurfaceY = generator.GetSurfaceHeightAt(profile, seed, 0);
        var player = new PlayerEntity(
            new Vector2(0, (playerSurfaceY - 2) * GameConstants.TileSize),
            collision);
        var sources = CreateActivitySources(generator, profile, seed);
        var scheduler = new SpawnScheduler(new Random(1977));
        var spawnOptions = new SpawnSchedulerOptions
        {
            SpawnIntervalSeconds = FixedDeltaSeconds,
            AttemptsPerInterval = 32,
            MinDistanceTiles = 18,
            MaxDistanceTiles = 96,
            VerticalSearchRadiusTiles = 32,
            PlacementSearchRadiusTiles = 32,
            SectorCount = 16,
            MaxTotalActiveEnemies = EntityCount,
            DespawnDistanceTiles = 4096,
            OnScreenExclusionPaddingTiles = 3
        };
        var biomeMap = new BiomeMap("forest");
        var worldTime = new WorldTime();

        for (var tick = 0; tick < WarmupTicks; tick++)
        {
            scheduler.Update(
                world,
                entities,
                content,
                biomeMap,
                worldTime,
                sources,
                FixedDeltaSeconds,
                spawnOptions);
            entities.UpdateAll(world, FixedDeltaSeconds, player, isNight: false, tick);
        }

        Assert.Equal(EntityCount, entities.Entities.Count);
        var spawnDistribution = new LongSessionDistributionCollector(
            LongSessionDistributionLabels.SpawnFinalTreeTwoHundredMaintenanceMilliseconds,
            capacity: 512,
            budget: 4);
        var aiDistribution = new LongSessionDistributionCollector(
            LongSessionDistributionLabels.AiFinalTreeTwoHundredUpdateMilliseconds,
            capacity: 512,
            budget: 8);
        var totalSpawned = 0;

        for (var tick = 0; tick < MeasuredTicks; tick++)
        {
            var spawnStartedAt = Stopwatch.GetTimestamp();
            var spawnResult = scheduler.Update(
                world,
                entities,
                content,
                biomeMap,
                worldTime,
                sources,
                FixedDeltaSeconds,
                spawnOptions);
            spawnDistribution.Add(Stopwatch.GetElapsedTime(spawnStartedAt).TotalMilliseconds);
            totalSpawned += spawnResult.Spawned;

            var aiStartedAt = Stopwatch.GetTimestamp();
            entities.UpdateAll(
                world,
                FixedDeltaSeconds,
                player,
                isNight: (tick / 180) % 2 != 0,
                tickNumber: WarmupTicks + tick);
            aiDistribution.Add(Stopwatch.GetElapsedTime(aiStartedAt).TotalMilliseconds);
        }

        var spawn = spawnDistribution.Capture();
        var ai = aiDistribution.Capture();
        Assert.Equal(0, totalSpawned);
        Assert.Equal(EntityCount, entities.Entities.Count);
        Assert.All(entities.Entities, entity => Assert.True(entity.IsActive));
        Assert.Contains(
            entities.Entities,
            entity => entity is EnemyEntity enemy && enemy.AiTelemetry.UpdateCount >= WarmupTicks + MeasuredTicks);
        Assert.Equal(MeasuredTicks, spawn.TotalSampleCount);
        Assert.Equal(MeasuredTicks, ai.TotalSampleCount);
        Assert.Equal(512, spawn.RetainedSampleCount);
        Assert.Equal(512, ai.RetainedSampleCount);
        Assert.Equal(LongSessionDistributionLabels.SpawnFinalTreeTwoHundredMaintenanceMilliseconds, spawn.Label);
        Assert.Equal(LongSessionDistributionLabels.AiFinalTreeTwoHundredUpdateMilliseconds, ai.Label);
        AssertOrdered(spawn);
        AssertOrdered(ai);
        Assert.True(spawn.P99 <= spawn.Budget, $"spawn p99={spawn.P99:0.###} ms");
        Assert.True(ai.P99 <= ai.Budget, $"ai p99={ai.P99:0.###} ms");
        Assert.True(spawn.OverBudgetRatio <= 0.01, $"spawn over-budget={spawn.OverBudgetRatio:P2}");
        Assert.True(ai.OverBudgetRatio <= 0.01, $"ai over-budget={ai.OverBudgetRatio:P2}");

        LongSessionDistributionArtifactExporter.ExportIfRequested(
            "simulation.final-tree-200",
            spawn,
            ai);
    }

    private static EntityManager CreatePopulation(
        GameContentDatabase content,
        InfiniteWorldChunkGenerator generator,
        WorldGenerationProfile profile,
        int seed,
        TileCollisionResolver collision)
    {
        var definitions = content.Entities.Definitions
            .Where(definition => definition.Ai is not null || !string.IsNullOrWhiteSpace(definition.AiBehavior))
            .OrderBy(definition => definition.Id, StringComparer.Ordinal)
            .ToArray();
        if (definitions.Length == 0)
        {
            throw new InvalidOperationException("Final-tree content does not contain AI-backed entity definitions.");
        }

        var entities = new EntityManager(64);
        var factory = new EntityFactory(collision);
        for (var index = 0; index < EntityCount; index++)
        {
            var tileX = -224 + ((448 * index) / (EntityCount - 1));
            var surfaceY = generator.GetSurfaceHeightAt(profile, seed, tileX);
            var definition = definitions[index % definitions.Length];
            var position = new Vector2(
                tileX * GameConstants.TileSize,
                (surfaceY * GameConstants.TileSize) - definition.Height - 1);
            entities.Add(factory.CreateEnemy(definition, position));
        }

        return entities;
    }

    private static SpawnActivitySource[] CreateActivitySources(
        InfiniteWorldChunkGenerator generator,
        WorldGenerationProfile profile,
        int seed)
    {
        var negativeSurfaceY = generator.GetSurfaceHeightAt(profile, seed, -80);
        var positiveSurfaceY = generator.GetSurfaceHeightAt(profile, seed, 80);
        return
        [
            SpawnActivitySource.ForPlayer(
                1,
                new TilePos(-80, negativeSurfaceY - 1),
                RectI.FromInclusiveTileBounds(-104, negativeSurfaceY - 16, -56, negativeSurfaceY + 10),
                new SpawnEnvironment("forest", "surface", "Clear", null, 1f)),
            SpawnActivitySource.ForCamera(
                2,
                new TilePos(80, positiveSurfaceY - 1),
                RectI.FromInclusiveTileBounds(56, positiveSurfaceY - 16, 104, positiveSurfaceY + 10),
                new SpawnEnvironment("forest", "surface", "Clear", null, 1f))
        ];
    }

    private static void GeneratePopulationArea(
        InfiniteWorldChunkGenerator generator,
        World world,
        WorldGenerationProfile profile)
    {
        var maximumChunkY = CoordinateUtils.TileToChunk(0, profile.HeightTiles - 1).Y;
        for (var chunkX = -8; chunkX <= 8; chunkX++)
        {
            for (var chunkY = 0; chunkY <= maximumChunkY; chunkY++)
            {
                generator.EnsureChunk(world, profile, new ChunkPos(chunkX, chunkY));
            }
        }

        world.ClearAllDirtyFlags();
    }

    private static GameContentDatabase LoadFinalTreeContent()
    {
        var result = new GameContentLoader().LoadWithMods(FindGameDataRoot(), modsRoot: null);
        if (result.Report.HasErrors)
        {
            throw new InvalidOperationException(
                "Final-tree content is invalid: " +
                string.Join(" | ", result.Report.Issues.Select(issue => issue.Message)));
        }

        return result.Database;
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

    private static void AssertOrdered(LongSessionDistributionSnapshot snapshot)
    {
        Assert.True(snapshot.P50 <= snapshot.P95);
        Assert.True(snapshot.P95 <= snapshot.P99);
        Assert.True(snapshot.P99 <= snapshot.Maximum);
    }
}
