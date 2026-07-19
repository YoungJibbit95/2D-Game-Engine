using System.Diagnostics;
using System.Numerics;
using Game.Core.Biomes;
using Game.Core.Crafting;
using Game.Core.Data;
using Game.Core.Entities;
using Game.Core.Items;
using Game.Core.Inventory;
using Game.Core.Loot;
using Game.Core.Physics;
using Game.Core.Projectiles;
using Game.Core.Runtime;
using Game.Core.Spawning;
using Game.Core.Tiles;
using Game.Core.World;
using Xunit;
using Xunit.Abstractions;

namespace Game.Tests.PerformanceTests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class RuntimeSnapshotPerformanceCollection
{
    public const string Name = "Runtime snapshot performance";
}

[Collection(RuntimeSnapshotPerformanceCollection.Name)]
public sealed class RuntimeSnapshotPerformanceTests
{
    private const int EntityCount = 500;
    private const int WarmupTicks = 64;
    private const int MeasurementTicks = 1_000;
    private const float FixedDeltaSeconds = 1f / 60f;
    private readonly ITestOutputHelper _output;

    public RuntimeSnapshotPerformanceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void EmptyRuntimeSnapshot_RecordsAllocationAndLatencyDistribution()
    {
        using var simulation = CreateSimulation(entityCount: 0);

        var measurement = Measure(simulation);

        _output.WriteLine(measurement.ToString());
        Assert.True(measurement.BytesPerTick <= 2 * 1024, measurement.ToString());
        Assert.True(measurement.SnapshotP99Microseconds <= 2_000, measurement.ToString());
    }

    [Fact]
    public void FiveHundredMovingEntitySnapshot_RecordsAllocationAndLatencyDistribution()
    {
        using var simulation = CreateSimulation(EntityCount);

        var measurement = Measure(simulation);

        _output.WriteLine(measurement.ToString());
        Assert.Equal(EntityCount, simulation.LatestSnapshot.Entities.Count);
        Assert.True(measurement.BytesPerTick <= 8 * 1024, measurement.ToString());
        Assert.True(measurement.SnapshotP99Microseconds <= 10_000, measurement.ToString());
    }

    [Fact]
    public void PublishedSnapshots_RemainIsolatedAcrossSubsequentMovingFrames()
    {
        using var simulation = CreateSimulation(entityCount: 8);
        var published = simulation.Tick(PlayerCommand.None, FixedDeltaSeconds).Snapshot;
        var publishedEntities = published.Entities.ToArray();
        var publishedHotbar = published.Player.Hotbar.ToArray();

        for (var tick = 0; tick < 240; tick++)
        {
            simulation.Tick(PlayerCommand.None, FixedDeltaSeconds);
        }

        Assert.Equal(publishedEntities, published.Entities);
        Assert.Equal(publishedHotbar, published.Player.Hotbar);
        Assert.NotSame(published.Entities, simulation.LatestSnapshot.Entities);
        Assert.NotEqual(published.Entities[0].Position, simulation.LatestSnapshot.Entities[0].Position);
        Assert.Equal(published.Entities[0].Bounds, publishedEntities[0].Bounds);
    }

    [Fact]
    public void UnchangedEntityFrames_ReuseThePublishedImmutableSequence()
    {
        using var simulation = CreateSimulation(entityCount: 32, moving: false);

        var first = simulation.Tick(PlayerCommand.None, FixedDeltaSeconds).Snapshot;
        var second = simulation.Tick(PlayerCommand.None, FixedDeltaSeconds).Snapshot;

        Assert.Same(first.Entities, second.Entities);
        Assert.Equal(first.Entities, second.Entities);
    }

    [Fact]
    public void RuntimeSnapshotsAndReplayCheckpoints_AreDeterministicAcrossSessions()
    {
        using var first = CreateSimulation(entityCount: 64);
        using var second = CreateSimulation(entityCount: 64);
        var replayOptions = new Game.Core.Diagnostics.Replay.ReplayCaptureOptions
        {
            FrameCapacity = 256,
            CheckpointIntervalTicks = 20
        };
        first.StartReplayCapture(replayOptions);
        second.StartReplayCapture(replayOptions);

        for (var tick = 0; tick < 240; tick++)
        {
            var firstResult = first.Tick(PlayerCommand.None, FixedDeltaSeconds);
            var secondResult = second.Tick(PlayerCommand.None, FixedDeltaSeconds);
            Assert.Equal(firstResult.Snapshot.TickNumber, secondResult.Snapshot.TickNumber);
            Assert.Equal(
                firstResult.Snapshot.Player with
                {
                    Hotbar = ImmutableSnapshotList<InventorySlotFrameSnapshot>.Empty
                },
                secondResult.Snapshot.Player with
                {
                    Hotbar = ImmutableSnapshotList<InventorySlotFrameSnapshot>.Empty
                });
            Assert.Equal(
                firstResult.Snapshot.Player.Hotbar.ToArray(),
                secondResult.Snapshot.Player.Hotbar.ToArray());
            Assert.Equal(firstResult.Snapshot.WorldTime, secondResult.Snapshot.WorldTime);
            Assert.Equal(firstResult.Snapshot.LivingWorld, secondResult.Snapshot.LivingWorld);
            Assert.Equal(
                firstResult.Snapshot.Entities.ToArray(),
                secondResult.Snapshot.Entities.ToArray());
        }

        var firstReplay = Assert.IsType<Game.Core.Diagnostics.Replay.ReplayRecordingSnapshot>(
            first.StopReplayCapture());
        var secondReplay = Assert.IsType<Game.Core.Diagnostics.Replay.ReplayRecordingSnapshot>(
            second.StopReplayCapture());
        Assert.Equal(firstReplay.Frames.ToArray(), secondReplay.Frames.ToArray());
    }

