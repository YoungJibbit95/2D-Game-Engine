using Game.Core.Combat;
using Game.Core.Entities;
using Game.Core.Entities.AI;
using Game.Core.Entities.AI.Sensing;
using Game.Core.Physics;
using Game.Core.World;
using System.Numerics;
using Xunit;

namespace Game.Tests.EntityTests;

public sealed class FactionAndAiTests
{
    [Fact]
    public void AiProfile_RejectsEnabledSingleMemberFlock()
    {
        var profile = new AiProfileDefinition
        {
            FlockRadius = 64,
            FlockWeight = 0.5f,
            MinFlockSize = 1
        };

        Assert.Throws<Game.Core.Data.RegistryValidationException>(() =>
            AiProfileDefinition.Validate("unsafe-flock", profile));
    }

    [Fact]
    public void EnemyEntity_RejectsNullAiBehavior()
    {
        Assert.Throws<ArgumentNullException>(() => new EnemyEntity(
            "invalid-ai",
            Vector2.Zero,
            new Vector2(12, 10),
            new HealthComponent(10),
            null!,
            new TileCollisionResolver()));
    }

    [Fact]
    public void EntityManager_UpdateMovesSpatialQueryMembership()
    {
        var world = CreateGroundWorld();
        var entities = new EntityManager(16);
        var actor = CreateActor(
            "moving-bird",
            EntityFaction.Friendly,
            AiBehaviorKind.None,
            new Vector2(32, 48),
            profile => profile with { Kind = AiBehaviorKind.None });
        actor.Body.Velocity = new Vector2(64, 0);
        entities.Add(actor);

        entities.UpdateAll(world, 0.5f);

        Assert.DoesNotContain(actor, entities.Query(new RectI(28, 44, 20, 24)));
        Assert.Contains(actor, entities.Query(new RectI(60, 44, 20, 24)));
    }

    [Theory]
    [InlineData(EntityFaction.Friendly, EntityFaction.Friendly, EntityDisposition.Friendly)]
    [InlineData(EntityFaction.Hostile, EntityFaction.Hostile, EntityDisposition.Friendly)]
    [InlineData(EntityFaction.Friendly, EntityFaction.Hostile, EntityDisposition.Hostile)]
    [InlineData(EntityFaction.Hostile, EntityFaction.Friendly, EntityDisposition.Hostile)]
    [InlineData(EntityFaction.Neutral, EntityFaction.Hostile, EntityDisposition.Neutral)]
    public void FactionResolver_ReturnsSymmetricDisposition(
        EntityFaction observer,
        EntityFaction subject,
        EntityDisposition expected)
    {
        Assert.Equal(expected, EntityFactionResolver.GetDisposition(observer, subject));
    }

    [Fact]
    public void Loader_ReadsFactionMovementTagsAndAiProfile()
    {
        const string json = """
        {
          "id": "squirrel",
          "displayName": "Squirrel",
          "texture": "entities/critters/squirrel",
          "maxHealth": 6,
          "faction": "friendly",
          "movementMode": "ground",
          "tags": ["critter", "forest"],
          "ai": {
            "kind": "critter",
            "detectionRange": 120,
            "fleeSpeed": 90
          }
        }
        """;

        var definition = new EntityDefinitionJsonLoader().LoadDefinitionFromJson(json);

        Assert.Equal(EntityFaction.Friendly, definition.Faction);
        Assert.Equal(EntityMovementMode.Ground, definition.MovementMode);
        Assert.Equal(new[] { "critter", "forest" }, definition.Tags);
        Assert.Equal(AiBehaviorKind.Critter, definition.Ai?.Kind);
        Assert.Equal(120, definition.Ai?.DetectionRange);
    }

    [Fact]
    public void Loader_LegacyDefinitionKeepsHostileGroundDefaults()
    {
        const string json = """
        { "id": "legacy", "displayName": "Legacy", "texture": "entities/legacy", "maxHealth": 10 }
        """;

        var definition = new EntityDefinitionJsonLoader().LoadDefinitionFromJson(json);

        Assert.Equal(EntityFaction.Hostile, definition.Faction);
        Assert.Equal(EntityMovementMode.Ground, definition.MovementMode);
        Assert.Null(definition.Ai);
    }

    [Fact]
    public void FriendlyCritter_FleesVisibleHostile()
    {
        var world = CreateGroundWorld();
        var entities = new EntityManager(16);
        var critter = CreateActor("squirrel", EntityFaction.Friendly, AiBehaviorKind.Critter, new Vector2(64, 64));
        var hostile = CreateActor("boar", EntityFaction.Hostile, AiBehaviorKind.Hostile, new Vector2(96, 64));
        critter.Body.OnGround = true;
        entities.Add(critter);
        entities.Add(hostile);

        entities.UpdateAll(world, 0.016f);

        Assert.Equal(AiState.Flee, critter.AiState);
        Assert.Equal(hostile.Id, critter.TargetEntityId);
        Assert.True(critter.Body.Velocity.X < 0);
    }

