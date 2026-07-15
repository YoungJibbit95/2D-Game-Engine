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
    private readonly List<Entity> _queryBuffer = new();
    private readonly HashSet<Entity> _querySeen = new();
    private readonly List<DroppedItemEntity> _pendingDrops = new();

    public CombatSystem(LootRoller lootRoller, TileCollisionResolver collisionResolver, StatusEffectApplier? statusEffects = null)
    {
        _lootRoller = lootRoller;
        _collisionResolver = collisionResolver;
        _statusEffects = statusEffects ?? new StatusEffectApplier();
    }

    public CombatResolutionResult ResolveProjectileHits(
        EntityManager entities,
        LootTableRegistry lootTables,
        GameEventBus? events = null,
        LootKillContext? lootContext = null)
    {
        return ResolveProjectileHits(entities, lootTables, projectiles: null, statusEffects: null, events, lootContext);
    }

    public CombatResolutionResult ResolveProjectileHits(
        EntityManager entities,
        GameContentDatabase content,
        GameEventBus? events = null,
        LootKillContext? lootContext = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        return ResolveProjectileHits(
            entities,
            content.LootTables,
            content.Projectiles,
            content.StatusEffects,
            events,
            lootContext);
    }

    private CombatResolutionResult ResolveProjectileHits(
        EntityManager entities,
        LootTableRegistry lootTables,
        ProjectileRegistry? projectiles,
        StatusEffectRegistry? statusEffects,
        GameEventBus? events,
        LootKillContext? lootContext)
    {
        ArgumentNullException.ThrowIfNull(entities);
        ArgumentNullException.ThrowIfNull(lootTables);

        var projectileHits = 0;
        var enemyDeaths = 0;
        var droppedItems = 0;
        var effectsApplied = 0;
        _pendingDrops.Clear();

        for (var projectileIndex = 0; projectileIndex < entities.Entities.Count; projectileIndex++)
        {
            if (entities.Entities[projectileIndex] is not ProjectileEntity { IsActive: true } projectile)
            {
                continue;
            }

            entities.QueryInto(projectile.Bounds, _queryBuffer, _querySeen);
            for (var enemyIndex = 0; enemyIndex < _queryBuffer.Count; enemyIndex++)
            {
                if (_queryBuffer[enemyIndex] is not EnemyEntity { IsActive: true } enemy)
                {
                    continue;
                }

                if (!projectile.Bounds.Intersects(enemy.Bounds))
                {
                    continue;
                }

                var collision = projectile.ResolveEntityCollision(
                    new ProjectileEntityCollision(enemy.Id, enemy.Faction));
                if (!collision.Accepted || collision.DamageRequest is not { } damageRequest)
                {
                    continue;
                }

                projectileHits++;
                var damageApplied = enemy.ApplyDamage(new DamageInfo(
                    damageRequest.BaseDamage,
                    damageRequest.DamageType,
                    damageRequest.SourceEntityId,
                    damageRequest.ImpactDirection,
                    damageRequest.KnockbackForce));
                events?.Publish(new ProjectileHitEvent(projectile.Id, enemy.Id, damageRequest.BaseDamage));
                if (damageApplied &&
                    projectiles is not null &&
                    statusEffects is not null &&
                    projectiles.TryGetById(projectile.ProjectileId, out var projectileDefinition))
                {
                    var effectResult = _statusEffects.ApplyDetailed(
                        enemy.StatusEffects,
                        statusEffects,
                        projectileDefinition.OnHitEffects);
                    effectsApplied += effectResult.AppliedCount;
                    PublishStatusEffects(
                        events,
                        enemy.Id,
                        StatusEffectSourceKind.Projectile,
                        projectile.ProjectileId,
                        effectResult);
                }

                if (damageApplied && enemy.Health.IsDead)
                {
                    enemy.IsActive = false;
                    enemyDeaths++;
                    events?.Publish(new EntityDiedEvent(enemy.Id, enemy.DefinitionId));
                    droppedItems += AddLootDrops(enemy, lootTables, _pendingDrops, lootContext);
                }

                if (!projectile.IsActive)
                {
                    break;
                }
            }
        }

        foreach (var drop in _pendingDrops)
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

        entities.QueryInto(player.Bounds, _queryBuffer, _querySeen);
        for (var index = 0; index < _queryBuffer.Count; index++)
        {
            if (_queryBuffer[index] is not EnemyEntity { IsActive: true } enemy)
            {
                continue;
            }

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
        GuardRuntimeState guard,
        CombatDamageResolver damageResolver,
        int damage = 10,
        float invulnerabilitySeconds = 0.65f,
        float knockbackForce = 180f,
        DamageMitigationProfile? mitigation = null,
        CombatResolutionPolicy? policy = null,
        GameEventBus? events = null)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(entities);
        ArgumentNullException.ThrowIfNull(guard);
        ArgumentNullException.ThrowIfNull(damageResolver);
        if (damage <= 0 || CannotReceiveDamage(player))
        {
            return ContactDamageResult.None;
        }

        entities.QueryInto(player.Bounds, _queryBuffer, _querySeen);
        for (var index = 0; index < _queryBuffer.Count; index++)
        {
            if (_queryBuffer[index] is not EnemyEntity { IsActive: true } enemy ||
                !enemy.Bounds.Intersects(player.Bounds))
            {
                continue;
            }

            var direction = ResolveImpactDirection(player, enemy);
            var resolution = damageResolver.Resolve(new CombatDamageRequest
            {
                SourceEntityId = enemy.Id,
                TargetEntityId = player.Id,
                SourceFaction = enemy.Faction,
                TargetFaction = EntityFaction.Friendly,
                BaseDamage = damage,
                DamageType = DamageType.Contact,
                ImpactDirection = direction,
                KnockbackForce = knockbackForce
            }, mitigation, guard, policy);
            return ApplyPlayerResolution(
                player,
                resolution,
                invulnerabilitySeconds,
                enemy.Id,
                DamageType.Contact,
                events);
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

    public ContactDamageResult ResolveEnemyContactDamage(
        PlayerEntity player,
        EntityManager entities,
        GameContentDatabase content,
        GuardRuntimeState guard,
        CombatDamageResolver damageResolver,
        float invulnerabilitySeconds = 0.65f,
        DamageMitigationProfile? mitigation = null,
        CombatResolutionPolicy? policy = null,
        GameEventBus? events = null)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(entities);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(guard);
        ArgumentNullException.ThrowIfNull(damageResolver);
        if (CannotReceiveDamage(player))
        {
            return ContactDamageResult.None;
        }

        entities.QueryInto(player.Bounds, _queryBuffer, _querySeen);
        for (var index = 0; index < _queryBuffer.Count; index++)
        {
            if (_queryBuffer[index] is not EnemyEntity { IsActive: true } enemy ||
                !enemy.Bounds.Intersects(player.Bounds) ||
                enemy.ContactDamage <= 0)
            {
                continue;
            }

            return ResolveEnemyContactCandidate(
                player,
                enemy,
                content,
                guard,
                damageResolver,
                invulnerabilitySeconds,
                mitigation,
                policy,
                events);
        }

        return ContactDamageResult.None;
    }

    public ContactDamageResult ResolvePlayerDamage(
        PlayerEntity player,
        EntityManager entities,
        GameContentDatabase content,
        GuardRuntimeState guard,
        CombatDamageResolver damageResolver,
        float invulnerabilitySeconds = 0.65f,
        DamageMitigationProfile? mitigation = null,
        CombatResolutionPolicy? policy = null,
        GameEventBus? events = null)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(entities);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(guard);
        ArgumentNullException.ThrowIfNull(damageResolver);
        if (CannotReceiveDamage(player))
        {
            return ContactDamageResult.None;
        }

        entities.QueryInto(player.Bounds, _queryBuffer, _querySeen);
        for (var index = 0; index < _queryBuffer.Count; index++)
        {
            if (_queryBuffer[index] is not ProjectileEntity { IsActive: true } projectile ||
                !projectile.Bounds.Intersects(player.Bounds))
            {
                continue;
            }

            var projectileResult = ResolveProjectileCandidate(
                player,
                projectile,
                content,
                guard,
                damageResolver,
                invulnerabilitySeconds,
                mitigation,
                policy,
                events);
            if (projectileResult.ContactHits > 0)
            {
                return projectileResult;
            }
        }

        for (var index = 0; index < _queryBuffer.Count; index++)
        {
            if (_queryBuffer[index] is not EnemyEntity { IsActive: true } enemy ||
                !enemy.Bounds.Intersects(player.Bounds) ||
                enemy.ContactDamage <= 0)
            {
                continue;
            }

            return ResolveEnemyContactCandidate(
                player,
                enemy,
                content,
                guard,
                damageResolver,
                invulnerabilitySeconds,
                mitigation,
                policy,
                events);
        }

        return ContactDamageResult.None;
    }

    public ContactDamageResult ResolveProjectileDamageAgainstPlayer(
        PlayerEntity player,
        EntityManager entities,
        GameContentDatabase content,
        GuardRuntimeState guard,
        CombatDamageResolver damageResolver,
        float invulnerabilitySeconds = 0.65f,
        DamageMitigationProfile? mitigation = null,
        CombatResolutionPolicy? policy = null,
        GameEventBus? events = null)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(entities);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(guard);
        ArgumentNullException.ThrowIfNull(damageResolver);
        if (CannotReceiveDamage(player))
        {
            return ContactDamageResult.None;
        }

        entities.QueryInto(player.Bounds, _queryBuffer, _querySeen);
        for (var index = 0; index < _queryBuffer.Count; index++)
        {
            if (_queryBuffer[index] is not ProjectileEntity { IsActive: true } projectile ||
                !projectile.Bounds.Intersects(player.Bounds))
            {
                continue;
            }

            var result = ResolveProjectileCandidate(
                player,
                projectile,
                content,
                guard,
                damageResolver,
                invulnerabilitySeconds,
                mitigation,
                policy,
                events);
            if (result.ContactHits > 0)
            {
                return result;
            }
        }

        return ContactDamageResult.None;
    }

    private ContactDamageResult ResolveEnemyContactCandidate(
        PlayerEntity player,
        EnemyEntity enemy,
        GameContentDatabase content,
        GuardRuntimeState guard,
        CombatDamageResolver damageResolver,
        float invulnerabilitySeconds,
        DamageMitigationProfile? mitigation,
        CombatResolutionPolicy? policy,
        GameEventBus? events)
    {
        var resolution = damageResolver.Resolve(new CombatDamageRequest
        {
            SourceEntityId = enemy.Id,
            TargetEntityId = player.Id,
            SourceFaction = enemy.Faction,
            TargetFaction = EntityFaction.Friendly,
            BaseDamage = enemy.ContactDamage,
            DamageType = DamageType.Contact,
            ImpactDirection = ResolveImpactDirection(player, enemy),
            KnockbackForce = enemy.ContactKnockback,
            StatusEffects = enemy.OnContactEffects
        }, mitigation, guard, policy);
        var result = ApplyPlayerResolution(
            player,
            resolution,
            invulnerabilitySeconds,
            enemy.Id,
            DamageType.Contact,
            events);
        if (result.DamageApplied > 0 && resolution.StatusEffects.Count > 0)
        {
            var effectResult = _statusEffects.ApplyDetailed(
                player.StatusEffects,
                content.StatusEffects,
                resolution.StatusEffects);
            PublishStatusEffects(
                events,
                player.Id,
                StatusEffectSourceKind.Entity,
                enemy.DefinitionId,
                effectResult);
        }

        return result;
    }

    private ContactDamageResult ResolveProjectileCandidate(
        PlayerEntity player,
        ProjectileEntity projectile,
        GameContentDatabase content,
        GuardRuntimeState guard,
        CombatDamageResolver damageResolver,
        float invulnerabilitySeconds,
        DamageMitigationProfile? mitigation,
        CombatResolutionPolicy? policy,
        GameEventBus? events)
    {
        var collision = projectile.ResolveEntityCollision(
            new ProjectileEntityCollision(player.Id, EntityFaction.Friendly));
        if (!collision.Accepted || collision.DamageRequest is not { } request)
        {
            return ContactDamageResult.None;
        }

        var resolution = damageResolver.Resolve(request, mitigation, guard, policy);
        var result = ApplyPlayerResolution(
            player,
            resolution,
            invulnerabilitySeconds,
            projectile.OwnerEntityId,
            request.DamageType,
            events);
        events?.Publish(new ProjectileHitEvent(
            projectile.Id,
            player.Id,
            result.DamageApplied));
        if (result.DamageApplied > 0 && resolution.StatusEffects.Count > 0)
        {
            var effectResult = _statusEffects.ApplyDetailed(
                player.StatusEffects,
                content.StatusEffects,
                resolution.StatusEffects);
            PublishStatusEffects(
                events,
                player.Id,
                StatusEffectSourceKind.Projectile,
                projectile.ProjectileId,
                effectResult);
        }

        return result;
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

        entities.QueryInto(player.Bounds, _queryBuffer, _querySeen);
        for (var index = 0; index < _queryBuffer.Count; index++)
        {
            if (_queryBuffer[index] is not EnemyEntity { IsActive: true } enemy)
            {
                continue;
            }

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

            var effectResult = _statusEffects.ApplyDetailed(player.StatusEffects, statusEffects, enemy.OnContactEffects);
            PublishStatusEffects(
                events,
                player.Id,
                StatusEffectSourceKind.Entity,
                enemy.DefinitionId,
                effectResult);
            events?.Publish(new PlayerDamagedEvent(enemy.ContactDamage, player.Health, player.MaxHealth, enemy.Id));
            return new ContactDamageResult(1, enemy.ContactDamage, player.HealthComponent.IsDead);
        }

        return ContactDamageResult.None;
    }

    private int AddLootDrops(
        EnemyEntity enemy,
        LootTableRegistry lootTables,
        List<DroppedItemEntity> pendingDrops,
        LootKillContext? lootContext)
    {
        if (string.IsNullOrWhiteSpace(enemy.LootTableId) || !lootTables.TryGetById(enemy.LootTableId, out var table))
        {
            return 0;
        }

        var count = 0;
        foreach (var drop in _lootRoller.Roll(table, lootContext ?? LootKillContext.Empty))
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

    private static ContactDamageResult ApplyPlayerResolution(
        PlayerEntity player,
        CombatHitResult resolution,
        float invulnerabilitySeconds,
        int? sourceEntityId,
        DamageType damageType,
        GameEventBus? events)
    {
        var healthBefore = player.Health;
        if (resolution.DamageApplied > 0)
        {
            var knockbackForce = resolution.Knockback.Length();
            var knockbackDirection = knockbackForce > 0
                ? resolution.Knockback / knockbackForce
                : System.Numerics.Vector2.Zero;
            _ = player.ApplyDamage(new DamageInfo(
                resolution.DamageApplied,
                damageType,
                sourceEntityId,
                knockbackDirection,
                knockbackForce), invulnerabilitySeconds);
        }

        var actualDamage = Math.Max(0, healthBefore - player.Health);
        if (actualDamage > 0)
        {
            events?.Publish(new PlayerDamagedEvent(
                actualDamage,
                player.Health,
                player.MaxHealth,
                sourceEntityId));
        }

        return new ContactDamageResult(
            1,
            actualDamage,
            player.HealthComponent.IsDead,
            resolution.Outcome,
            resolution.GuardStaminaSpent,
            resolution.DamagePrevented)
        {
            Resolution = resolution
        };
    }

    private static bool CannotReceiveDamage(PlayerEntity player)
    {
        return player.HealthComponent.IsDead || player.HealthComponent.InvulnerabilityTimeRemaining > 0;
    }

    private static System.Numerics.Vector2 ResolveImpactDirection(PlayerEntity player, EnemyEntity enemy)
    {
        var direction = player.Body.Center - enemy.Body.Center;
        return direction == System.Numerics.Vector2.Zero
            ? System.Numerics.Vector2.UnitY * -1
            : direction;
    }

    private static void PublishStatusEffects(
        GameEventBus? events,
        int targetEntityId,
        StatusEffectSourceKind sourceKind,
        string sourceId,
        StatusEffectApplyResult result)
    {
        if (events is null)
        {
            return;
        }

        foreach (var effect in result.AppliedEffects)
        {
            events.Publish(new StatusEffectAppliedEvent(
                targetEntityId,
                effect.EffectId,
                sourceKind,
                sourceId,
                effect.Refreshed,
                effect.DurationSeconds));
        }
    }
}
