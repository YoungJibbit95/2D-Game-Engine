using Game.Core.Combat;
using Game.Core.Entities;
using Game.Core.Events;
using Game.Core.Items;
using Game.Core.Loot;
using Game.Core.Physics;
using System.Numerics;
using Xunit;

namespace Game.Tests.CombatTests;

public sealed class MeleeAttackSystemTests
{
    [Fact]
    public void Attack_DamagesEnemyInsideHitbox()
    {
        var entities = new EntityManager(spatialCellSize: 16);
        var player = new PlayerEntity(Vector2.Zero, new TileCollisionResolver());
        entities.Add(player);
        var enemy = CreateEnemy(new Vector2(18, 0), health: 20);
        entities.Add(enemy);
        MeleeHitEvent? hit = null;
        var events = new GameEventBus();
        events.Subscribe<MeleeHitEvent>(gameEvent => hit = gameEvent);

        var result = CreateSystem().Attack(player, entities, CreateSword(damage: 6), CreateLootTables(), new Vector2(64, 0), events);

        Assert.True(result.Attacked);
        Assert.Equal(1, result.Hits);
        Assert.Equal(14, enemy.Health.Current);
        Assert.NotNull(hit);
        Assert.Equal(enemy.Id, hit.TargetEntityId);
    }

    [Fact]
    public void Attack_RespectsCooldown()
    {
        var entities = new EntityManager(spatialCellSize: 16);
        var player = new PlayerEntity(Vector2.Zero, new TileCollisionResolver());
        entities.Add(player);
        entities.Add(CreateEnemy(new Vector2(18, 0), health: 20));
        var system = CreateSystem();
        var sword = CreateSword(damage: 5);

        var first = system.Attack(player, entities, sword, CreateLootTables(), new Vector2(64, 0));
        var second = system.Attack(player, entities, sword, CreateLootTables(), new Vector2(64, 0));
        system.Update(sword.UseTime);
        var third = system.Attack(player, entities, sword, CreateLootTables(), new Vector2(64, 0));

        Assert.True(first.Attacked);
        Assert.False(second.Attacked);
        Assert.True(third.Attacked);
    }

    [Fact]
    public void Attack_DropsLootWhenEnemyDies()
    {
        var entities = new EntityManager(spatialCellSize: 16);
        var player = new PlayerEntity(Vector2.Zero, new TileCollisionResolver());
        entities.Add(player);
        entities.Add(CreateEnemy(new Vector2(18, 0), health: 5));

        var result = CreateSystem().Attack(player, entities, CreateSword(damage: 5), CreateLootTables(), new Vector2(64, 0));

        Assert.Equal(1, result.EnemyDeaths);
        Assert.Equal(1, result.DroppedItems);
        Assert.Contains(entities.Entities, entity => entity is DroppedItemEntity);
    }

    private static MeleeAttackSystem CreateSystem()
    {
        return new MeleeAttackSystem(new LootRoller(new Random(1)), new TileCollisionResolver());
    }

    private static ItemDefinition CreateSword(int damage)
    {
        return new ItemDefinition
        {
            Id = "copper_sword",
            DisplayName = "Copper Sword",
            Type = ItemType.WeaponMelee,
            TexturePath = "items/copper_sword",
            MaxStack = 1,
            UseTime = 0.25f,
            Damage = damage,
            Knockback = 60
        };
    }

    private static EnemyEntity CreateEnemy(Vector2 position, int health)
    {
        return new EntityFactory(new TileCollisionResolver()).CreateEnemy(new EntityDefinition
        {
            Id = "slime",
            DisplayName = "Slime",
            TexturePath = "entities/slime",
            MaxHealth = 20,
            LootTableId = "slime_basic"
        }, position, currentHealth: health);
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
