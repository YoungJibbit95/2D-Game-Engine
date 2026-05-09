using Game.Core.Data;
using Game.Core.Entities;
using Game.Core.Events;
using Game.Core.Effects;
using Game.Core.Inventory;
using Game.Core.Loot;
using Game.Core.Physics;
using Game.Core.Projectiles;

namespace Game.Core.Combat;

public sealed class CombatSystem
{
    private readonly LootRoller _lootRoller;
    private readonly TileCollisionResolver _collisionResolver;
    private readonly StatusEffectApplier _statusEffects;

    public CombatSystem(LootRoller lootRoller, TileCollisionResolver collisionResolver, StatusEffectApplier? statusEffects = null)
    {
        _lootRoller = lootRoller;
        _collisionResolver = collisionResolver;
        _statusEffects = statusEffects ?? new StatusEffectApplier();
    }

    public CombatResolutionResult ResolveProjectileHits(EntityManager entities, LootTableRegistry lootTables, GameEventBus? events = null)
    {
        return ResolveProjectileHits(entities, lootTables, projectiles: null, statusEffects: null, events);
    }

    public CombatResolutionResult ResolveProjectileHits(EntityManager entities, GameContentDatabase content, GameEventBus? events = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        return ResolveProjectileHits(entities, content.LootTables, content.Projectiles, content.StatusEffects, events);
    }

    private CombatResolutionResult ResolveProjectileHits(
        EntityManager entities,
        LootTableRegistry lootTables,
        ProjectileRegistry? projectiles,
        StatusEffectRegistry? statusEffects,
        GameEventBus? events)
    {
        ArgumentNullException.ThrowIfNull(entities);
        ArgumentNullException.ThrowIfNull(lootTables);

        var projectileHits = 0;
        var enemyDeaths = 0;
        var droppedItems = 0;
        var effectsApplied = 0;
        var pendingDrops = new List<DroppedItemEntity>();

        foreach (var projectile in entities.Entities.OfType<ProjectileEntity>().Where(entity => entity.IsActive).ToArray())
        {
            foreach (var enemy in entities.Query(projectile.Bounds).OfType<EnemyEntity>().Where(entity => entity.IsActive).ToArray())
            {
                if (projectile.OwnerEntityId == enemy.Id || !projectile.Bounds.Intersects(enemy.Bounds))
                {
                    continue;
                }

                projectileHits++;
                var damageApplied = enemy.ApplyDamage(projectile.DamageInfo);
                events?.Publish(new ProjectileHitEvent(projectile.Id, enemy.Id, projectile.Damage));
                if (damageApplied &&
                    projectiles is not null &&
                    statusEffects is not null &&
                    projectiles.TryGetById(projectile.ProjectileId, out var projectileDefinition))
                {
                    effectsApplied += _statusEffects.Apply(enemy.StatusEffects, statusEffects, projectileDefinition.OnHitEffects);
                }

                projectile.RegisterHit();

                if (damageApplied && enemy.Health.IsDead)
                {
                    enemy.IsActive = false;
                    enemyDeaths++;
                    events?.Publish(new EntityDiedEvent(enemy.Id, enemy.DefinitionId));
                    droppedItems += AddLootDrops(enemy, lootTables, pendingDrops);
                }

                if (!projectile.IsActive)
                {
                    break;
                }
            }
        }

        foreach (var drop in pendingDrops)
        {
            entities.Add(drop);
        }

        return new CombatResolutionResult(projectileHits, enemyDeaths, droppedItems, effectsApplied);
    }

    public ContactDamageResult ResolveEnemyContactDamage(
        PlayerEntity player,
        EntityManager entities,
        int damage = 10,
        float invulnerabilitySeconds = 0.65f,
        float knockbackForce = 180f,
        GameEventBus? events = null)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(entities);

        if (damage <= 0 || player.HealthComponent.IsDead)
        {
            return ContactDamageResult.None;
        }

        foreach (var enemy in entities.Query(player.Bounds).OfType<EnemyEntity>().Where(entity => entity.IsActive).ToArray())
        {
            if (!enemy.Bounds.Intersects(player.Bounds))
            {
                continue;
            }

            var direction = player.Body.Center - enemy.Body.Center;
            if (direction == System.Numerics.Vector2.Zero)
            {
                direction = System.Numerics.Vector2.UnitY * -1;
            }

            var damageInfo = new DamageInfo(damage, DamageType.Contact, enemy.Id, direction, knockbackForce);
            if (!player.ApplyDamage(damageInfo, invulnerabilitySeconds))
            {
                return ContactDamageResult.None;
            }

            events?.Publish(new PlayerDamagedEvent(damage, player.Health, player.MaxHealth, enemy.Id));
            return new ContactDamageResult(1, damage, player.HealthComponent.IsDead);
        }

        return ContactDamageResult.None;
    }

    public ContactDamageResult ResolveEnemyContactDamage(
        PlayerEntity player,
        EntityManager entities,
        GameContentDatabase content,
        float invulnerabilitySeconds = 0.65f,
        GameEventBus? events = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        return ResolveEnemyContactDamageInternal(player, entities, content.StatusEffects, invulnerabilitySeconds, events);
    }

    private ContactDamageResult ResolveEnemyContactDamageInternal(
        PlayerEntity player,
        EntityManager entities,
        StatusEffectRegistry statusEffects,
        float invulnerabilitySeconds,
        GameEventBus? events)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(entities);
        ArgumentNullException.ThrowIfNull(statusEffects);

        if (player.HealthComponent.IsDead)
        {
            return ContactDamageResult.None;
        }

        foreach (var enemy in entities.Query(player.Bounds).OfType<EnemyEntity>().Where(entity => entity.IsActive).ToArray())
        {
            if (!enemy.Bounds.Intersects(player.Bounds) || enemy.ContactDamage <= 0)
            {
                continue;
            }

            var direction = player.Body.Center - enemy.Body.Center;
            if (direction == System.Numerics.Vector2.Zero)
            {
                direction = System.Numerics.Vector2.UnitY * -1;
            }

            var damageInfo = new DamageInfo(enemy.ContactDamage, DamageType.Contact, enemy.Id, direction, enemy.ContactKnockback);
            if (!player.ApplyDamage(damageInfo, invulnerabilitySeconds))
            {
                return ContactDamageResult.None;
            }

            _statusEffects.Apply(player.StatusEffects, statusEffects, enemy.OnContactEffects);
            events?.Publish(new PlayerDamagedEvent(enemy.ContactDamage, player.Health, player.MaxHealth, enemy.Id));
            return new ContactDamageResult(1, enemy.ContactDamage, player.HealthComponent.IsDead);
        }

        return ContactDamageResult.None;
    }

    private int AddLootDrops(EnemyEntity enemy, LootTableRegistry lootTables, List<DroppedItemEntity> pendingDrops)
    {
        if (string.IsNullOrWhiteSpace(enemy.LootTableId) || !lootTables.TryGetById(enemy.LootTableId, out var table))
        {
            return 0;
        }

        var count = 0;
        foreach (var drop in _lootRoller.Roll(table))
        {
            if (drop.IsEmpty)
            {
                continue;
            }

            pendingDrops.Add(new DroppedItemEntity(drop, enemy.Body.Position, _collisionResolver));
            count++;
        }

        return count;
    }
}
