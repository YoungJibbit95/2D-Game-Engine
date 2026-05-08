using Game.Core.Entities;
using Game.Core.Events;
using Game.Core.Inventory;
using Game.Core.Loot;
using Game.Core.Physics;
using Game.Core.Projectiles;

namespace Game.Core.Combat;

public sealed class CombatSystem
{
    private readonly LootRoller _lootRoller;
    private readonly TileCollisionResolver _collisionResolver;

    public CombatSystem(LootRoller lootRoller, TileCollisionResolver collisionResolver)
    {
        _lootRoller = lootRoller;
        _collisionResolver = collisionResolver;
    }

    public CombatResolutionResult ResolveProjectileHits(EntityManager entities, LootTableRegistry lootTables, GameEventBus? events = null)
    {
        ArgumentNullException.ThrowIfNull(entities);
        ArgumentNullException.ThrowIfNull(lootTables);

        var projectileHits = 0;
        var enemyDeaths = 0;
        var droppedItems = 0;
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

        return new CombatResolutionResult(projectileHits, enemyDeaths, droppedItems);
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
