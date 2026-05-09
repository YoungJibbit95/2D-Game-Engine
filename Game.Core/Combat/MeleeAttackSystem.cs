using Game.Core.Entities;
using Game.Core.Events;
using Game.Core.Effects;
using Game.Core.Items;
using Game.Core.Loot;
using Game.Core.Physics;
using Game.Core.World;
using Game.Core.World.Queries;
using System.Numerics;
using GameWorld = Game.Core.World.World;

namespace Game.Core.Combat;

public sealed class MeleeAttackSystem
{
    private const int DefaultRangePixels = 38;
    private const int HitboxThicknessPixels = 30;

    private readonly LootRoller _lootRoller;
    private readonly TileCollisionResolver _collisionResolver;
    private readonly WorldQueryService _queries = new();
    private readonly AreaQueryService _areaQueries = new();
    private readonly AttackShapeResolver _attackShapes = new();
    private readonly StatusEffectApplier _statusEffects;
    private float _cooldownRemaining;

    public MeleeAttackSystem(LootRoller lootRoller, TileCollisionResolver collisionResolver, StatusEffectApplier? statusEffects = null)
    {
        _lootRoller = lootRoller;
        _collisionResolver = collisionResolver;
        _statusEffects = statusEffects ?? new StatusEffectApplier();
    }

    public bool CanAttack => _cooldownRemaining <= 0;

    public void Update(float deltaSeconds)
    {
        if (deltaSeconds <= 0)
        {
            return;
        }

        _cooldownRemaining = Math.Max(0, _cooldownRemaining - deltaSeconds);
    }

    public MeleeAttackResult Attack(
        PlayerEntity player,
        EntityManager entities,
        ItemDefinition item,
        LootTableRegistry lootTables,
        Vector2 targetWorldPosition,
        GameEventBus? events = null,
        StatusEffectRegistry? statusEffectRegistry = null)
    {
        return AttackInternal(player, entities, item, lootTables, targetWorldPosition, world: null, events, statusEffectRegistry);
    }

    public MeleeAttackResult Attack(
        PlayerEntity player,
        EntityManager entities,
        ItemDefinition item,
        LootTableRegistry lootTables,
        Vector2 targetWorldPosition,
        GameWorld world,
        GameEventBus? events = null,
        StatusEffectRegistry? statusEffectRegistry = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        return AttackInternal(player, entities, item, lootTables, targetWorldPosition, world, events, statusEffectRegistry);
    }

    private MeleeAttackResult AttackInternal(
        PlayerEntity player,
        EntityManager entities,
        ItemDefinition item,
        LootTableRegistry lootTables,
        Vector2 targetWorldPosition,
        GameWorld? world,
        GameEventBus? events,
        StatusEffectRegistry? statusEffectRegistry)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(entities);
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(lootTables);

        if (!CanAttack || !CanUseAsMelee(item) || item.Damage <= 0 || player.HealthComponent.IsDead)
        {
            return MeleeAttackResult.None;
        }

        _cooldownRemaining = Math.Max(0.08f, item.UseTime);
        var shape = _attackShapes.Resolve(player, targetWorldPosition, item.AttackShape ?? new AttackShapeDefinition
        {
            Kind = AttackShapeKind.Rectangle,
            Range = DefaultRangePixels,
            Height = HitboxThicknessPixels
        });
        var hits = 0;
        var enemyDeaths = 0;
        var droppedItems = 0;
        var effectsApplied = 0;
        var pendingDrops = new List<DroppedItemEntity>();

        foreach (var enemy in _areaQueries.QueryEntities(entities, shape).OfType<EnemyEntity>().Where(enemy => enemy.IsActive).ToArray())
        {
            if (world is not null && !_queries.HasLineOfSight(world, player.Body.Center, enemy.Body.Center))
            {
                continue;
            }

            hits++;
            var direction = enemy.Body.Center - player.Body.Center;
            if (direction == Vector2.Zero)
            {
                direction = Vector2.UnitX;
            }

            var damage = new DamageInfo(item.Damage, DamageType.Melee, player.Id, direction, item.Knockback);
            var applied = enemy.ApplyDamage(damage);
            events?.Publish(new MeleeHitEvent(player.Id, enemy.Id, item.Damage));
            if (applied && statusEffectRegistry is not null && item.OnHitEffects.Count > 0)
            {
                effectsApplied += _statusEffects.Apply(enemy.StatusEffects, statusEffectRegistry, item.OnHitEffects);
            }

            if (!applied || !enemy.Health.IsDead)
            {
                continue;
            }

            enemy.IsActive = false;
            enemyDeaths++;
            events?.Publish(new EntityDiedEvent(enemy.Id, enemy.DefinitionId));
            droppedItems += AddLootDrops(enemy, lootTables, pendingDrops);
        }

        foreach (var drop in pendingDrops)
        {
            entities.Add(drop);
        }

        return new MeleeAttackResult(true, hits, enemyDeaths, droppedItems, effectsApplied);
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

    private static bool CanUseAsMelee(ItemDefinition item)
    {
        var action = ItemActionResolver.GetPrimaryAction(item);
        return action.Kind == ItemActionKind.Melee || item.Type is ItemType.WeaponMelee or ItemType.ToolAxe;
    }
}