    [Fact]
    public void HostileActor_TransitionsFromChaseToAttack()
    {
        var world = CreateGroundWorld();
        var entities = new EntityManager(16);
        var hostile = CreateActor("boar", EntityFaction.Hostile, AiBehaviorKind.Hostile, new Vector2(48, 64));
        var player = new PlayerEntity(new Vector2(112, 52), new TileCollisionResolver());
        hostile.Body.OnGround = true;
        entities.Add(hostile);
        entities.Add(player);

        entities.UpdateAll(world, 0.016f);
        Assert.Equal(AiState.Chase, hostile.AiState);
        Assert.Equal(player.Id, hostile.TargetEntityId);
        Assert.False(hostile.CanDespawnSafely());

        player.Body.Position = new Vector2(65, 52);
        hostile.Body.OnGround = true;
        entities.UpdateAll(world, 0.016f);

        Assert.Equal(AiState.Attack, hostile.AiState);
        Assert.True(hostile.TryConsumeAttackIntent(out var intent));
        Assert.Equal(player.Id, intent.TargetEntityId);
    }

    [Fact]
    public void LineOfSightSensor_DetectsBlockingTile()
    {
        var world = CreateGroundWorld();
        world.SetTile(5, 3, KnownTileIds.Dirt);
        var sensor = new LineOfSightSensor();

        Assert.False(sensor.HasLineOfSight(world, new Vector2(48, 56), new Vector2(112, 56)));
        Assert.True(sensor.HasLineOfSight(world, new Vector2(48, 32), new Vector2(112, 32)));
    }

    [Fact]
    public void LedgeAndLiquidSensors_InspectTilesAhead()
    {
        var world = CreateGroundWorld();
        world.RemoveTile(5, 5);
        world.SetTile(3, 4, TileInstance.Liquid(255));
        var actor = CreateActor("squirrel", EntityFaction.Friendly, AiBehaviorKind.Critter, new Vector2(64, 64));

        Assert.True(new LedgeSensor().HasLedgeAhead(world, actor, 1));
        Assert.True(new TileHazardSensor().HasLiquidAhead(world, actor, -1));
    }

    [Fact]
    public void EnemyEntity_CapturesAppliedDamageForLootContext()
    {
        var actor = CreateActor("spider", EntityFaction.Hostile, AiBehaviorKind.Hostile, Vector2.Zero);
        actor.ApplyDamage(new DamageInfo(5, DamageType.Melee, 42, Vector2.UnitX, 0));

        var context = actor.CreateLootKillContext(EntityFaction.Friendly, isNight: true, victimDepth: 140);

        Assert.Equal(42, context.KillerEntityId);
        Assert.Equal(EntityFaction.Friendly, context.KillerFaction);
        Assert.Equal(DamageType.Melee, context.DamageType);
        Assert.True(context.IsNight);
        Assert.Equal(140, context.VictimDepth);
    }

    [Fact]
    public void HostileActor_RemembersLastSeenTargetUntilMemoryExpires()
    {
        var world = CreateGroundWorld();
        var entities = new EntityManager(16);
        var hostile = CreateActor(
            "spider",
            EntityFaction.Hostile,
            AiBehaviorKind.Hostile,
            new Vector2(48, 64),
            profile => profile with { PerceptionMemorySeconds = 0.25f });
        var player = new PlayerEntity(new Vector2(112, 52), new TileCollisionResolver());
        entities.Add(hostile);
        entities.Add(player);

        entities.UpdateAll(world, 0.01f);
        Assert.Equal(player.Id, hostile.TargetEntityId);

        world.SetTile(5, 3, KnownTileIds.Dirt);
        world.SetTile(5, 4, KnownTileIds.Dirt);
        entities.UpdateAll(world, 0.1f);

        Assert.Equal(AiState.Investigate, hostile.AiState);
        Assert.Equal(player.Id, hostile.TargetEntityId);

        entities.UpdateAll(world, 0.2f);

        Assert.Equal(AiState.Patrol, hostile.AiState);
        Assert.Null(hostile.TargetEntityId);
        Assert.True(hostile.CanDespawnSafely());
    }

