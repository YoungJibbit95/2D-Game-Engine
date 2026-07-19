using System.Numerics;
using Game.Core;
using Game.Core.Combat;
using Game.Core.Entities;
using Game.Core.Entities.AI;
using Game.Core.Physics;
using Game.Core.World;
using Xunit;

namespace Game.Tests.EntityTests;

public sealed class EntityAiDecisionSchedulerTests
{
    private const float FixedDeltaSeconds = 1f / 60f;

    [Fact]
    public void SmallPopulationPreservesFullRateDecisions()
    {
        var fixture = CreateFixture(actorCount: 4, new EntityAiSchedulingOptions
        {
            DecisionBudgetPerTick = 4,
            FullRatePopulationThreshold = 4
        });

        fixture.Manager.UpdateAll(fixture.World, FixedDeltaSeconds, fixture.Player, false, tickNumber: 0);

        Assert.All(fixture.Behaviors, behavior => Assert.Equal(1, behavior.UpdateCount));
        Assert.Equal(4, fixture.Manager.AiSchedulingTelemetryLastUpdate.FullRateDecisions);
        Assert.Equal(4, fixture.Manager.PhysicsTelemetryLastUpdate.BodiesSimulated);
    }

    [Fact]
    public void NearAndEngagedActorsRunEveryTickEvenWhenMandatoryWorkExceedsBudget()
    {
        var options = CreateConstrainedOptions(decisionBudget: 1);
        var fixture = CreateFixture(actorCount: 8, options);
        fixture.Actors[0].Body.Position = new Vector2(32, 32);
        fixture.Behaviors[1].SetEngagedTarget(fixture.Player.Id == 0 ? 77 : fixture.Player.Id);

        for (var tick = 0; tick < 6; tick++)
        {
            fixture.Manager.UpdateAll(fixture.World, FixedDeltaSeconds, fixture.Player, false, tick);
        }

        var telemetry = fixture.Manager.AiSchedulingTelemetryLastUpdate;
        Assert.Equal(6, fixture.Behaviors[0].UpdateCount);
        Assert.Equal(6, fixture.Behaviors[1].UpdateCount);
        Assert.Equal(1, telemetry.NearDecisions);
        Assert.Equal(1, telemetry.EngagedDecisions);
        Assert.Equal(2, telemetry.DecisionsScheduled);
        Assert.Equal(1, telemetry.BudgetOverrun);
        Assert.True(telemetry.Overloaded);
        Assert.Equal(8, telemetry.PhysicsBodiesSubmitted);
        Assert.Equal(8, fixture.Manager.PhysicsTelemetryLastUpdate.BodiesSimulated);
    }

    [Fact]
    public void TickAndEntityPriorityIsDeterministicAcrossEquivalentManagers()
    {
        var options = CreateConstrainedOptions(decisionBudget: 3);
        var first = CreateFixture(actorCount: 32, options);
        var second = CreateFixture(actorCount: 32, options);

        for (var tick = 0; tick < 48; tick++)
        {
            first.Manager.UpdateAll(first.World, FixedDeltaSeconds, first.Player, false, tick);
            second.Manager.UpdateAll(second.World, FixedDeltaSeconds, second.Player, false, tick);

            for (var index = 0; index < first.Behaviors.Length; index++)
            {
                Assert.Equal(first.Behaviors[index].UpdateCount, second.Behaviors[index].UpdateCount);
                Assert.Equal(first.Behaviors[index].LastUpdateTick, second.Behaviors[index].LastUpdateTick);
            }

            Assert.Equal(
                first.Manager.AiSchedulingTelemetryLastUpdate,
                second.Manager.AiSchedulingTelemetryLastUpdate);
        }
    }

    [Fact]
    public void FarPopulationUsesFairAgePriorityWithoutStarvation()
    {
        const int actorCount = 64;
        const int budget = 4;
        var fixture = CreateFixture(actorCount, CreateConstrainedOptions(budget));
        var sawFairnessPromotion = false;

        for (var tick = 0; tick < 64; tick++)
        {
            fixture.Manager.UpdateAll(fixture.World, FixedDeltaSeconds, fixture.Player, false, tick);
            sawFairnessPromotion |= fixture.Manager.AiSchedulingTelemetryLastUpdate.StarvationPromotions > 0;
        }

        Assert.True(sawFairnessPromotion);
        Assert.All(fixture.Behaviors, behavior =>
        {
            Assert.InRange(behavior.FirstUpdateTick, 0, 15);
            Assert.InRange(behavior.MaximumUpdateGap, 0, 16);
            Assert.InRange(behavior.UpdateCount, 4, 4);
        });
    }

