using Game.Core.Entities;
using Game.Core.Events;
using Game.Core.Loot;
using Game.Core.Physics;

namespace Game.Core.Combat;

public sealed class EntityDeathLifecycle
{
    private readonly HashSet<int> _processedEntityIds = new();
    private readonly LootRoller _lootRoller;
    private readonly TileCollisionResolver _collisionResolver;

    public EntityDeathLifecycle(LootRoller lootRoller, TileCollisionResolver collisionResolver)
    {
        _lootRoller = lootRoller;
        _collisionResolver = collisionResolver;
    }

    public EntityDeathLifecycleResult ResolveDeath(
        EnemyEntity victim,
        EntityManager entities,
        LootTableRegistry lootTables,
        LootKillContext killContext,
        LootRollKey rollKey,
        GameEventBus? events = null)
    {
        ArgumentNullException.ThrowIfNull(victim);
        ArgumentNullException.ThrowIfNull(entities);
        ArgumentNullException.ThrowIfNull(lootTables);
        if (!victim.Health.IsDead || victim.Id <= 0 || !_processedEntityIds.Add(victim.Id))
        {
            return EntityDeathLifecycleResult.None;
        }

        victim.IsActive = false;
        if (string.IsNullOrWhiteSpace(victim.LootTableId) ||
            !lootTables.TryGetById(victim.LootTableId, out var table))
        {
            return new EntityDeathLifecycleResult(true, victim.Id, 0);
        }

        var droppedStacks = 0;
        foreach (var stack in _lootRoller.RollDeterministic(table, killContext, rollKey))
        {
            if (stack.IsEmpty)
            {
                continue;
            }

            entities.Add(new DroppedItemEntity(stack, victim.Body.Position, _collisionResolver));
            events?.Publish(new LootDroppedEvent(victim.Id, stack, victim.Body.Position));
            droppedStacks++;
        }

        return new EntityDeathLifecycleResult(true, victim.Id, droppedStacks);
    }
}