    [Fact]
    public void HostileActor_EmitsRangeCheckedAttackIntentsOnCooldown()
    {
        var world = CreateGroundWorld();
        var entities = new EntityManager(16);
        var hostile = CreateActor(
            "boar",
            EntityFaction.Hostile,
            AiBehaviorKind.Hostile,
            new Vector2(64, 64),
            profile => profile with { AttackCooldown = 0.75f, AttackRange = 32 });
        var player = new PlayerEntity(new Vector2(76, 52), new TileCollisionResolver());
        entities.Add(hostile);
        entities.Add(player);

        entities.UpdateAll(world, 0.01f);
        var first = new EntityAttackSystem().ResolvePendingAttacks(entities, playerInvulnerabilitySeconds: 0);
        entities.UpdateAll(world, 0.2f);
        var coolingDown = new EntityAttackSystem().ResolvePendingAttacks(entities, playerInvulnerabilitySeconds: 0);
        entities.UpdateAll(world, 0.6f);
        var readyAgain = new EntityAttackSystem().ResolvePendingAttacks(entities, playerInvulnerabilitySeconds: 0);

        Assert.Equal(1, first.HitsApplied);
        Assert.Equal(0, coolingDown.IntentsConsumed);
        Assert.Equal(1, readyAgain.HitsApplied);
        Assert.Equal(80, player.Health);
    }

    [Fact]
    public void InjuredHostile_FleesFactionEnemyBelowHealthThreshold()
    {
        var world = CreateGroundWorld();
        var entities = new EntityManager(16);
        var hostile = CreateActor(
            "boar",
            EntityFaction.Hostile,
            AiBehaviorKind.Hostile,
            new Vector2(64, 64),
            profile => profile with { FleeHealthThreshold = 0.25f });
        var player = new PlayerEntity(new Vector2(112, 52), new TileCollisionResolver());
        hostile.ApplyDamage(new DamageInfo(16, DamageType.Melee, null, Vector2.Zero, 0));
        entities.Add(hostile);
        entities.Add(player);

        entities.UpdateAll(world, 0.01f);

        Assert.Equal(AiState.Flee, hostile.AiState);
        Assert.True(hostile.Body.Velocity.X < 0);
    }

    [Fact]
    public void GroundSteering_JumpsOverLocalOneTileObstacle()
    {
        var world = CreateGroundWorld();
        world.SetTile(5, 4, KnownTileIds.Dirt);
        var entities = new EntityManager(16);
        var hostile = CreateActor(
            "boar",
            EntityFaction.Hostile,
            AiBehaviorKind.Hostile,
            new Vector2(64, 64),
            profile => profile with { RequiresLineOfSight = false, JumpSpeed = 280 });
        var player = new PlayerEntity(new Vector2(112, 52), new TileCollisionResolver());
        hostile.Body.OnGround = true;
        entities.Add(hostile);
        entities.Add(player);

        entities.UpdateAll(world, 0.01f);

        Assert.Equal(AiState.Chase, hostile.AiState);
        Assert.True(hostile.Body.Velocity.Y < 0);
    }

    [Fact]
    public void EnemyEntity_RecentDamageProtectsDespawnUntilTimerExpires()
    {
        var world = CreateGroundWorld();
        var actor = new EntityFactory(new TileCollisionResolver()).CreateEnemy(new EntityDefinition
        {
            Id = "boar",
            DisplayName = "Boar",
            TexturePath = "entities/boar",
            MaxHealth = 20,
            Despawn = new EntityDespawnPolicyDefinition
            {
                Mode = EntityDespawnMode.Distance,
                DamageProtectionSeconds = 1
            }
        }, new Vector2(64, 64));

        actor.ApplyDamage(new DamageInfo(1, DamageType.Melee, null, Vector2.Zero, 0));
        Assert.False(actor.CanDespawnSafely());

        actor.Update(world, 1.1f);

        Assert.True(actor.CanDespawnSafely());
    }

    private static EnemyEntity CreateActor(
        string id,
        EntityFaction faction,
        AiBehaviorKind kind,
        Vector2 position,
        Func<AiProfileDefinition, AiProfileDefinition>? configureProfile = null)
    {
        var profile = new AiProfileDefinition
        {
            Kind = kind,
            DetectionRange = 160,
            LoseTargetRange = 220,
            MoveSpeed = 50,
            FleeSpeed = 100,
            AttackRange = 28,
            RequiresLineOfSight = true,
            IdleChance = 0
        };

        return new EntityFactory(new TileCollisionResolver()).CreateEnemy(new EntityDefinition
        {
            Id = id,
            DisplayName = id,
            TexturePath = $"entities/{id}",
            MaxHealth = 20,
            Width = 16,
            Height = 16,
            Faction = faction,
            Tags = new[] { faction == EntityFaction.Hostile ? "enemy" : "critter" },
            Ai = configureProfile?.Invoke(profile) ?? profile
        }, position);
    }

    private static World CreateGroundWorld()
    {
        var world = new World(16, 12, WorldMetadata.CreateDefault(7));
        for (var x = 0; x < world.WidthTiles; x++)
        {
            world.SetTile(x, 5, KnownTileIds.Dirt);
        }

        return world;
    }
}
