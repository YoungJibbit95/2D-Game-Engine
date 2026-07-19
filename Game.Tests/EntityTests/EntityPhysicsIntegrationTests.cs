using System.Numerics;
using Game.Core;
using Game.Core.Combat;
using Game.Core.Entities;
using Game.Core.Entities.AI;
using Game.Core.Inventory;
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
