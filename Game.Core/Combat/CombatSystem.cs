using Game.Core.Data;
using Game.Core.Entities;
using Game.Core.Events;
using Game.Core.Effects;
using Game.Core.Inventory;
using Game.Core.Loot;
using Game.Core.Physics;
using Game.Core.Projectiles;
using Game.Core.Tiles;
using Game.Core.World;
using System.Numerics;
using GameWorld = Game.Core.World.World;

namespace Game.Core.Combat;

public sealed class CombatSystem
{
    private readonly LootRoller _lootRoller;
    private readonly TileCollisionResolver _collisionResolver;
    private readonly StatusEffectApplier _statusEffects;
    private readonly CombatQueryWorkspace _compatibilityWorkspace = new();

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
        LootKillContext? lootContext = null,
        CombatQueryWorkspace? workspace = null)
    {
        return ResolveProjectileHits(
            entities,
            lootTables,
            statusEffects: null,
            world: null,
            tiles: null,
            events,
            lootContext,
            workspace ?? _compatibilityWorkspace);
    }

    public CombatResolutionResult ResolveProjectileHits(
        EntityManager entities,
        GameContentDatabase content,
        GameWorld world,
        GameEventBus? events = null,
        LootKillContext? lootContext = null,
        CombatQueryWorkspace? workspace = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        return ResolveProjectileHits(
            entities,
            content.LootTables,
            content.StatusEffects,
            world,
            content.Tiles,
            events,
            lootContext,
            workspace ?? _compatibilityWorkspace);
    }

    public CombatResolutionResult ResolveProjectileHits(
        EntityManager entities,
        GameContentDatabase content,
        GameEventBus? events = null,
        LootKillContext? lootContext = null,
        CombatQueryWorkspace? workspace = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        return ResolveProjectileHits(
            entities,
            content.LootTables,
            content.StatusEffects,
            null,
            null,
            events,
            lootContext,
            workspace ?? _compatibilityWorkspace);
    }

    private CombatResolutionResult ResolveProjectileHits(
        EntityManager entities,
        LootTableRegistry lootTables,
        StatusEffectRegistry? statusEffects,
        GameWorld? world,
        TileRegistry? tiles,
        GameEventBus? events,
        LootKillContext? lootContext,
        CombatQueryWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(entities);
        ArgumentNullException.ThrowIfNull(lootTables);

        var projectileHits = 0;
        var enemyDeaths = 0;
        var droppedItems = 0;
        var effectsApplied = 0;
        workspace.BeginResolution();
        var candidates = workspace.Candidates;
        var pendingDrops = workspace.PendingDrops;
        var projectileHitCandidates = workspace.ProjectileHits;

        for (var projectileIndex = 0; projectileIndex < entities.Entities.Count; projectileIndex++)
        {
            if (entities.Entities[projectileIndex] is not ProjectileEntity projectile)
            {
                continue;
            }

            var hasTileCollision = projectile.TryConsumeLatestTileCollisionResult(
                out var tileCollision,
                out var incomingTileVelocity);
            var canResolveHistoricalTileSweep = hasTileCollision &&
                                                tileCollision.TerminationReason ==
                                                ProjectileTerminationReason.TileCollision;
            var tileReached = hasTileCollision;
            if (projectile.IsActive || canResolveHistoricalTileSweep)
            {
                var sweptBounds = BuildProjectileSweptBounds(projectile);
                entities.QueryInto(sweptBounds, candidates);
                workspace.BeginProjectile();
                for (var enemyIndex = 0; enemyIndex < candidates.Count; enemyIndex++)
                {
                    if (candidates[enemyIndex] is not EnemyEntity { IsActive: true } enemy ||
                        !TryGetProjectileSweepTime(projectile, enemy.Bounds, out var timeOfImpact))
                    {
                        continue;
                    }

                    projectileHitCandidates.Add(new ProjectileHitCandidate(enemy, timeOfImpact));
                }

                projectileHitCandidates.Sort(ProjectileHitCandidateComparer.Instance);
                for (var enemyIndex = 0; enemyIndex < projectileHitCandidates.Count; enemyIndex++)
                {
                    var enemy = projectileHitCandidates[enemyIndex].Enemy;
                    if (!enemy.IsActive)
                    {
                        continue;
                    }

                    var entityCollision = new ProjectileEntityCollision(enemy.Id, enemy.Faction);
                    var collision = hasTileCollision
                        ? projectile.ResolveEntityCollisionBeforeTile(
                            entityCollision,
                            incomingTileVelocity)
                        : projectile.ResolveEntityCollision(entityCollision);
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
                    events?.Publish(new ProjectileHitEvent(
                        projectile.Id,
                        enemy.Id,
                        damageRequest.BaseDamage));
                    if (damageApplied && statusEffects is not null)
                    {
                        var effectResult = _statusEffects.ApplyDetailed(
                            enemy.StatusEffects,
                            statusEffects,
                            damageRequest.StatusEffects);
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
                        droppedItems += AddLootDrops(enemy, lootTables, pendingDrops, lootContext);
                    }

                    if (collision.Decision == ProjectileEntityCollisionDecision.HitAndStopped)
                    {
                        tileReached = false;
                        break;
                    }

                    if (!projectile.IsActive && !hasTileCollision)
                    {
                        break;
                    }
                }
            }

            if (tileReached && world is not null && tiles is not null)
            {
                ResolveProjectileTileCollision(projectile, tileCollision, world, tiles, events);
            }
        }

        for (var index = 0; index < pendingDrops.Count; index++)
        {
            entities.Add(pendingDrops[index]);
        }

        return new CombatResolutionResult(projectileHits, enemyDeaths, droppedItems, effectsApplied);
    }
    private static RectI BuildProjectileSweptBounds(ProjectileEntity projectile)
    {
        var current = projectile.Bounds;
        var previous = BuildProjectileBoundsAt(projectile, projectile.RuntimeState.PreviousPosition);
        var left = Math.Min(previous.Left, current.Left);
        var top = Math.Min(previous.Top, current.Top);
        var right = Math.Max(previous.Right, current.Right);
        var bottom = Math.Max(previous.Bottom, current.Bottom);
        return new RectI(
            left,
            top,
            SaturatingLength((long)right - left),
            SaturatingLength((long)bottom - top));
    }

    private static RectI BuildProjectileBoundsAt(ProjectileEntity projectile, Vector2 position)
    {
        var size = Math.Max(1, (int)MathF.Ceiling(projectile.Definition.CollisionRadius * 2));
        return new RectI(
            SaturatingFloor(position.X),
            SaturatingFloor(position.Y),
            size,
            size);
    }

    private static bool TryGetProjectileSweepTime(
        ProjectileEntity projectile,
        RectI target,
        out double timeOfImpact)
    {
        var current = projectile.Bounds;
        var previous = BuildProjectileBoundsAt(projectile, projectile.RuntimeState.PreviousPosition);
        if (previous.Intersects(target))
        {
            timeOfImpact = 0d;
            return true;
        }

        var start = projectile.RuntimeState.PreviousPosition;
        var movement = projectile.Position - start;
        if (movement == Vector2.Zero)
        {
            timeOfImpact = 0d;
            return current.Intersects(target);
        }

        var minimumX = target.Left - (double)previous.Width;
        var minimumY = target.Top - (double)previous.Height;
        var maximumX = target.Right;
        var maximumY = target.Bottom;
        var entry = 0d;
        var exit = 1d;
        var intersects = ClipSweepAxis(start.X, movement.X, minimumX, maximumX, ref entry, ref exit) &&
                         ClipSweepAxis(start.Y, movement.Y, minimumY, maximumY, ref entry, ref exit) &&
                         entry <= exit;
        timeOfImpact = intersects ? Math.Clamp(entry, 0d, 1d) : 0d;
        return intersects;
    }

    private static bool ClipSweepAxis(
        double origin,
        double movement,
        double minimum,
        double maximum,
        ref double entry,
        ref double exit)
    {
        if (Math.Abs(movement) <= double.Epsilon)
        {
            return origin >= minimum && origin <= maximum;
        }

        var first = (minimum - origin) / movement;
        var second = (maximum - origin) / movement;
        if (first > second)
        {
            (first, second) = (second, first);
        }

        entry = Math.Max(entry, first);
        exit = Math.Min(exit, second);
        return entry <= exit && exit >= 0d && entry <= 1d;
    }

    private static void ResolveProjectileTileCollision(
        ProjectileEntity projectile,
        in ProjectileTileCollisionResult collision,
        GameWorld world,
        TileRegistry tiles,
        GameEventBus? events)
    {
        if (collision.Decision == ProjectileTileCollisionDecision.IgnoredByDefinition ||
            !TryResolveMineableTile(world, collision, out var tileX, out var tileY, out var minedTileId))
        {
            return;
        }

        var tile = world.GetTile(tileX, tileY);
        var definition = tiles.GetByNumericId(minedTileId);
        if (projectile.Damage < definition.MiningPowerRequired)
        {
            return;
        }

        if (tile.IsAir)
        {
            world.SetWall(tileX, tileY, 0);
        }
        else
        {
            world.RemoveTile(tileX, tileY);
        }

        var drop = string.IsNullOrWhiteSpace(definition.DropItemId)
            ? ItemStack.Empty
            : new ItemStack(definition.DropItemId, 1);
        events?.Publish(new TileMinedEvent(new TilePos(tileX, tileY), minedTileId, drop));
    }

    private static bool TryResolveMineableTile(
        GameWorld world,
        in ProjectileTileCollisionResult collision,
        out int tileX,
        out int tileY,
        out ushort minedTileId)
    {
        tileX = 0;
        tileY = 0;
        minedTileId = 0;

        if (collision.TileX is { } explicitTileX &&
            collision.TileY is { } explicitTileY &&
            TryGetMineableTile(world, explicitTileX, explicitTileY, out tileX, out tileY, out minedTileId))
        {
            return true;
        }

        if (TryResolveMineableTile(world, collision.Position, out tileX, out tileY, out minedTileId))
        {
            return true;
        }

        if (collision.Velocity == Vector2.Zero)
        {
            return false;
        }

        var normalizedVelocity = Vector2.Normalize(collision.Velocity);
        var probeDistance = GameConstants.TileSize * 0.5f;
        for (var step = 1; step <= 3; step++)
        {
            var distance = step * probeDistance;
            if (TryResolveMineableTile(
                world,
                collision.Position + normalizedVelocity * -distance,
                out tileX,
                out tileY,
                out minedTileId))
            {
                return true;
            }

            if (TryResolveMineableTile(
                world,
                collision.Position + normalizedVelocity * distance,
                out tileX,
                out tileY,
                out minedTileId))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryResolveMineableTile(
        GameWorld world,
        Vector2 worldPosition,
        out int tileX,
        out int tileY,
        out ushort minedTileId)
    {
        tileX = 0;
        tileY = 0;
        minedTileId = 0;

        var center = CoordinateUtils.WorldToTile(worldPosition.X, worldPosition.Y);
        for (var offsetY = -1; offsetY <= 1; offsetY++)
        {
            for (var offsetX = -1; offsetX <= 1; offsetX++)
            {
                if (TryGetMineableTile(world, center.X + offsetX, center.Y + offsetY, out tileX, out tileY, out minedTileId))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool TryGetMineableTile(
        GameWorld world,
        int tileX,
        int tileY,
        out int resolvedTileX,
        out int resolvedTileY,
        out ushort minedTileId)
    {
        resolvedTileX = 0;
        resolvedTileY = 0;
        minedTileId = 0;

        if (!world.IsInBounds(tileX, tileY) ||
            !world.TryGetTile(tileX, tileY, out var tile) ||
            (tile.IsAir && tile.WallId == 0))
        {
            return false;
        }

        minedTileId = tile.IsAir ? tile.WallId : tile.TileId;
        if (minedTileId == 0)
        {
            return false;
        }

        resolvedTileX = tileX;
        resolvedTileY = tileY;
        return true;
    }

    private static int SaturatingFloor(float value)
    {
        var floored = Math.Floor(value);
        return floored <= int.MinValue
            ? int.MinValue
            : floored >= int.MaxValue
                ? int.MaxValue
                : (int)floored;
    }

    private static int SaturatingLength(long value)
    {
        return (int)Math.Clamp(value, 0, int.MaxValue);
    }

    private sealed class ProjectileHitCandidateComparer : IComparer<ProjectileHitCandidate>
    {
        public static ProjectileHitCandidateComparer Instance { get; } = new();

        public int Compare(ProjectileHitCandidate left, ProjectileHitCandidate right)
        {
            var time = left.TimeOfImpact.CompareTo(right.TimeOfImpact);
            return time != 0 ? time : left.Enemy.Id.CompareTo(right.Enemy.Id);
        }
    }

    private sealed class PlayerProjectileHitCandidateComparer : IComparer<PlayerProjectileHitCandidate>
    {
        public static PlayerProjectileHitCandidateComparer Instance { get; } = new();

        public int Compare(PlayerProjectileHitCandidate left, PlayerProjectileHitCandidate right)
        {
            var time = left.TimeOfImpact.CompareTo(right.TimeOfImpact);
            return time != 0 ? time : left.Projectile.Id.CompareTo(right.Projectile.Id);
        }
    }

    public ContactDamageResult ResolveEnemyContactDamage(
        PlayerEntity player,
        EntityManager entities,
        int damage = 10,
        float invulnerabilitySeconds = 0.65f,
        float knockbackForce = 180f,
        GameEventBus? events = null,
        CombatQueryWorkspace? workspace = null)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(entities);

        if (damage <= 0 || player.HealthComponent.IsDead)
        {
            return ContactDamageResult.None;
        }

        var candidates = (workspace ?? _compatibilityWorkspace).Candidates;
        entities.QueryInto(player.Bounds, candidates);
        for (var index = 0; index < candidates.Count; index++)
        {
            if (candidates[index] is not EnemyEntity { IsActive: true } enemy)
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
        GameEventBus? events = null,
        CombatQueryWorkspace? workspace = null)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(entities);
        ArgumentNullException.ThrowIfNull(guard);
        ArgumentNullException.ThrowIfNull(damageResolver);
        if (damage <= 0 || CannotReceiveDamage(player))
        {
            return ContactDamageResult.None;
        }

        var candidates = (workspace ?? _compatibilityWorkspace).Candidates;
        entities.QueryInto(player.Bounds, candidates);
        for (var index = 0; index < candidates.Count; index++)
        {
            if (candidates[index] is not EnemyEntity { IsActive: true } enemy ||
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
        GameEventBus? events = null,
        CombatQueryWorkspace? workspace = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        return ResolveEnemyContactDamageInternal(
            player,
            entities,
            content.StatusEffects,
            invulnerabilitySeconds,
            events,
            workspace ?? _compatibilityWorkspace);
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
        GameEventBus? events = null,
        CombatQueryWorkspace? workspace = null)
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

        var candidates = (workspace ?? _compatibilityWorkspace).Candidates;
        entities.QueryInto(player.Bounds, candidates);
        for (var index = 0; index < candidates.Count; index++)
        {
            if (candidates[index] is not EnemyEntity { IsActive: true } enemy ||
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
        GameEventBus? events = null,
        CombatQueryWorkspace? workspace = null)
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

        var queryWorkspace = workspace ?? _compatibilityWorkspace;
        var projectileResult = ResolveSweptProjectileDamageAgainstPlayer(
            player,
            entities,
            content,
            guard,
            damageResolver,
            invulnerabilitySeconds,
            mitigation,
            policy,
            events,
            queryWorkspace);
        if (projectileResult.ContactHits > 0)
        {
            return projectileResult;
        }

        var candidates = queryWorkspace.Candidates;
        entities.QueryInto(player.Bounds, candidates);
        for (var index = 0; index < candidates.Count; index++)
        {
            if (candidates[index] is not EnemyEntity { IsActive: true } enemy ||
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
        GameEventBus? events = null,
        CombatQueryWorkspace? workspace = null)
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

        return ResolveSweptProjectileDamageAgainstPlayer(
            player,
            entities,
            content,
            guard,
            damageResolver,
            invulnerabilitySeconds,
            mitigation,
            policy,
            events,
            workspace ?? _compatibilityWorkspace);
    }

    private ContactDamageResult ResolveSweptProjectileDamageAgainstPlayer(
        PlayerEntity player,
        EntityManager entities,
        GameContentDatabase content,
        GuardRuntimeState guard,
        CombatDamageResolver damageResolver,
        float invulnerabilitySeconds,
        DamageMitigationProfile? mitigation,
        CombatResolutionPolicy? policy,
        GameEventBus? events,
        CombatQueryWorkspace workspace)
    {
        workspace.BeginPlayerProjectileResolution();
        var candidates = workspace.PlayerProjectileHits;
        for (var index = 0; index < entities.Entities.Count; index++)
        {
            if (entities.Entities[index] is not ProjectileEntity { IsActive: true } projectile ||
                !CanProjectileTargetPlayer(projectile, player, policy) ||
                !TryGetProjectileSweepTime(projectile, player.Bounds, out var timeOfImpact))
            {
                continue;
            }

            candidates.Add(new PlayerProjectileHitCandidate(projectile, timeOfImpact));
        }

        candidates.Sort(PlayerProjectileHitCandidateComparer.Instance);
        for (var index = 0; index < candidates.Count; index++)
        {
            var result = ResolveProjectileCandidate(
                player,
                candidates[index].Projectile,
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

    private static bool CanProjectileTargetPlayer(
        ProjectileEntity projectile,
        PlayerEntity player,
        CombatResolutionPolicy? policy)
    {
        if (projectile.OwnerEntityId == player.Id)
        {
            return false;
        }

        if (projectile.OwnerFaction != EntityFaction.Friendly)
        {
            return true;
        }

        return projectile.Definition.FriendlyFire &&
               policy is { FriendlyFireEnabled: true };
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
        GameEventBus? events,
        CombatQueryWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(entities);
        ArgumentNullException.ThrowIfNull(statusEffects);

        if (player.HealthComponent.IsDead)
        {
            return ContactDamageResult.None;
        }

        var candidates = workspace.Candidates;
        entities.QueryInto(player.Bounds, candidates);
        for (var index = 0; index < candidates.Count; index++)
        {
            if (candidates[index] is not EnemyEntity { IsActive: true } enemy)
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
        PublishCombatEvents(events, resolution.Events);
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

    private static void PublishCombatEvents(GameEventBus? events, IReadOnlyList<ICombatEvent> combatEvents)
    {
        if (events is null)
        {
            return;
        }

        for (var index = 0; index < combatEvents.Count; index++)
        {
            switch (combatEvents[index])
            {
                case AttackStartedCombatEvent gameEvent:
                    events.Publish(gameEvent);
                    break;
                case AttackPhaseChangedCombatEvent gameEvent:
                    events.Publish(gameEvent);
                    break;
                case AttackComboQueuedCombatEvent gameEvent:
                    events.Publish(gameEvent);
                    break;
                case AttackCompletedCombatEvent gameEvent:
                    events.Publish(gameEvent);
                    break;
                case CombatHitResolvedEvent gameEvent:
                    events.Publish(gameEvent);
                    break;
                case CombatParriedEvent gameEvent:
                    events.Publish(gameEvent);
                    break;
                case CombatBlockedEvent gameEvent:
                    events.Publish(gameEvent);
                    break;
                case GuardBrokenCombatEvent gameEvent:
                    events.Publish(gameEvent);
                    break;
            }
        }
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
