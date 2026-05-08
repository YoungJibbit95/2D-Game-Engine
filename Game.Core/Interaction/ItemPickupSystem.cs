using Game.Core.Entities;
using Game.Core.Events;
using Game.Core.Inventory;
using Game.Core.World;

namespace Game.Core.Interaction;

public sealed class ItemPickupSystem
{
    public int PickupItems(EntityManager entities, Inventory.Inventory inventory, RectI pickupArea, GameEventBus? events = null)
    {
        ArgumentNullException.ThrowIfNull(entities);
        ArgumentNullException.ThrowIfNull(inventory);

        var pickedUp = 0;
        foreach (var droppedItem in entities.Query(pickupArea).OfType<DroppedItemEntity>().ToArray())
        {
            if (!droppedItem.IsActive || !pickupArea.Intersects(droppedItem.Bounds))
            {
                continue;
            }

            var stack = droppedItem.Stack;
            if (!inventory.AddItem(stack))
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
