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
using Game.Core.Randomness;
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
    public void ResolveProjectileHits_PiercingProjectileHitsSameEnemyExactlyOnce()
    {
        var entities = new EntityManager(spatialCellSize: 16);
        var enemy = CreateEnemy(health: 20);
        var projectile = new ProjectileEntity("arrow", Vector2.Zero, Vector2.Zero, 3, 0, 2, 5);
        entities.Add(enemy);
        entities.Add(projectile);
        var combat = CreateCombatSystem();

        var first = combat.ResolveProjectileHits(entities, CreateLootTables());
        var second = combat.ResolveProjectileHits(entities, CreateLootTables());

        Assert.Equal(1, first.ProjectileHits);
        Assert.Equal(0, second.ProjectileHits);
        Assert.Equal(17, enemy.Health.Current);
        Assert.True(projectile.IsActive);
        Assert.Equal(1, projectile.Pierce);
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

    [Fact]
    public void ResolveEnemyContactDamage_WithGuardReturnsParryWithoutApplyingDamage()
    {
        var entities = new EntityManager(spatialCellSize: 16);
        var player = new PlayerEntity(Vector2.Zero, new TileCollisionResolver());
        var enemy = CreateEnemy(health: 20);
        entities.Add(enemy);
        var guard = new GuardRuntimeState(new GuardDefinition());
        Assert.True(guard.TryBeginGuard(enemy.Body.Center - player.Body.Center));

        var result = CreateCombatSystem().ResolveEnemyContactDamage(
            player,
            entities,
            guard,
            CreateDamageResolver(),
            damage: 12);

        Assert.Equal(1, result.ContactHits);
        Assert.True(result.Parried);
        Assert.Equal(CombatHitOutcome.Parried, result.Outcome);
        Assert.Equal(0, result.DamageApplied);
        Assert.Equal(100, player.Health);
        Assert.NotNull(result.Resolution);
        Assert.Contains(result.Resolution.Value.Events, gameEvent => gameEvent is CombatParriedEvent);
    }

    [Fact]
    public void ResolveEnemyContactDamage_WithGuardReturnsBlockedActualDamage()
    {
        var entities = new EntityManager(spatialCellSize: 16);
        var player = new PlayerEntity(Vector2.Zero, new TileCollisionResolver());
        var enemy = CreateEnemy(health: 20);
        entities.Add(enemy);
        var guard = new GuardRuntimeState(new GuardDefinition());
        Assert.True(guard.TryBeginGuard(enemy.Body.Center - player.Body.Center));
        guard.Update(0.2f);

        var result = CreateCombatSystem().ResolveEnemyContactDamage(
            player,
            entities,
            guard,
            CreateDamageResolver(),
            damage: 12);

        Assert.True(result.Blocked);
        Assert.Equal(3, result.DamageApplied);
        Assert.Equal(9, result.DamagePrevented);
        Assert.Equal(12, result.GuardStaminaSpent);
        Assert.Equal(97, player.Health);
    }

    [Fact]
    public void ResolveEnemyContactDamage_WithGuardReturnsGuardBreakAndActualDamage()
    {
        var entities = new EntityManager(spatialCellSize: 16);
        var player = new PlayerEntity(Vector2.Zero, new TileCollisionResolver());
        var enemy = CreateEnemy(health: 20);
        entities.Add(enemy);
        var guard = new GuardRuntimeState(new GuardDefinition(), stamina: 5);
        Assert.True(guard.TryBeginGuard(enemy.Body.Center - player.Body.Center));
        guard.Update(0.2f);

        var result = CreateCombatSystem().ResolveEnemyContactDamage(
            player,
            entities,
            guard,
            CreateDamageResolver(),
            damage: 12);

        Assert.True(result.GuardBroken);
        Assert.Equal(12, result.DamageApplied);
        Assert.Equal(5, result.GuardStaminaSpent);
        Assert.Equal(88, player.Health);
        Assert.True(guard.IsGuardBroken);
    }

    [Fact]
    public void ResolveProjectileDamageAgainstPlayer_QueriesAndAppliesHostileProjectile()
    {
        var entities = new EntityManager(spatialCellSize: 16);
        var player = new PlayerEntity(Vector2.Zero, new TileCollisionResolver());
        var projectile = new ProjectileEntity(
            new ProjectileDefinition
            {
                Id = "hostile-bolt",
                TexturePath = "projectiles/hostile-bolt",
                Speed = 100,
                Damage = 8,
                DamageType = DamageType.Magic,
                Lifetime = 5
            },
            Vector2.Zero,
            Vector2.UnitX,
            ownerEntityId: 99,
            ownerFaction: EntityFaction.Hostile);
        entities.Add(projectile);

        var result = CreateCombatSystem().ResolveProjectileDamageAgainstPlayer(
            player,
            entities,
            CreateContent(),
            new GuardRuntimeState(new GuardDefinition()),
            CreateDamageResolver());

        Assert.Equal(1, result.ContactHits);
        Assert.Equal(CombatHitOutcome.Applied, result.Outcome);
        Assert.Equal(8, result.DamageApplied);
        Assert.Equal(92, player.Health);
        Assert.False(projectile.IsActive);
    }

    [Fact]
    public void ResolvePlayerDamage_HandlesProjectileAndContactThroughCombinedEntryPoint()
    {
        var entities = new EntityManager(spatialCellSize: 16);
        var player = new PlayerEntity(Vector2.Zero, new TileCollisionResolver());
        entities.Add(CreateEnemy(health: 20));
        var projectile = new ProjectileEntity(
            new ProjectileDefinition
            {
                Id = "hostile-bolt",
                TexturePath = "projectiles/hostile-bolt",
                Speed = 100,
                Damage = 8,
                Lifetime = 5
            },
            Vector2.Zero,
            Vector2.UnitX,
            ownerEntityId: 99,
            ownerFaction: EntityFaction.Hostile);
        entities.Add(projectile);

        var result = CreateCombatSystem().ResolvePlayerDamage(
            player,
            entities,
            CreateContent(),
            new GuardRuntimeState(new GuardDefinition()),
            CreateDamageResolver());

        Assert.Equal(1, result.ContactHits);
        Assert.Equal(8, result.DamageApplied);
        Assert.Equal(92, player.Health);
        Assert.False(projectile.IsActive);
    }

    private static CombatSystem CreateCombatSystem()
    {
        return new CombatSystem(new LootRoller(new Random(1)), new TileCollisionResolver());
    }

    private static CombatDamageResolver CreateDamageResolver()
    {
        var randoms = new SessionRandomRegistry(9173);
        return new CombatDamageResolver(
            randoms.GetStream("combat.critical"),
            randoms.GetStream("combat.status-resolution"));
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
