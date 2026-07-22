using Game.Core.Entities;
using Game.Core.Physics;
using Game.Core.Projectiles;
using Game.Core.World;
using System.Numerics;
using Xunit;

namespace Game.Tests.ProjectileTests;

public sealed class ProjectileEntityTests
{
    [Fact]
    public void Factory_CreatesProjectileVelocityFromDirectionAndSpeed()
    {
        var definition = CreateDefinition();

        var projectile = new ProjectileFactory().Create(definition, Vector2.Zero, Vector2.UnitY, ownerEntityId: 7);

        Assert.Equal(new Vector2(0, 320), projectile.Velocity);
        Assert.Equal(7, projectile.OwnerEntityId);
        Assert.Same(definition, projectile.Definition);
        Assert.Same(projectile.RuntimeState.Definition, projectile.Definition);
    }

    [Fact]
    public void Factory_PreservesAdvancedDefinitionOnExistingProjectileEntityPath()
    {
        var definition = CreateDefinition() with
        {
            DragPerSecond = 0.5f,
            HomingRange = 200,
            HomingTurnRateRadiansPerSecond = 2,
            BounceCount = 3,
            TileCollisionBehavior = ProjectileTileCollisionBehavior.Bounce
        };

        var projectile = new ProjectileFactory().Create(
            definition,
            Vector2.Zero,
            Vector2.UnitX,
            ownerEntityId: 4,
            damageOverride: 9,
            damageTypeOverride: Game.Core.Combat.DamageType.Magic,
            ownerFaction: Game.Core.Entities.EntityFaction.Friendly);

        Assert.Equal(9, projectile.Damage);
        Assert.Equal(0.5f, projectile.Definition.DragPerSecond);
        Assert.Equal(3, projectile.RemainingBounces);
        Assert.Equal(ProjectileTileCollisionBehavior.Bounce, projectile.Definition.TileCollisionBehavior);
    }

    [Fact]
    public void Entity_AdvanceRuntimeSynchronizesExistingPositionAndVelocityProperties()
    {
        var projectile = new ProjectileFactory().Create(
            CreateDefinition() with { DragPerSecond = MathF.Log(2) },
            Vector2.Zero,
            Vector2.UnitX);

        projectile.AdvanceRuntime(1);

        Assert.Equal(projectile.RuntimeState.Position, projectile.Position);
        Assert.Equal(160, projectile.Velocity.X, precision: 3);
        Assert.Equal(160, projectile.Position.X, precision: 3);
    }

    [Fact]
    public void Entity_UpdateConsumesLatchedHomingTargetsExactlyOnce()
    {
        var world = new World(16, 16, WorldMetadata.CreateDefault(seed: 1));
        var projectile = new ProjectileFactory().Create(
            CreateDefinition() with
            {
                HomingRange = 200,
                HomingTurnRateRadiansPerSecond = MathF.PI
            },
            Vector2.Zero,
            Vector2.UnitX,
            ownerEntityId: 1);
        projectile.SetHomingTargetsForNextUpdate(new[]
        {
            new ProjectileHomingTarget(
                2,
                Game.Core.Entities.EntityFaction.Hostile,
                new Vector2(100, -100))
        });

        projectile.Update(world, 0.25f);
        var velocityAfterHoming = projectile.Velocity;
        projectile.Update(world, 0.25f);

        Assert.True(velocityAfterHoming.Y < 0);
        Assert.Equal(velocityAfterHoming, projectile.Velocity);
    }

    [Fact]
    public void EntityManager_UpdateSuppliesSpatialHomingTargetsWithoutCallerArrays()
    {
        var world = new World(32, 32, WorldMetadata.CreateDefault(seed: 1));
        var entities = new EntityManager(spatialCellSize: 32);
        entities.Add(new EntityFactory(new TileCollisionResolver()).CreateEnemy(
            new EntityDefinition
            {
                Id = "flying-target",
                DisplayName = "Flying Target",
                TexturePath = "entities/flying-target",
                MaxHealth = 10,
                ContactDamage = 0,
                MovementMode = EntityMovementMode.Flying
            },
            new Vector2(96, 96)));
        var projectile = new ProjectileFactory().Create(
            CreateDefinition() with
            {
                HomingRange = 256,
                HomingTurnRateRadiansPerSecond = MathF.PI
            },
            new Vector2(32, 32),
            Vector2.UnitX,
            ownerEntityId: 99,
            ownerFaction: EntityFaction.Friendly);
        entities.Add(projectile);

        entities.UpdateAll(world, 0.25f);

        Assert.True(projectile.Velocity.Y > 0);
    }

