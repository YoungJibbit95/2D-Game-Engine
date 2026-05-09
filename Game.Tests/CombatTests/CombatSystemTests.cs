using Game.Core.Combat;
using Game.Core.Entities;
using Game.Core.Events;
using Game.Core.Biomes;
using Game.Core.Crafting;
using Game.Core.Data;
using Game.Core.Effects;
using Game.Core.Inventory;
using Game.Core.Items;
using Game.Core.Loot;
using Game.Core.Physics;
using Game.Core.Projectiles;
using Game.Core.Spawning;
using Game.Core.Tiles;
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
    public void ResolveProjectileHits_WithContentAppliesProjectileStatusEffects()
    {
        var entities = new EntityManager(spatialCellSize: 16);
        var enemy = CreateEnemy(health: 20);
        var projectile = new ProjectileEntity("poison_arrow", new Vector2(0, 0), Vector2.Zero, 1, 0, 0, 5);
        entities.Add(enemy);
        entities.Add(projectile);

        var result = CreateCombatSystem().ResolveProjectileHits(entities, CreateContent(), events: null);

        Assert.Equal(1, result.StatusEffectsApplied);
        Assert.True(enemy.StatusEffects.HasEffect("poisoned"));
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

    [Fact]
    public void ResolveEnemyContactDamage_WithContentUsesEnemyContactDataAndEffects()
    {
        var entities = new EntityManager(spatialCellSize: 16);
        var player = new PlayerEntity(Vector2.Zero, new TileCollisionResolver());
        entities.Add(new EntityFactory(new TileCollisionResolver()).CreateEnemy(new EntityDefinition
        {
            Id = "poison_slime",
            DisplayName = "Poison Slime",
            TexturePath = "entities/slime",
            MaxHealth = 20,
            ContactDamage = 7,
            ContactKnockback = 50,
            OnContactEffects = new[] { new StatusEffectApplication { EffectId = "poisoned" } }
        }, Vector2.Zero));

        var result = CreateCombatSystem().ResolveEnemyContactDamage(player, entities, CreateContent(), events: null);

        Assert.Equal(1, result.ContactHits);
        Assert.Equal(7, result.DamageApplied);
        Assert.Equal(93, player.Health);
        Assert.True(player.StatusEffects.HasEffect("poisoned"));
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

    private static GameContentDatabase CreateContent()
    {
        return new GameContentDatabase(
            TileRegistry.Create(Array.Empty<TileDefinition>()),
            ItemRegistry.Create(Array.Empty<ItemDefinition>()),
            RecipeRegistry.Create(Array.Empty<RecipeDefinition>()),
            CreateLootTables(),
            BiomeRegistry.Create(Array.Empty<BiomeDefinition>()),
            ProjectileRegistry.Create(new[]
            {
                new ProjectileDefinition
                {
                    Id = "poison_arrow",
                    TexturePath = "projectiles/poison_arrow",
                    Speed = 100,
                    Damage = 1,
                    Lifetime = 5,
                    OnHitEffects = new[] { new StatusEffectApplication { EffectId = "poisoned" } }
                }
            }),
            EntityDefinitionRegistry.Create(Array.Empty<EntityDefinition>()),
            SpawnRuleRegistry.Create(Array.Empty<SpawnRuleDefinition>()))
        {
            StatusEffects = CreateStatusEffects()
        };
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
