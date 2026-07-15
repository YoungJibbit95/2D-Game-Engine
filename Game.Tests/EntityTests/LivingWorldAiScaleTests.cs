using Game.Core;
using Game.Core.Combat;
using Game.Core.Entities;
using Game.Core.Entities.AI;
using Game.Core.Physics;
using Game.Core.Tiles;
using Game.Core.World;
using System.Numerics;
using Xunit;

namespace Game.Tests.EntityTests;

public sealed class LivingWorldAiScaleTests
{
    [Fact]
    public void FriendlyFlock_PerchesOutsideActivityPeriodAndPublishesTelemetry()
    {
        var world = CreateGroundWorld();
        var profile = new AiProfileDefinition
        {
            Kind = AiBehaviorKind.Critter,
            MoveSpeed = 36,
            FleeSpeed = 72,
            PatrolRadius = 96,
            FlockRadius = 80,
            FlockWeight = 0.75f,
            MinFlockSize = 2,
            IdleChance = 0,
            DecisionInterval = 0.25f,
            ActivityPeriod = AiActivityPeriod.Day,
            PerchWhenInactive = true,
            RequiresLineOfSight = false
        };
        var firstBehavior = new CritterAiBehavior(profile);
        var secondBehavior = new CritterAiBehavior(profile);
        var first = CreateActor("bird", firstBehavior, new Vector2(48, 64), EntityFaction.Friendly, EntityMovementMode.Flying);
        var second = CreateActor("bird", secondBehavior, new Vector2(72, 64), EntityFaction.Friendly, EntityMovementMode.Flying);
        var manager = new EntityManager();
        manager.Add(first);
        manager.Add(second);

        firstBehavior.Update(first, new AiUpdateContext(world, manager.Entities, IsNight: false), 1f / 60f);
        Assert.Equal(AiState.Flock, firstBehavior.CurrentState);
        Assert.Equal(1, firstBehavior.Telemetry.NearbyAllies);
        Assert.True(firstBehavior.Telemetry.ActivePeriod);

        firstBehavior.Update(first, new AiUpdateContext(world, manager.Entities, IsNight: true), 1f / 60f);
        var telemetry = firstBehavior.Telemetry;

        Assert.Equal(AiState.Perch, telemetry.State);
        Assert.False(telemetry.ActivePeriod);
        Assert.Equal(2, telemetry.UpdateCount);
        Assert.True(telemetry.StateTransitionCount >= 2);
    }

    [Fact]
    public void HostileMemory_InvestigatesThenReturnsHome()
    {
        var world = CreateGroundWorld();
        var profile = new AiProfileDefinition
        {
            Kind = AiBehaviorKind.Hostile,
            DetectionRange = 160,
            LoseTargetRange = 220,
            MoveSpeed = 48,
            PatrolRadius = 32,
            ReturnHomeDistance = 40,
            AttackRange = 12,
            PerceptionMemorySeconds = 0.2f,
            DecisionInterval = 0.5f,
            RequiresLineOfSight = true
        };
        var behavior = new HostileAiBehavior(profile);
        var hostile = CreateActor("spider", behavior, new Vector2(48, 64), EntityFaction.Hostile, EntityMovementMode.Ground);
        var player = new PlayerEntity(new Vector2(112, 52), new TileCollisionResolver());
        var manager = new EntityManager();
        manager.Add(hostile);
        manager.Add(player);
        var context = new AiUpdateContext(world, manager.Entities, player);

        behavior.Update(hostile, context, 0.01f);
        world.SetTile(5, 3, KnownTileIds.Dirt);
        world.SetTile(5, 4, KnownTileIds.Dirt);
        behavior.Update(hostile, context, 0.1f);

        Assert.Equal(AiState.Investigate, behavior.CurrentState);
        Assert.False(behavior.Telemetry.TargetVisible);
        Assert.Equal(player.Id, behavior.Telemetry.TargetEntityId);

        hostile.Body.Position += new Vector2(96, 0);
        behavior.Update(hostile, new AiUpdateContext(world, new Entity[] { hostile }), 0.2f);

        Assert.Equal(AiState.ReturnHome, behavior.CurrentState);
        Assert.Null(behavior.Telemetry.TargetEntityId);
        Assert.True(behavior.BodyVelocityPointsHome(hostile));
    }

