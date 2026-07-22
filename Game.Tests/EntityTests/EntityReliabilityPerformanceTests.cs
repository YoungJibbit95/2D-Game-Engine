using System.Diagnostics;
using System.Numerics;
using Game.Core;
using Game.Core.Combat;
using Game.Core.Entities;
using Game.Core.Entities.AI;
using Game.Core.Physics;
using Game.Core.Tiles;
using Game.Core.World;
using Xunit;
using Xunit.Abstractions;

namespace Game.Tests.EntityTests;

[Collection(EntityReliabilityCollection.Name)]
public sealed class EntityReliabilityPerformanceTests
{
    private const int EntityCount = 500;
    private const int WarmupTicks = 64;
    private const int MeasurementTicks = 180;
    private const float FixedDeltaSeconds = 1f / 60f;
    private readonly ITestOutputHelper _output;

    public EntityReliabilityPerformanceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void FiveHundredEntityManagerUpdates_StayInsideCpuAndAllocationGates()
    {
        const int measurementRuns = 3;
        var world = CreateWorld();
        var entities = CreatePopulation();
        for (var tick = 0; tick < WarmupTicks; tick++)
        {
            entities.UpdateAll(world, FixedDeltaSeconds, player: null, isNight: false, tick);
        }

        var samples = new double[MeasurementTicks];
        var runP99Milliseconds = new double[measurementRuns];
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        for (var run = 0; run < measurementRuns; run++)
        {
            for (var tick = 0; tick < MeasurementTicks; tick++)
            {
                var absoluteTick = WarmupTicks + run * MeasurementTicks + tick;
                var startedAt = Stopwatch.GetTimestamp();
                entities.UpdateAll(
                    world,
                    FixedDeltaSeconds,
                    player: null,
                    isNight: (absoluteTick / 60) % 2 != 0,
                    tickNumber: absoluteTick);
                samples[tick] = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
            }

            Array.Sort(samples);
            runP99Milliseconds[run] = samples[(int)Math.Ceiling(samples.Length * 0.99) - 1];
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        var bytesPerTick = allocated / (double)(MeasurementTicks * measurementRuns);
        Array.Sort(runP99Milliseconds);
        var p99 = runP99Milliseconds[measurementRuns / 2];
        _output.WriteLine(
            $"500 entities: median-run p99={p99:F3} ms, " +
            $"runs={string.Join('/', runP99Milliseconds.Select(value => value.ToString("F3")))}, " +
            $"allocated={allocated} B, perTick={bytesPerTick:F1} B");

        Assert.True(
            p99 <= 12 && bytesPerTick <= 1 * 1024,
            $"median-run p99={p99:F3} ms, allocated={allocated} B, perTick={bytesPerTick:F1} B");
        Assert.Equal(EntityCount, entities.Entities.Count);
        var expectedUpdates = WarmupTicks + MeasurementTicks * measurementRuns;
        Assert.All(
            entities.Entities,
            entity => Assert.True(
                entity is EnemyEntity { IsActive: true } actor && actor.AiTelemetry.UpdateCount == expectedUpdates));
    }
    private static EntityManager CreatePopulation()
    {
        var entities = new EntityManager(64);
        var collision = new TileCollisionResolver();
        for (var index = 0; index < EntityCount; index++)
        {
            var faction = (index & 1) == 0 ? EntityFaction.Friendly : EntityFaction.Hostile;
            var profile = new AiProfileDefinition
            {
                Kind = faction == EntityFaction.Friendly ? AiBehaviorKind.Critter : AiBehaviorKind.Hostile,
                DetectionRange = 96,
                LoseTargetRange = 128,
                MoveSpeed = 28,
                FleeSpeed = 56,
                AttackRange = 10,
                AttackCooldown = 0.5f,
                PatrolRadius = 64,
                FlockRadius = 64,
                FlockWeight = 0.5f,
                MinFlockSize = 2,
                DecisionInterval = 0.25f,
                IdleChance = 0,
                RequiresLineOfSight = false,
                AvoidLedges = false,
                AvoidLiquid = false
            };
            IAiBehavior behavior = faction == EntityFaction.Friendly
                ? new CritterAiBehavior(profile)
                : new HostileAiBehavior(profile);
            entities.Add(new EnemyEntity(
                $"scale-{faction.ToString().ToLowerInvariant()}",
                new Vector2((index + 8) * GameConstants.TileSize, 64),
                new Vector2(12, 10),
                new HealthComponent(20),
                behavior,
                collision,
                contactDamage: faction == EntityFaction.Hostile ? 2 : 0,
                faction: faction,
                movementMode: EntityMovementMode.Flying));
        }

        return entities;
    }

    private static World CreateWorld()
    {
        var world = new World(1_024, 64, WorldMetadata.CreateDefault(8_117));
        for (var tileX = 0; tileX < world.WidthTiles; tileX++)
        {
            world.SetTile(tileX, 10, KnownTileIds.Dirt);
        }

        return world;
    }
}
