using System.Numerics;
using Game.Core;
using Game.Core.Combat;
using Game.Core.Entities;
using Game.Core.Entities.AI;
using Game.Core.Inventory;
using Game.Core.Projectiles;
using Game.Core.Physics;
using Game.Core.Tiles;
using Game.Core.World;
using Xunit;

namespace Game.Tests.EntityTests;

public sealed class EntityPhysicsIntegrationTests
{
    [Fact]
    public void EnemyEntity_DirectUpdateUsesDynamicPhysicsGravityAndSynchronizesPosition()
    {
        var world = CreateWorld();
        var enemy = CreateEnemy(new TileCollisionResolver(), EntityMovementMode.Ground);

        enemy.Update(world, 0.1f);

        Assert.Equal(PhysicsBodyType.Dynamic, enemy.Body.BodyType);
        Assert.Equal(105f, enemy.Body.Velocity.Y, precision: 3);
        Assert.Equal(10.5f, enemy.Body.Position.Y, precision: 3);
        Assert.Equal(enemy.Body.Position, enemy.Position);
    }

    [Fact]
    public void DroppedItemEntity_DirectUpdateUsesPhysicsGravityAndPreservesLinearDragPolicy()
    {
        var world = CreateWorld();
        var droppedItem = new DroppedItemEntity(
            new ItemStack("gel", 1),
            Vector2.Zero,
            new TileCollisionResolver());
        droppedItem.Body.Velocity = new Vector2(180, 0);

        droppedItem.Update(world, 0.1f);

        Assert.Equal(PhysicsBodyType.Dynamic, droppedItem.Body.BodyType);
        Assert.Equal(90f, droppedItem.Body.Velocity.X, precision: 3);
        Assert.Equal(85f, droppedItem.Body.Velocity.Y, precision: 3);
        Assert.Equal(new Vector2(9f, 8.5f), droppedItem.Body.Position);
        Assert.Equal(droppedItem.Body.Position, droppedItem.Position);
    }

    [Fact]
    public void EntityManager_SubmitsCompleteMixedBatchAndRejectsCapacityOverflowAtIngress()
    {
        var world = CreateWorld();
        var collision = new TileCollisionResolver();
        var entities = new EntityManager(
            physicsCollisionResolver: collision,
            maximumPhysicsBodies: 2);
        var enemy = CreateEnemy(collision, EntityMovementMode.Flying);
        var droppedItem = new DroppedItemEntity(
            new ItemStack("gel", 1),
            new Vector2(32, 0),
            collision);
        entities.Add(enemy);
        entities.Add(droppedItem);

        entities.UpdateAll(world, 0.1f);

        Assert.Equal(2, entities.PhysicsTelemetryLastUpdate.BodiesRequested);
        Assert.Equal(2, entities.PhysicsTelemetryLastUpdate.BodiesSimulated);
        Assert.Equal(1, entities.PhysicsTelemetryLastUpdate.DynamicBodies);
        Assert.Equal(1, entities.PhysicsTelemetryLastUpdate.KinematicBodies);
        Assert.Equal(0, entities.PhysicsTelemetryLastUpdate.BodiesDeferred);
        var overflow = new DroppedItemEntity(
            new ItemStack("gel", 1),
            new Vector2(64, 0),
            collision);

        var error = Assert.Throws<InvalidOperationException>(() => entities.Add(overflow));

        Assert.Contains("never deferred", error.Message, StringComparison.Ordinal);
        Assert.Equal(2, entities.Entities.Count);
        Assert.Equal(0, overflow.Id);
    }

    [Fact]
    public void EntityManager_ResolvesEnemyAndDropContactInsideTheAuthoritativeBatch()
    {
        var world = CreateWorld();
        var collision = new TileCollisionResolver();
        var entities = new EntityManager(
            physicsCollisionResolver: collision,
            maximumPhysicsBodies: 2,
            maximumPhysicsBodyPairs: 1);
        var enemy = CreateEnemy(collision, EntityMovementMode.Flying);
        var droppedItem = new DroppedItemEntity(
            new ItemStack("gel", 1),
            new Vector2(8, 0),
            collision);
        entities.Add(enemy);
        entities.Add(droppedItem);

        entities.UpdateAll(world, 0.1f);

        Assert.Equal(1, entities.PhysicsTelemetryLastUpdate.BodyPairsFound);
        Assert.Equal(1, entities.PhysicsTelemetryLastUpdate.BodyPairsResolved);
        Assert.Equal(1, entities.PhysicsTelemetryLastUpdate.BodyPairPositionCorrections);
        Assert.Equal(enemy.Body.Position, enemy.Position);
        Assert.Equal(droppedItem.Body.Position, droppedItem.Position);
        Assert.True(droppedItem.Position.X > 8f);
    }

