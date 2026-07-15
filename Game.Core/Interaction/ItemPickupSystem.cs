using Game.Core.Entities;
using Game.Core.Events;
using Game.Core.Inventory;
using Game.Core.World;

namespace Game.Core.Interaction;

public sealed class ItemPickupSystem
{
    private readonly List<Entity> _queryBuffer = new();
    private readonly HashSet<Entity> _querySeen = new();

    public int PickupItems(EntityManager entities, Inventory.Inventory inventory, RectI pickupArea, GameEventBus? events = null)
    {
        ArgumentNullException.ThrowIfNull(entities);
        ArgumentNullException.ThrowIfNull(inventory);

        return PickupItems(entities, pickupArea, events, inventory, playerInventory: null);
    }

    public int PickupItems(EntityManager entities, PlayerInventory inventory, RectI pickupArea, GameEventBus? events = null)
    {
        ArgumentNullException.ThrowIfNull(entities);
        ArgumentNullException.ThrowIfNull(inventory);

        return PickupItems(entities, pickupArea, events, inventory: null, playerInventory: inventory);
    }

    private int PickupItems(
        EntityManager entities,
        RectI pickupArea,
        GameEventBus? events,
        Inventory.Inventory? inventory,
        PlayerInventory? playerInventory)
    {
        var pickedUp = 0;
        entities.QueryInto(pickupArea, _queryBuffer, _querySeen);
        for (var index = 0; index < _queryBuffer.Count; index++)
        {
            if (_queryBuffer[index] is not DroppedItemEntity droppedItem)
            {
                continue;
            }

            if (!droppedItem.IsActive || !pickupArea.Intersects(droppedItem.Bounds))
            {
                continue;
            }

            var stack = droppedItem.Stack;
            var added = playerInventory is not null
                ? playerInventory.AddItem(stack)
                : inventory!.AddItem(stack);
            if (!added)
            {
                continue;
            }

            droppedItem.SetStack(ItemStack.Empty);
            events?.Publish(new ItemPickedUpEvent(droppedItem.Id, stack));
            pickedUp++;
        }

        return pickedUp;
    }
}
