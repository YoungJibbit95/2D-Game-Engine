using Game.Core.Items;

namespace Game.Core.Inventory;

public sealed class InventoryInteractionService
{
    private readonly IItemDefinitionProvider _items;

    public InventoryInteractionService(IItemDefinitionProvider items)
    {
        _items = items;
    }

    public InventoryInteractionResult Click(InventorySlot slot, CursorItemState cursor, InventoryClickButton button)
    {
        ArgumentNullException.ThrowIfNull(slot);
        ArgumentNullException.ThrowIfNull(cursor);

        return button switch
        {
            InventoryClickButton.Left => LeftClick(slot, cursor),
            InventoryClickButton.Right => RightClick(slot, cursor),
            _ => InventoryInteractionResult.NoChange
        };
    }

    private InventoryInteractionResult LeftClick(InventorySlot slot, CursorItemState cursor)
    {
        if (!cursor.IsHoldingItem)
        {
            if (slot.IsEmpty)
            {
                return InventoryInteractionResult.NoChange;
            }

            cursor.Set(slot.Clear());
            return InventoryInteractionResult.Success;
        }

        if (slot.IsEmpty)
        {
            slot.SetStack(cursor.Clear());
            return InventoryInteractionResult.Success;
        }

        if (!string.Equals(slot.Stack.ItemId, cursor.HeldStack.ItemId, StringComparison.OrdinalIgnoreCase))
        {
            var slotStack = slot.Clear();
            slot.SetStack(cursor.Clear());
            cursor.Set(slotStack);
            return InventoryInteractionResult.Success;
        }

        var maxStack = _items.GetById(slot.Stack.ItemId).MaxStack;
        var space = maxStack - slot.Stack.Count;
        if (space <= 0)
        {
            return InventoryInteractionResult.NoChange;
        }

        var moved = Math.Min(space, cursor.HeldStack.Count);
        slot.SetStack(slot.Stack.WithCount(slot.Stack.Count + moved));
        cursor.Set(cursor.HeldStack.WithCount(cursor.HeldStack.Count - moved));
        return InventoryInteractionResult.Success;
    }

    private InventoryInteractionResult RightClick(InventorySlot slot, CursorItemState cursor)
    {
        if (!cursor.IsHoldingItem)
        {
            if (slot.IsEmpty)
            {
                return InventoryInteractionResult.NoChange;
            }

            var take = (slot.Stack.Count + 1) / 2;
            cursor.Set(new ItemStack(slot.Stack.ItemId, take));
            slot.SetStack(slot.Stack.WithCount(slot.Stack.Count - take));
            return InventoryInteractionResult.Success;
        }

        if (slot.IsEmpty)
        {
            slot.SetStack(new ItemStack(cursor.HeldStack.ItemId, 1));
            cursor.Set(cursor.HeldStack.WithCount(cursor.HeldStack.Count - 1));
            return InventoryInteractionResult.Success;
        }

        if (!string.Equals(slot.Stack.ItemId, cursor.HeldStack.ItemId, StringComparison.OrdinalIgnoreCase))
        {
            return InventoryInteractionResult.Failed("Cannot place one item on a different stack.");
        }

        var maxStack = _items.GetById(slot.Stack.ItemId).MaxStack;
        if (slot.Stack.Count >= maxStack)
        {
            return InventoryInteractionResult.NoChange;
        }

        slot.SetStack(slot.Stack.WithCount(slot.Stack.Count + 1));
        cursor.Set(cursor.HeldStack.WithCount(cursor.HeldStack.Count - 1));
        return InventoryInteractionResult.Success;
    }
}
