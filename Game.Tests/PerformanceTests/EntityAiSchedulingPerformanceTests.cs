using System.Diagnostics;
using System.Numerics;
using Game.Core;
using Game.Core.Combat;
using Game.Core.Entities;
using Game.Core.Entities.AI;
using Game.Core.Physics;
using Game.Core.World;
using Xunit;
using Xunit.Abstractions;

namespace Game.Tests.PerformanceTests;

public sealed class EntityAiSchedulingPerformanceTests
{
    private const float FixedDeltaSeconds = 1f / 60f;
    private readonly ITestOutputHelper _output;

    public EntityAiSchedulingPerformanceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Theory]
    [InlineData(500)]
    [InlineData(2_000)]
    public void BudgetedDecisionsKeepAuthoritativePhysicsAtScaleWithoutAllocations(int actorCount)
    {
        const int warmupTicks = 32;
        const int measurementTicks = 120;
        var fullRate = CreatePopulation(actorCount, new EntityAiSchedulingOptions
        {
            DecisionBudgetPerTick = 256,
            FullRatePopulationThreshold = actorCount
        });
        var budgeted = CreatePopulation(actorCount, options: null);

        for (var tick = 0; tick < warmupTicks; tick++)
        {
            fullRate.Manager.UpdateAll(fullRate.World, FixedDeltaSeconds, fullRate.Player, isNight: false, tick);
            budgeted.Manager.UpdateAll(budgeted.World, FixedDeltaSeconds, budgeted.Player, isNight: false, tick);
        }

        var fullRateMeasurement = Measure(fullRate, warmupTicks, measurementTicks);
        var budgetedMeasurement = Measure(budgeted, warmupTicks, measurementTicks);
        _output.WriteLine(
            $"actors={actorCount} fullRateAvgMs={fullRateMeasurement.AverageMilliseconds:F3} " +
            $"budgetedAvgMs={budgetedMeasurement.AverageMilliseconds:F3} " +
            $"improvement={(1 - budgetedMeasurement.AverageMilliseconds / fullRateMeasurement.AverageMilliseconds) * 100:F1}% " +
            $"budgetedP99={budgetedMeasurement.P99Milliseconds:F3} " +
            $"fullRateDecisions={fullRateMeasurement.DecisionsPerTick:F1} " +
            $"budgetedDecisions={budgetedMeasurement.DecisionsPerTick:F1} " +
            $"allocatedPerTick={budgetedMeasurement.AllocatedBytes / (double)measurementTicks:F1} " +
            $"physicsBodies={budgeted.Manager.PhysicsTelemetryLastUpdate.BodiesSimulated}");

        Assert.Equal(0, fullRateMeasurement.AllocatedBytes);
        Assert.Equal(0, budgetedMeasurement.AllocatedBytes);
        Assert.True(
            budgetedMeasurement.AverageMilliseconds < fullRateMeasurement.AverageMilliseconds,
            $"actors={actorCount}, full={fullRateMeasurement.AverageMilliseconds:F3} ms, " +
            $"budgeted={budgetedMeasurement.AverageMilliseconds:F3} ms");
#if DEBUG
        const double p99BudgetMilliseconds = 25.0;
#else
        var p99BudgetMilliseconds = actorCount == 500 ? 3.0 : 6.0;
#endif
        Assert.True(
            budgetedMeasurement.P99Milliseconds <= p99BudgetMilliseconds,
            $"actors={actorCount}, p99={budgetedMeasurement.P99Milliseconds:F3} ms");
        Assert.Equal(actorCount, fullRateMeasurement.DecisionsPerTick);
        Assert.InRange(
            budgetedMeasurement.DecisionsPerTick,
            1,
            budgeted.Manager.AiSchedulingOptions.DecisionBudgetPerTick);
        Assert.InRange(
            budgeted.Manager.AiSchedulingTelemetryLastUpdate.DecisionsScheduled,
            0,
            budgeted.Manager.AiSchedulingOptions.DecisionBudgetPerTick);
        Assert.Equal(actorCount, fullRate.Manager.PhysicsTelemetryLastUpdate.BodiesSimulated);
        Assert.Equal(actorCount, budgeted.Manager.AiSchedulingTelemetryLastUpdate.PhysicsBodiesSubmitted);
        Assert.Equal(actorCount, budgeted.Manager.PhysicsTelemetryLastUpdate.BodiesSimulated);
    }

    private static Measurement Measure(Population population, int firstTick, int tickCount)
    {
        var decisionsBefore = CountDecisions(population.Behaviors);
        var samples = new double[tickCount];
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var startedAt = Stopwatch.GetTimestamp();
        for (var tick = 0; tick < tickCount; tick++)
        {
            var tickStartedAt = Stopwatch.GetTimestamp();
            population.Manager.UpdateAll(
                population.World,
                FixedDeltaSeconds,
                population.Player,
                isNight: false,
                firstTick + tick);
            samples[tick] = Stopwatch.GetElapsedTime(tickStartedAt).TotalMilliseconds;
        }

        var elapsed = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        var decisions = CountDecisions(population.Behaviors) - decisionsBefore;
        Array.Sort(samples);
        return new Measurement(
            elapsed / tickCount,
            samples[(int)Math.Ceiling(samples.Length * 0.99) - 1],
            allocated,
            decisions / (double)tickCount);
    }

    private static Population CreatePopulation(int actorCount, EntityAiSchedulingOptions? options)
    {
        var world = new World(actorCount + 96, 32, WorldMetadata.CreateDefault(8_119));
        var collision = new TileCollisionResolver();
        var entities = new EntityManager(64, collision, aiSchedulingOptions: options);
        var player = new PlayerEntity(new Vector2(16, 16), collision);
        var behaviors = new CountingWorkAiBehavior[actorCount];
        for (var index = 0; index < actorCount; index++)
        {
            var behavior = new CountingWorkAiBehavior();
            entities.Add(CreateActor(index, collision, behavior));
            behaviors[index] = behavior;
        }

        return new Population(world, entities, player, behaviors);
    }

    private static long CountDecisions(IReadOnlyList<CountingWorkAiBehavior> behaviors)
    {
        var count = 0L;
        for (var index = 0; index < behaviors.Count; index++)
        {
            count += behaviors[index].UpdateCount;
        }

        return count;
    }

    private static EnemyEntity CreateActor(
        int index,
        TileCollisionResolver collision,
        CountingWorkAiBehavior behavior)
    {
        return new EnemyEntity(
            "scheduler-perf",
            new Vector2((index + 64) * GameConstants.TileSize, 32),
            new Vector2(12, 10),
            new HealthComponent(10),
            behavior,
            collision,
            movementMode: EntityMovementMode.Flying);
    }

    private sealed class CountingWorkAiBehavior : IAiBehavior
    {
        private uint _state = 0x9E3779B9u;

        public AiState CurrentState => AiState.Wander;

        public int? TargetEntityId => null;

        public long UpdateCount { get; private set; }

        public void Update(EnemyEntity entity, AiUpdateContext context, float deltaSeconds)
        {
            UpdateCount++;
            var state = _state ^ unchecked((uint)entity.Id) ^ unchecked((uint)context.TickNumber);
            for (var iteration = 0; iteration < 1_024; iteration++)
            {
                state ^= state << 13;
                state ^= state >> 17;
                state ^= state << 5;
            }

            _state = state;
            entity.Body.Velocity = new Vector2((state & 1) == 0 ? -24 : 24, 0);
        }

        public bool TryConsumeAttackIntent(out AiAttackIntent intent)
        {
            intent = default;
            return false;
        }
    }

    private sealed record Population(
        World World,
        EntityManager Manager,
        PlayerEntity Player,
        CountingWorkAiBehavior[] Behaviors);

    private readonly record struct Measurement(
        double AverageMilliseconds,
        double P99Milliseconds,
        long AllocatedBytes,
        double DecisionsPerTick);
}