    [Fact]
    public void TwoHundredEntityBehaviorSoak_StaysWithinSteadyStateAllocationBudget()
    {
        const int entityCount = 200;
        const int measurementTicks = 120;
        // Full sensing, flocking and steering budget after warmup for one 200-actor tick.
        const long maxBytesPerTwoHundredEntityTick = 12 * 1024;
        var world = CreateGroundWorld(width: 512);
        var profile = new AiProfileDefinition
        {
            Kind = AiBehaviorKind.Critter,
            MoveSpeed = 30,
            FleeSpeed = 70,
            PatrolRadius = 120,
            FlockRadius = 48,
            FlockWeight = 0.6f,
            MinFlockSize = 3,
            IdleChance = 0.1f,
            DecisionInterval = 0.5f,
            RequiresLineOfSight = false,
            AvoidLedges = false,
            AvoidLiquid = false
        };
        var behaviors = new CritterAiBehavior[entityCount];
        var actors = new EnemyEntity[entityCount];
        var manager = new EntityManager(32);
        for (var index = 0; index < entityCount; index++)
        {
            var behavior = new CritterAiBehavior(profile);
            var actor = CreateActor(
                "soak_critter",
                behavior,
                new Vector2((index + 8) * GameConstants.TileSize, 64),
                EntityFaction.Friendly,
                EntityMovementMode.Flying);
            behaviors[index] = behavior;
            actors[index] = actor;
            manager.Add(actor);
        }

        var context = new AiUpdateContext(world, manager.Entities, IsNight: false);
        for (var warmup = 0; warmup < 32; warmup++)
        {
            UpdateAll(behaviors, actors, context);
        }

        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        for (var tick = 0; tick < measurementTicks; tick++)
        {
            UpdateAll(behaviors, actors, context);
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        var bytesPerTick = allocated / (double)measurementTicks;

        Assert.True(
            bytesPerTick <= maxBytesPerTwoHundredEntityTick,
            $"allocated={allocated} B, perTick={bytesPerTick:F1} B, budget={maxBytesPerTwoHundredEntityTick} B");
        Assert.All(behaviors, behavior => Assert.Equal(152, behavior.Telemetry.UpdateCount));
    }

    private static void UpdateAll(
        CritterAiBehavior[] behaviors,
        EnemyEntity[] actors,
        AiUpdateContext context)
    {
        for (var index = 0; index < behaviors.Length; index++)
        {
            behaviors[index].Update(actors[index], context, 1f / 60f);
        }
    }

    private static EnemyEntity CreateActor(
        string id,
        IAiBehavior behavior,
        Vector2 position,
        EntityFaction faction,
        EntityMovementMode movementMode)
    {
        return new EnemyEntity(
            id,
            position,
            new Vector2(12, 10),
            new HealthComponent(10),
            behavior,
            new TileCollisionResolver(),
            contactDamage: faction == EntityFaction.Hostile ? 5 : 0,
            faction: faction,
            movementMode: movementMode,
            tags: new[] { "living_world", id });
    }

    private static World CreateGroundWorld(int width = 256)
    {
        var world = new World(width, 32, WorldMetadata.CreateDefault(71));
        for (var x = 0; x < width; x++)
        {
            world.SetTile(x, 10, KnownTileIds.Dirt);
        }

        return world;
    }
}

internal static class AiScaleTestExtensions
{
    public static bool BodyVelocityPointsHome(this HostileAiBehavior behavior, EnemyEntity actor)
    {
        return behavior.Telemetry.HomePosition.X < actor.Body.Position.X && actor.Body.Velocity.X < 0;
    }
}