    [Fact]
    public void EntityManager_BudgetsDenseHomingQueriesAndRotatesDeferredProjectiles()
    {
        const int projectileCount = 257;
        var world = new World(64, 64, WorldMetadata.CreateDefault(seed: 1));
        var entities = new EntityManager(spatialCellSize: 256);
        entities.Add(new EntityFactory(new TileCollisionResolver()).CreateEnemy(
            new EntityDefinition
            {
                Id = "homing-target",
                DisplayName = "Homing Target",
                TexturePath = "entities/homing-target",
                MaxHealth = 10,
                ContactDamage = 0,
                MovementMode = EntityMovementMode.Flying
            },
            new Vector2(64, 128)));
        var projectiles = new ProjectileEntity[projectileCount];
        var definition = CreateDefinition() with
        {
            HomingRange = 512,
            HomingTurnRateRadiansPerSecond = MathF.PI
        };
        for (var index = 0; index < projectiles.Length; index++)
        {
            projectiles[index] = new ProjectileFactory().Create(
                definition,
                new Vector2(64, 64),
                Vector2.UnitX,
                ownerEntityId: 10_000 + index,
                ownerFaction: EntityFaction.Friendly);
            entities.Add(projectiles[index]);
        }

        entities.UpdateAll(world, 0.01f);

        Assert.Equal(256, entities.HomingQueriesPreparedLastUpdate);
        Assert.Equal(1, entities.HomingQueriesDeferredLastUpdate);
        Assert.Equal(0f, projectiles[^1].Velocity.Y);

        entities.UpdateAll(world, 0.01f);

        Assert.Equal(256, entities.HomingQueriesPreparedLastUpdate);
        Assert.Equal(1, entities.HomingQueriesDeferredLastUpdate);
        Assert.True(projectiles[^1].Velocity.Y > 0f);
    }

    [Fact]
    public void Entity_UpdateUsesExactTileSweepBeyondLegacySamplingLimit()
    {
        var world = new World(8_192, 16, WorldMetadata.CreateDefault(seed: 1));
        world.SetTile(50, 0, KnownTileIds.Stone);
        var projectile = new ProjectileEntity(
            new ProjectileDefinition
            {
                Id = "extreme-speed-test",
                TexturePath = "projectiles/extreme-speed-test",
                Speed = 1_000_000,
                Damage = 1,
                CollisionRadius = 2,
                Lifetime = 1
            },
            Vector2.Zero,
            new Vector2(1_000_000, 0));

        projectile.Update(world, 0.1f);

        Assert.False(projectile.IsActive);
        Assert.True(projectile.TryConsumeLatestTileCollisionResult(out var collision));
        Assert.Equal(ProjectileTileCollisionDecision.Destroyed, collision.Decision);
        Assert.Equal(50, collision.TileX);
        Assert.Equal(0, collision.TileY);
        Assert.Equal(KnownTileIds.Stone, collision.TileId);
        Assert.InRange(projectile.Position.X, 795.9f, 796.1f);
    }

    [Fact]
    public void Entity_UpdateDoesNotRehitTouchingTileWhileMovingAway()
    {
        var world = new World(16, 16, WorldMetadata.CreateDefault(seed: 1));
        world.SetTile(1, 0, KnownTileIds.Stone);
        var projectile = new ProjectileEntity(
            CreateDefinition() with
            {
                Speed = 40
            },
            new Vector2(32, 0),
            new Vector2(40, 0));

        projectile.Update(world, 0.1f);

        Assert.True(projectile.IsActive);
        Assert.False(projectile.HasPendingTileCollisionResult);
        Assert.True(projectile.Position.X > 32f);
    }

    [Fact]
    public void LegacyConstructor_UsesSameRuntimeStateAndPreservesPublicContract()
    {
        var projectile = new ProjectileEntity(
            "legacy-arrow",
            new Vector2(3, 4),
            new Vector2(50, 0),
            damage: 7,
            gravity: 0.2f,
            pierce: 1,
            lifetime: 5,
            ownerEntityId: 12,
            age: 1.5f);

        Assert.Equal("legacy-arrow", projectile.ProjectileId);
        Assert.Equal(7, projectile.Damage);
        Assert.Equal(1, projectile.Pierce);
        Assert.Equal(1.5f, projectile.Age);
        Assert.Equal(projectile.Position, projectile.RuntimeState.Position);
    }

    [Fact]
    public void Update_DeactivatesProjectileAfterLifetime()
    {
        var projectile = new ProjectileEntity("arrow", Vector2.Zero, Vector2.Zero, 1, 0, 0, 0.5f);
        var world = new World(16, 16, WorldMetadata.CreateDefault(seed: 1));

        projectile.Update(world, 0.6f);

        Assert.False(projectile.IsActive);
    }

    [Fact]
    public void Update_DeactivatesProjectileOnSolidTileCollision()
    {
        var world = new World(16, 16, WorldMetadata.CreateDefault(seed: 1));
        world.SetTile(1, 0, KnownTileIds.Stone);
        var projectile = new ProjectileEntity("arrow", new Vector2(0, 0), new Vector2(40, 0), 1, 0, 0, 5f);

        projectile.Update(world, 0.5f);

        Assert.False(projectile.IsActive);
    }

    [Fact]
    public void Update_ExistingEntityPathUsesConfiguredTileBounce()
    {
        var world = new World(16, 16, WorldMetadata.CreateDefault(seed: 1));
        world.SetTile(1, 0, KnownTileIds.Stone);
        var projectile = new ProjectileFactory().Create(
            CreateDefinition() with
            {
                Speed = 40,
                BounceCount = 1,
                TileCollisionBehavior = ProjectileTileCollisionBehavior.Bounce
            },
            Vector2.Zero,
            Vector2.UnitX);

        projectile.Update(world, 0.5f);

        Assert.True(projectile.IsActive);
        Assert.True(projectile.Velocity.X < 0);
        Assert.Equal(0, projectile.RemainingBounces);
    }

    private static ProjectileDefinition CreateDefinition()
    {
        return new ProjectileDefinition
        {
            Id = "arrow",
            TexturePath = "projectiles/arrow",
            Speed = 320,
            Damage = 5,
            Lifetime = 5
        };
    }
}