    [Fact]
    public void EntityManager_UsesContinuousSolverForFastProjectilePairs()
    {
        var world = CreateWorld();
        var collision = new TileCollisionResolver();
        var entities = new EntityManager(
            physicsCollisionResolver: collision,
            maximumPhysicsBodyPairs: 1);
        var definition = new ProjectileDefinition
        {
            Id = "continuous-pair-test",
            TexturePath = "projectiles/arrow",
            Speed = 8_000,
            Damage = 1,
            Lifetime = 1,
            CollisionRadius = 2,
            Gravity = 0
        };

        var leftProjectile = new ProjectileFactory().Create(
            definition,
            new Vector2(202, 8),
            Vector2.UnitX,
            ownerEntityId: 1);
        var rightProjectile = new ProjectileFactory().Create(
            definition with
            {
                Id = "continuous-pair-test-reverse"
            },
            new Vector2(258, 8),
            -Vector2.UnitX,
            ownerEntityId: 2);
        leftProjectile.Body.CollisionMask = PhysicsCollisionLayer.Projectile | PhysicsCollisionLayer.World;
        rightProjectile.Body.CollisionMask = PhysicsCollisionLayer.Projectile | PhysicsCollisionLayer.World;

        entities.Add(leftProjectile);
        entities.Add(rightProjectile);

        entities.UpdateAll(world, 0.01f);

        Assert.True(leftProjectile.IsActive);
        Assert.True(rightProjectile.IsActive);
        Assert.Equal(1, entities.PhysicsTelemetryLastUpdate.ContinuousBodyPairs);
        Assert.True(entities.PhysicsTelemetryLastUpdate.ContinuousBodyToiPasses > 0);
        Assert.True(entities.PhysicsTelemetryLastUpdate.BodyPairsResolved >= 1);
        Assert.True(leftProjectile.Position.X < rightProjectile.Position.X);
        Assert.True(leftProjectile.Body.Velocity.X < 0f);
        Assert.True(rightProjectile.Body.Velocity.X > 0f);
        Assert.True(float.IsFinite(leftProjectile.Position.X));
        Assert.True(float.IsFinite(rightProjectile.Position.X));
    }

    [Fact]
    public void EntityManager_ReadOnlyTileContactsReturnsOnlyOwningBodySlice()
    {
        var firstBodyContact = new PhysicsContact(
            1,
            2,
            new Vector2(3, 4),
            Vector2.UnitX,
            0.25f,
            PhysicsContactFlags.RightWall);
        var secondBodyContact = new PhysicsContact(
            5,
            6,
            new Vector2(7, 8),
            -Vector2.UnitY,
            0.5f,
            PhysicsContactFlags.Ground);
        var staleNeighborContact = new PhysicsContact(
            9,
            10,
            new Vector2(11, 12),
            Vector2.UnitY,
            0.75f,
            PhysicsContactFlags.Ceiling);
        PhysicsContact[] contacts =
        [
            firstBodyContact,
            default,
            secondBodyContact,
            staleNeighborContact
        ];

        var slice = EntityManager.ReadOnlyTileContacts(
            contacts,
            bodyIndex: 1,
            contactsPerBody: 2,
            contactsWritten: 1);

        Assert.Equal(1, slice.Length);
        Assert.Equal(secondBodyContact, slice[0]);
        Assert.True(EntityManager.ReadOnlyTileContacts(
            contacts,
            bodyIndex: int.MaxValue,
            contactsPerBody: int.MaxValue,
            contactsWritten: int.MaxValue).IsEmpty);
    }

    [Fact]
    public void EntityManager_DoesNotRouteNeighborTileContactsIntoProjectileRuntime()
    {
        var world = CreateWorld();
        world.SetTile(0, 1, KnownTileIds.Dirt);
        var collision = new TileCollisionResolver();
        var entities = new EntityManager(
            physicsCollisionResolver: collision,
            maximumPhysicsBodies: 2,
            maximumPhysicsBodyPairs: 1);
        var droppedItem = new DroppedItemEntity(
            new ItemStack("gel", 1),
            Vector2.Zero,
            collision);
        var projectile = new ProjectileEntity(
            "contact-slice-probe",
            new Vector2(64, 64),
            new Vector2(120, 0),
            damage: 1,
            gravity: 0,
            pierce: 0,
            lifetime: 5,
            ownerEntityId: 1);
        entities.Add(droppedItem);
        entities.Add(projectile);

        entities.UpdateAll(world, 0.1f);

        Assert.True(entities.PhysicsTelemetryLastUpdate.ContactsWritten > 0);
        Assert.True(projectile.IsActive);
        Assert.False(projectile.HasPendingTileCollisionResult);
        Assert.True(projectile.Position.X > 64f);
    }

