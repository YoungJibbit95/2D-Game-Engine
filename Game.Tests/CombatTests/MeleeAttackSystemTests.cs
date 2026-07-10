using Game.Core.Combat;
using Game.Core.Entities;
using Game.Core.Events;
using Game.Core.Effects;
using Game.Core.Items;
using Game.Core.Loot;
using Game.Core.Physics;
using Game.Core.World;
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
        Assert.True(second.Blocked);
        Assert.Equal(GameplayActionFailureReason.Cooldown, second.FailureReason);
        Assert.Equal(sword.UseTime, second.CooldownRemaining, precision: 3);
        Assert.Equal(0f, second.CooldownProgress, precision: 3);
        Assert.True(third.Attacked);
    }

    [Fact]
    public void Attack_RejectsInvalidAimWithExplicitReason()
    {
        var entities = new EntityManager(spatialCellSize: 16);
        var player = new PlayerEntity(Vector2.Zero, new TileCollisionResolver());
        entities.Add(player);

        var result = CreateSystem().Attack(
            player,
            entities,
            CreateSword(damage: 5),
            CreateLootTables(),
            player.Body.Center);

        Assert.True(result.Blocked);
        Assert.Equal(GameplayActionFailureReason.InvalidTarget, result.FailureReason);
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

    [Fact]
    public void Attack_WithWorldLineOfSight_DoesNotHitThroughSolidTiles()
    {
        var world = new World(8, 4, WorldMetadata.CreateDefault(seed: 1));
        world.SetTile(1, 0, KnownTileIds.Stone);
        var entities = new EntityManager(spatialCellSize: 16);
        var player = new PlayerEntity(Vector2.Zero, new TileCollisionResolver());
        entities.Add(player);
        var enemy = CreateEnemy(new Vector2(24, 0), health: 20);
        entities.Add(enemy);

        var result = CreateSystem().Attack(player, entities, CreateSword(damage: 6), CreateLootTables(), new Vector2(64, 0), world);

        Assert.True(result.Attacked);
        Assert.Equal(0, result.Hits);
        Assert.Equal(20, enemy.Health.Current);
    }

    [Fact]
    public void Attack_UsesDataDrivenCircleShape()
    {
        var entities = new EntityManager(spatialCellSize: 16);
        var player = new PlayerEntity(Vector2.Zero, new TileCollisionResolver());
        entities.Add(player);
        var enemyAbovePlayer = CreateEnemy(new Vector2(0, -24), health: 20);
        entities.Add(enemyAbovePlayer);
        var shortSword = CreateSword(damage: 4) with
        {
            AttackShape = new AttackShapeDefinition
            {
                Kind = AttackShapeKind.Circle,
                Range = 40
            }
        };

        var result = CreateSystem().Attack(player, entities, shortSword, CreateLootTables(), new Vector2(64, 0));

        Assert.Equal(1, result.Hits);
        Assert.Equal(16, enemyAbovePlayer.Health.Current);
    }

    [Fact]
    public void Attack_AppliesItemOnHitStatusEffects()
    {
        var entities = new EntityManager(spatialCellSize: 16);
        var player = new PlayerEntity(Vector2.Zero, new TileCollisionResolver());
        entities.Add(player);
        var enemy = CreateEnemy(new Vector2(18, 0), health: 20);
        entities.Add(enemy);
        var poisonedSword = CreateSword(damage: 2) with
        {
            OnHitEffects = new[]
            {
                new StatusEffectApplication { EffectId = "poisoned" }
            }
        };
        var events = new GameEventBus();
        StatusEffectAppliedEvent? appliedEvent = null;
        events.Subscribe<StatusEffectAppliedEvent>(gameEvent => appliedEvent = gameEvent);

        var result = CreateSystem().Attack(
            player,
            entities,
            poisonedSword,
            CreateLootTables(),
            new Vector2(64, 0),
            events,
            statusEffectRegistry: CreateStatusEffects());

        Assert.Equal(1, result.StatusEffectsApplied);
        Assert.True(enemy.StatusEffects.HasEffect("poisoned"));
        Assert.NotNull(appliedEvent);
        Assert.Equal(StatusEffectSourceKind.Item, appliedEvent.SourceKind);
        Assert.Equal(poisonedSword.Id, appliedEvent.SourceId);
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

    private static StatusEffectRegistry CreateStatusEffects()
    {
        return StatusEffectRegistry.Create(new[]
        {
            new StatusEffectDefinition
            {
                Id = "poisoned",
                DisplayName = "Poisoned",
                Kind = StatusEffectKind.Debuff,
                DurationSeconds = 4,
                TickIntervalSeconds = 1,
                DamagePerTick = 1
            }
        });
    }
}
