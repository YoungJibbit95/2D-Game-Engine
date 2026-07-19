using System.Diagnostics;
using System.Numerics;
using Game.Core;
using Game.Core.Data;
using Game.Core.Entities;
using Game.Core.Physics;
using Game.Core.Spawning;
using Game.Core.Time;
using Game.Core.World;
using Xunit;
using Xunit.Abstractions;

namespace Game.Tests.SpawningTests;

[Collection(EntityReliabilityCollection.Name)]
public sealed class SpawnReliabilityPerformanceTests
{
    private const int EntityCount = 500;
    private const int WarmupTicks = 64;
    private const int MeasurementTicks = 180;
    private const float FixedDeltaSeconds = 1f / 60f;
    private readonly ITestOutputHelper _output;

    public SpawnReliabilityPerformanceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void FiveHundredEntityCapMaintenance_StaysInsideCpuAndAllocationGates()
    {
        var contentResult = new GameContentLoader().LoadWithMods(FindGameDataRoot(), modsRoot: null);
        Assert.False(contentResult.Report.HasErrors);
        var definition = contentResult.Database.Entities.Definitions
            .First(candidate => candidate.Ai is not null || !string.IsNullOrWhiteSpace(candidate.AiBehavior));
        var collision = new TileCollisionResolver();
        var factory = new EntityFactory(collision);
        var entities = new EntityManager(64);
        for (var index = 0; index < EntityCount; index++)
        {
            entities.Add(factory.CreateEnemy(
                definition,
                new Vector2((index - EntityCount / 2) * GameConstants.TileSize, 64)));
        }

        var world = new World(
            GameConstants.ChunkSize,
            160,
            WorldMetadata.CreateDefault(4_117),
            isHorizontallyInfinite: true);
        var scheduler = new SpawnScheduler(new Random(91));
        var sources = new[]
        {
            SpawnActivitySource.ForPlayer(
                1,
                new TilePos(0, 4),
                RectI.FromInclusiveTileBounds(-16, -6, 16, 14),
                new SpawnEnvironment("forest", "surface", "Clear", null, 1f))
        };
        var options = new SpawnSchedulerOptions
        {
            SpawnIntervalSeconds = FixedDeltaSeconds,
            AttemptsPerInterval = 32,
            MaxTotalActiveEnemies = EntityCount,
            DespawnDistanceTiles = 4_096
        };
        var time = new WorldTime();
        time.SetDay();
        var biomeMap = new Game.Core.Biomes.BiomeMap("forest");
        for (var tick = 0; tick < WarmupTicks; tick++)
        {
            scheduler.Update(
                world,
                entities,
                contentResult.Database,
                biomeMap,
                time,
                sources,
                FixedDeltaSeconds,
                options);
        }

        var samples = new double[MeasurementTicks];
        var totalAttempts = 0;
        var totalSpawned = 0;
        var totalDespawned = 0;
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        for (var tick = 0; tick < MeasurementTicks; tick++)
        {
            var startedAt = Stopwatch.GetTimestamp();
            var result = scheduler.Update(
                world,
                entities,
                contentResult.Database,
                biomeMap,
                time,
                sources,
                FixedDeltaSeconds,
                options);
            samples[tick] = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
            totalAttempts += result.Attempts;
            totalSpawned += result.Spawned;
            totalDespawned += result.Despawned;
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        var bytesPerTick = allocated / (double)MeasurementTicks;
        Array.Sort(samples);
        var p99 = samples[(int)Math.Ceiling(samples.Length * 0.99) - 1];
        _output.WriteLine(
            $"500 entity spawn cap: p99={p99:F3} ms, allocated={allocated} B, perTick={bytesPerTick:F1} B");

        Assert.True(
            p99 <= 2 && bytesPerTick <= 512,
            $"p99={p99:F3} ms, allocated={allocated} B, perTick={bytesPerTick:F1} B");
        Assert.Equal(0, totalAttempts);
        Assert.Equal(0, totalSpawned);
        Assert.Equal(0, totalDespawned);
        Assert.Equal(EntityCount, entities.Entities.Count);
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