    [Fact]
    public void EnemyEntity_KnockbackUsesMassResistanceAndVelocityClamp()
    {
        var enemy = CreateEnemy(new TileCollisionResolver(), EntityMovementMode.Ground);
        enemy.Body.Mass = 4f;
        enemy.Body.KnockbackResistance = 0.5f;

        Assert.True(enemy.ApplyDamage(new DamageInfo(
            1,
            DamageType.Magic,
            SourceEntityId: 7,
            KnockbackDirection: new Vector2(3, 4),
            KnockbackForce: 400f)));
        Assert.Equal(new Vector2(30, 40), enemy.Body.Velocity);

        enemy.Body.Velocity = Vector2.Zero;
        enemy.Body.Mass = 0.01f;
        enemy.Body.KnockbackResistance = -2f;
        var applied = enemy.Body.ApplyKnockback(Vector2.UnitX, 100_000f);

        Assert.Equal(new Vector2(720, 0), applied);
        Assert.Equal(new Vector2(720, 0), enemy.Body.Velocity);
    }

    [Fact]
    public void EntityManager_ContinuousProjectilesWithoutTileContactsStayActive()
    {
        var world = CreateWorld();
        var entities = new EntityManager();
        var projectile = new ProjectileEntity(
            "high-speed-passive",
            new Vector2(8, 8),
            new Vector2(8_000, 0),
            damage: 1,
            gravity: 0,
            pierce: 0,
            lifetime: 5,
            ownerEntityId: 1);

        entities.Add(projectile);
        entities.UpdateAll(world, 0.01f);

        Assert.True(projectile.IsActive);
        Assert.True(projectile.Position.X > 8f);
        Assert.Equal(0, entities.PhysicsTelemetryLastUpdate.ContinuousBodyPairs);
        Assert.Equal(0, entities.PhysicsTelemetryLastUpdate.BodyPairsResolved);
        Assert.Equal(0, entities.PhysicsTelemetryLastUpdate.ContinuousBodyToiPasses);
    }

    [Fact]
    public void EntityManager_NegativeXLoadedChunkRetainsBodyContacts()
    {
        var world = new World(
            GameConstants.ChunkSize,
            32,
            WorldMetadata.CreateDefault(seed: 19),
            isHorizontallyInfinite: true);
        world.GetOrCreateChunk(new ChunkPos(-1, 0));
        var collision = new TileCollisionResolver();
        var entities = new EntityManager(
            physicsCollisionResolver: collision,
            maximumPhysicsBodies: 2,
            maximumPhysicsBodyPairs: 1);
        var left = new DroppedItemEntity(
            new ItemStack("gel", 1),
            new Vector2(-40, 32),
            collision);
        var right = new DroppedItemEntity(
            new ItemStack("gel", 1),
            new Vector2(-35, 32),
            collision);
        entities.Add(left);
        entities.Add(right);

        entities.UpdateAll(world, 1f / 60f);

        Assert.Equal(1, entities.PhysicsTelemetryLastUpdate.BodyPairsResolved);
        Assert.True(left.Position.X < -40f);
        Assert.True(right.Position.X > -35f);
        Assert.True(float.IsFinite(left.Position.Y));
        Assert.True(float.IsFinite(right.Position.Y));
        Assert.Single(world.Chunks);
    }

    [Fact]
    public void EntityManager_UnloadedNegativeBoundaryDoesNotGenerateOrEnterMissingChunk()
    {
        var world = new World(
            GameConstants.ChunkSize,
            32,
            WorldMetadata.CreateDefault(seed: 23),
            isHorizontallyInfinite: true);
        world.GetOrCreateChunk(new ChunkPos(0, 0));
        var collision = new TileCollisionResolver();
        var entities = new EntityManager(
            physicsCollisionResolver: collision,
            maximumPhysicsBodies: 1);
        var droppedItem = new DroppedItemEntity(
            new ItemStack("gel", 1),
            new Vector2(1, 32),
            collision);
        droppedItem.Body.Velocity = new Vector2(-120, 0);
        entities.Add(droppedItem);

        entities.UpdateAll(world, 0.1f);

        Assert.Equal(0f, droppedItem.Position.X);
        Assert.Equal(0f, droppedItem.Body.Velocity.X);
        Assert.Single(world.Chunks);
    }

    [Fact]
    public void EntityManager_RejectsMismatchedCollisionBudgetsBeforeRegistration()
    {
        var managerCollision = new TileCollisionResolver();
        var entityCollision = new TileCollisionResolver(new TileCollisionSettings(4, 8, 128));
        var entities = new EntityManager(physicsCollisionResolver: managerCollision);
        var enemy = CreateEnemy(entityCollision, EntityMovementMode.Ground);

        Assert.Throws<InvalidOperationException>(() => entities.Add(enemy));
        Assert.Empty(entities.Entities);
        Assert.Equal(0, enemy.Id);
    }

    private static EnemyEntity CreateEnemy(
        TileCollisionResolver collision,
        EntityMovementMode movementMode)
    {
        return new EnemyEntity(
            "physics-test",
            Vector2.Zero,
            new Vector2(12, 10),
            new HealthComponent(10),
            NullAiBehavior.Instance,
            collision,
            movementMode: movementMode);
    }

    private static World CreateWorld()
    {
        var world = new World(64, 32, WorldMetadata.CreateDefault(seed: 17));
        world.SetTile(15, 15, KnownTileIds.Dirt);
        return world;
    }
}