    [Fact]
    public void SkippedKinematicDecisionPreservesExternalVelocityAndStillRunsPhysics()
    {
        var fixture = CreateFixture(actorCount: 2, CreateConstrainedOptions(decisionBudget: 1));
        fixture.Actors[0].Body.Position = new Vector2(32, 32);
        fixture.Actors[1].Body.Position = new Vector2(3_000, 32);
        EnsureBodyChunkLoaded(fixture.World, fixture.Actors[0]);
        EnsureBodyChunkLoaded(fixture.World, fixture.Actors[1]);
        fixture.Manager.UpdateAll(fixture.World, FixedDeltaSeconds, fixture.Player, false, tickNumber: 0);
        fixture.Actors[1].Body.Velocity = new Vector2(333, -17);

        fixture.Manager.UpdateAll(fixture.World, FixedDeltaSeconds, fixture.Player, false, tickNumber: 1);

        Assert.Equal(0, fixture.Behaviors[1].UpdateCount);
        Assert.Equal(new Vector2(333, -17), fixture.Actors[1].Body.Velocity);
        Assert.Equal(2, fixture.Manager.PhysicsTelemetryLastUpdate.BodiesSimulated);
    }

    [Fact]
    public void DeferredDecisionTimePreservesElapsedTimeBeforeActorReturnsToNearRange()
    {
        var fixture = CreateFixture(actorCount: 2, CreateConstrainedOptions(decisionBudget: 1));
        fixture.Actors[0].Body.Position = new Vector2(32, 32);
        fixture.Actors[1].Body.Position = new Vector2(3_000, 32);
        for (var tick = 0; tick < 30; tick++)
        {
            fixture.Manager.UpdateAll(fixture.World, FixedDeltaSeconds, fixture.Player, false, tick);
        }

        fixture.Player.Body.Position = new Vector2(2_992, 32);
        fixture.Manager.UpdateAll(fixture.World, FixedDeltaSeconds, fixture.Player, false, tickNumber: 30);

        Assert.Equal(1, fixture.Behaviors[1].UpdateCount);
        Assert.Equal(0.5f + FixedDeltaSeconds, fixture.Behaviors[1].LastDeltaSeconds, precision: 5);
        Assert.Equal(2, fixture.Manager.PhysicsTelemetryLastUpdate.BodiesSimulated);
    }

    [Fact]
    public void ActorTransferResetsForeignScheduleTicketBeforeNewManagerTick()
    {
        var world = new World(512, 32, WorldMetadata.CreateDefault(9_120));
        var collision = new TileCollisionResolver();
        var options = CreateConstrainedOptions(decisionBudget: 1);
        var firstManager = new EntityManager(64, collision, maximumPhysicsBodies: 2, aiSchedulingOptions: options);
        var secondManager = new EntityManager(64, collision, maximumPhysicsBodies: 2, aiSchedulingOptions: options);
        var player = new PlayerEntity(Vector2.Zero, collision);
        var transferredBehavior = new RecordingAiBehavior();
        var transferred = CreateActor(transferredBehavior, collision, new Vector2(32, 32));
        firstManager.Add(transferred);
        firstManager.UpdateAll(world, FixedDeltaSeconds, player, false, tickNumber: 0);
        Assert.Equal(1, transferredBehavior.UpdateCount);

        firstManager.Remove(transferred);
        transferred.Body.Position = new Vector2(3_000, 32);
        var blocker = CreateActor(new RecordingAiBehavior(), collision, new Vector2(32, 32));
        secondManager.Add(transferred);
        secondManager.Add(blocker);
        secondManager.UpdateAll(world, FixedDeltaSeconds, player, false, tickNumber: 0);

        Assert.Equal(1, transferredBehavior.UpdateCount);
        Assert.Equal(1, secondManager.AiSchedulingTelemetryLastUpdate.NearDecisions);
        Assert.Equal(2, secondManager.PhysicsTelemetryLastUpdate.BodiesSimulated);
    }

