using Game.Core.Entities;
using Game.Core.Events;
using Game.Core.Items;
using Game.Core.Loot;
using Game.Core.Physics;
using Game.Core.World;
using System.Numerics;

namespace Game.Core.Combat;

public sealed class MeleeAttackSystem
{
    private const int DefaultRangePixels = 38;
    private const int HitboxThicknessPixels = 30;

    private readonly LootRoller _lootRoller;
    private readonly TileCollisionResolver _collisionResolver;
    private float _cooldownRemaining;

    public MeleeAttackSystem(LootRoller lootRoller, TileCollisionResolver collisionResolver)
    {
        _lootRoller = lootRoller;
        _collisionResolver = collisionResolver;
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
        GameEventBus? events = null)
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
        var hitbox = CreateHitbox(player, targetWorldPosition, DefaultRangePixels);
        var hits = 0;
        var enemyDeaths = 0;
        var droppedItems = 0;
        var pendingDrops = new List<DroppedItemEntity>();

        foreach (var enemy in entities.Query(hitbox).OfType<EnemyEntity>().Where(enemy => enemy.IsActive).ToArray())
        {
            if (!enemy.Bounds.Intersects(hitbox))
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

        return new MeleeAttackResult(true, hits, enemyDeaths, droppedItems);
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

    private static RectI CreateHitbox(PlayerEntity player, Vector2 targetWorldPosition, int range)
    {
        var center = player.Body.Center;
        var direction = targetWorldPosition - center;
        if (direction == Vector2.Zero)
        {
            direction = Vector2.UnitX;
        }

        direction = Vector2.Normalize(direction);
        if (Math.Abs(direction.X) >= Math.Abs(direction.Y))
        {
            var x = direction.X >= 0 ? player.Bounds.Right : player.Bounds.Left - range;
            var y = (int)MathF.Floor(center.Y - HitboxThicknessPixels / 2f);
            return new RectI(x, y, range, HitboxThicknessPixels);
        }

        var verticalY = direction.Y >= 0 ? player.Bounds.Bottom : player.Bounds.Top - range;
        var verticalX = (int)MathF.Floor(center.X - HitboxThicknessPixels / 2f);
        return new RectI(verticalX, verticalY, HitboxThicknessPixels, range);
    }

    private static bool CanUseAsMelee(ItemDefinition item)
    {
        return item.Type is ItemType.WeaponMelee or ItemType.ToolAxe or ItemType.ToolPickaxe;
    }
}