    private static RuntimeSnapshotMeasurement Measure(GameSimulation simulation)
    {
        simulation.ConfigureOptions(simulation.Options with
        {
            AutoPickupItems = false,
            EnablePhaseTelemetry = true
        });
        for (var tick = 0; tick < WarmupTicks; tick++)
        {
            simulation.Tick(PlayerCommand.None, FixedDeltaSeconds);
        }

        simulation.PhaseTelemetry.Reset();
        var snapshotMicroseconds = new double[MeasurementTicks];
        var tickMicroseconds = new double[MeasurementTicks];
        for (var tick = 0; tick < MeasurementTicks; tick++)
        {
            var startedAt = Stopwatch.GetTimestamp();
            simulation.Tick(PlayerCommand.None, FixedDeltaSeconds);
            tickMicroseconds[tick] = Stopwatch.GetElapsedTime(startedAt).TotalMicroseconds;

            var telemetry = simulation.PhaseTelemetry.CaptureSnapshot();
            Assert.True(telemetry.TryGet(GameSimulationPhase.FrameSnapshot, out var phase));
            snapshotMicroseconds[tick] = phase.LastElapsedTicks * 1_000_000d / telemetry.TimestampFrequency;
        }

        var finalTelemetry = simulation.PhaseTelemetry.CaptureSnapshot();
        Assert.True(finalTelemetry.TryGet(GameSimulationPhase.FrameSnapshot, out var snapshotPhase));
        Array.Sort(snapshotMicroseconds);
        Array.Sort(tickMicroseconds);
        return new RuntimeSnapshotMeasurement(
            snapshotMicroseconds[PercentileIndex(0.95)],
            snapshotMicroseconds[PercentileIndex(0.99)],
            tickMicroseconds[PercentileIndex(0.95)],
            tickMicroseconds[PercentileIndex(0.99)],
            snapshotPhase.AverageAllocatedBytes);
    }

    private static int PercentileIndex(double percentile)
    {
        return Math.Clamp((int)Math.Ceiling(MeasurementTicks * percentile) - 1, 0, MeasurementTicks - 1);
    }

    private static GameSimulation CreateSimulation(int entityCount, bool moving = true)
    {
        var content = CreateContent();
        var entities = new EntityManager(spatialCellSize: 32);
        for (var index = 0; index < entityCount; index++)
        {
            entities.Add(new SnapshotProbeEntity(
                new Vector2(48 + index * 18, 96 + index % 5),
                moving
                    ? new Vector2((index & 1) == 0 ? 7.25f : -6.5f, 0.125f)
                    : Vector2.Zero));
        }

        return new GameSimulation(
            content,
            new World(1_024, 64, WorldMetadata.CreateDefault(seed: 81_117)),
            new BiomeMap("forest"),
            new PlayerEntity(new Vector2(32, 32), new TileCollisionResolver()),
            new PlayerInventory(content.Items),
            entities,
            spawnOptions: new SpawnSchedulerOptions { MaxTotalActiveEnemies = 0 });
    }

    private static GameContentDatabase CreateContent()
    {
        return new GameContentDatabase(
            TileRegistry.Create(Array.Empty<TileDefinition>()),
            ItemRegistry.Create(Array.Empty<ItemDefinition>()),
            RecipeRegistry.Create(Array.Empty<RecipeDefinition>()),
            LootTableRegistry.Create(Array.Empty<LootTableDefinition>()),
            BiomeRegistry.Create(
            [
                new BiomeDefinition
                {
                    Id = "forest",
                    DisplayName = "Forest",
                    SurfaceTile = "air",
                    UndergroundTile = "air"
                }
            ]),
            ProjectileRegistry.Create(Array.Empty<ProjectileDefinition>()),
            EntityDefinitionRegistry.Create(Array.Empty<EntityDefinition>()),
            SpawnRuleRegistry.Create(Array.Empty<SpawnRuleDefinition>()));
    }

    private readonly record struct RuntimeSnapshotMeasurement(
        double SnapshotP95Microseconds,
        double SnapshotP99Microseconds,
        double TickP95Microseconds,
        double TickP99Microseconds,
        double BytesPerTick)
    {
        public override string ToString()
        {
            return $"snapshot p95={SnapshotP95Microseconds:F3} us, p99={SnapshotP99Microseconds:F3} us, " +
                   $"tick p95={TickP95Microseconds:F3} us, p99={TickP99Microseconds:F3} us, " +
                   $"snapshot={BytesPerTick:F1} B/tick";
        }
    }

    private sealed class SnapshotProbeEntity : Entity
    {
        private readonly Vector2 _velocity;

        public SnapshotProbeEntity(Vector2 position, Vector2 velocity)
        {
            Position = position;
            _velocity = velocity;
        }

        public override RectI Bounds => new((int)Position.X, (int)Position.Y, 12, 14);

        public override void Update(World world, float deltaSeconds)
        {
            Position += _velocity * deltaSeconds;
        }
    }
}