    [Fact]
    public void DeadButActiveActorDoesNotConsumeDecisionBudget()
    {
        var fixture = CreateFixture(actorCount: 2, CreateConstrainedOptions(decisionBudget: 1));
        fixture.Actors[0].ApplyDamage(new DamageInfo(
            100,
            DamageType.Generic,
            SourceEntityId: null,
            KnockbackDirection: Vector2.Zero,
            KnockbackForce: 0));

        fixture.Manager.UpdateAll(fixture.World, FixedDeltaSeconds, fixture.Player, false, tickNumber: 0);

        Assert.Equal(0, fixture.Behaviors[0].UpdateCount);
        Assert.Equal(1, fixture.Behaviors[1].UpdateCount);
        Assert.Equal(1, fixture.Manager.AiSchedulingTelemetryLastUpdate.ActiveActors);
        Assert.Equal(1, fixture.Manager.AiSchedulingTelemetryLastUpdate.DecisionsScheduled);
        Assert.Equal(1, fixture.Manager.PhysicsTelemetryLastUpdate.BodiesSimulated);
    }

    private static SchedulerFixture CreateFixture(int actorCount, EntityAiSchedulingOptions options)
    {
        var world = new World(actorCount * 32 + 256, 32, WorldMetadata.CreateDefault(9_119));
        var collision = new TileCollisionResolver();
        var manager = new EntityManager(
            spatialCellSize: 64,
            physicsCollisionResolver: collision,
            maximumPhysicsBodies: Math.Max(4, actorCount),
            aiSchedulingOptions: options);
        var player = new PlayerEntity(new Vector2(0, 32), collision);
        var actors = new EnemyEntity[actorCount];
        var behaviors = new RecordingAiBehavior[actorCount];
        for (var index = 0; index < actorCount; index++)
        {
            var behavior = new RecordingAiBehavior();
            var actor = new EnemyEntity(
                "scheduled-actor",
                new Vector2((index + 192) * GameConstants.TileSize, 32),
                new Vector2(12, 10),
                new HealthComponent(10),
                behavior,
                collision,
                movementMode: EntityMovementMode.Flying);
            manager.Add(actor);
            actors[index] = actor;
            behaviors[index] = behavior;
        }

        return new SchedulerFixture(world, manager, player, actors, behaviors);
    }

    private static EntityAiSchedulingOptions CreateConstrainedOptions(int decisionBudget)
    {
        return new EntityAiSchedulingOptions
        {
            DecisionBudgetPerTick = decisionBudget,
            FullRatePopulationThreshold = 0,
            NearDistance = 96,
            MidDistance = 256,
            MidCadenceTicks = 2,
            FarCadenceTicks = 4,
            StarvationThresholdTicks = 8
        };
    }

    private static EnemyEntity CreateActor(
        IAiBehavior behavior,
        TileCollisionResolver collision,
        Vector2 position)
    {
        return new EnemyEntity(
            "scheduled-actor",
            position,
            new Vector2(12, 10),
            new HealthComponent(10),
            behavior,
            collision,
            movementMode: EntityMovementMode.Flying);
    }

    private static void EnsureBodyChunkLoaded(World world, EnemyEntity actor)
    {
        world.GetOrCreateChunk(CoordinateUtils.TileToChunk(
            CoordinateUtils.WorldToTile(actor.Body.Center.X, actor.Body.Center.Y)));
    }

    private sealed record SchedulerFixture(
        World World,
        EntityManager Manager,
        PlayerEntity Player,
        EnemyEntity[] Actors,
        RecordingAiBehavior[] Behaviors);

    private sealed class RecordingAiBehavior : IAiBehavior
    {
        private int? _targetEntityId;

        public AiState CurrentState => _targetEntityId is null ? AiState.Wander : AiState.Chase;

        public int? TargetEntityId => _targetEntityId;

        public int UpdateCount { get; private set; }

        public long FirstUpdateTick { get; private set; } = -1;

        public long LastUpdateTick { get; private set; } = -1;

        public long MaximumUpdateGap { get; private set; }

        public float LastDeltaSeconds { get; private set; }

        public void SetEngagedTarget(int targetEntityId)
        {
            _targetEntityId = targetEntityId;
        }

        public void Update(EnemyEntity entity, AiUpdateContext context, float deltaSeconds)
        {
            if (FirstUpdateTick < 0)
            {
                FirstUpdateTick = context.TickNumber;
            }
            else
            {
                MaximumUpdateGap = Math.Max(MaximumUpdateGap, context.TickNumber - LastUpdateTick);
            }

            LastUpdateTick = context.TickNumber;
            LastDeltaSeconds = deltaSeconds;
            UpdateCount++;
            entity.Body.Velocity = new Vector2(24, 0);
        }

        public bool TryConsumeAttackIntent(out AiAttackIntent intent)
        {
            intent = default;
            return false;
        }
    }
}
