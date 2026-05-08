using Game.Core.Combat;
using Game.Core.Entities;
using Game.Core.Events;
using Game.Core.Loot;
using Game.Core.Physics;
using Game.Core.Projectiles;
using System.Numerics;
using Xunit;

namespace Game.Tests.CombatTests;

public sealed class CombatSystemTests
{
    [Fact]
    public void ResolveProjectileHits_DamagesEnemyAndConsumesProjectile()
    {
        var entities = new EntityManager(spatialCellSize: 16);
        var enemy = CreateEnemy(health: 20);
        var projectile = new ProjectileEntity("arrow", new Vector2(0, 0), Vector2.Zero, 5, 0, 0, 5);
        entities.Add(enemy);
        entities.Add(projectile);

        var result = CreateCombatSystem().ResolveProjectileHits(entities, CreateLootTables());

        Assert.Equal(1, result.ProjectileHits);
        Assert.Equal(15, enemy.Health.Current);
        Assert.False(projectile.IsActive);
    }

    [Fact]
    public void ResolveProjectileHits_DropsLootWhenEnemyDies()
    {
        var entities = new EntityManager(spatialCellSize: 16);
        var enemy = CreateEnemy(health: 5);
        var projectile = new ProjectileEntity("arrow", new Vector2(0, 0), Vector2.Zero, 5, 0, 0, 5);
        entities.Add(enemy);
        entities.Add(projectile);

        var result = CreateCombatSystem().ResolveProjectileHits(entities, CreateLootTables());

        Assert.Equal(1, result.EnemyDeaths);
        Assert.Equal(1, result.DroppedItems);
        Assert.Contains(entities.Entities, entity => entity is DroppedItemEntity);
    }

    [Fact]
    public void ResolveEnemyContactDamage_DamagesPlayerAndPublishesEvent()
    {
        var entities = new EntityManager(spatialCellSize: 16);
        var enemy = CreateEnemy(health: 20);
        var player = new PlayerEntity(Vector2.Zero, new TileCollisionResolver());
        entities.Add(enemy);
        var events = new GameEventBus();
        PlayerDamagedEvent? received = null;
        events.Subscribe<PlayerDamagedEvent>(gameEvent => received = gameEvent);

        var result = CreateCombatSystem().ResolveEnemyContactDamage(player, entities, damage: 12, events: events);

        Assert.Equal(1, result.ContactHits);
        Assert.Equal(12, result.DamageApplied);
        Assert.Equal(88, player.Health);
        Assert.NotEqual(Vector2.Zero, player.Body.Velocity);
        Assert.NotNull(received);
        Assert.Equal(12, received.Damage);
        Assert.Equal(enemy.Id, received.SourceEntityId);
    }

    [Fact]
    public void ResolveEnemyContactDamage_RespectsPlayerInvulnerability()
    {
        var entities = new EntityManager(spatialCellSize: 16);
        var player = new PlayerEntity(Vector2.Zero, new TileCollisionResolver());
        entities.Add(CreateEnemy(health: 20));

        var combat = CreateCombatSystem();
        var first = combat.ResolveEnemyContactDamage(player, entities, damage: 10);
        var second = combat.ResolveEnemyContactDamage(player, entities, damage: 10);

        Assert.Equal(1, first.ContactHits);
        Assert.Equal(ContactDamageResult.None, second);
        Assert.Equal(90, player.Health);
    }

    private static CombatSystem CreateCombatSystem()
    {
        return new CombatSystem(new LootRoller(new Random(1)), new TileCollisionResolver());
    }

    private static EnemyEntity CreateEnemy(int health)
    {
        return new EntityFactory(new TileCollisionResolver()).CreateEnemy(new EntityDefinition
        {
            Id = "slime",
            DisplayName = "Slime",
            TexturePath = "entities/slime",
            MaxHealth = 20,
            LootTableId = "slime_basic"
        }, Vector2.Zero, currentHealth: health);
    }

    private static LootTableRegistry CreateLootTables()
    {
        return LootTableRegistry.Create(new[]
        {
            new LootTableDefinition
            {
                Id = "slime_basic",
                Entries = new[]
                {
                    new LootEntryDefinition { ItemId = "gel", Min = 1, Max = 1, Chance = 1f }
                }
            }
        });
    }
}
