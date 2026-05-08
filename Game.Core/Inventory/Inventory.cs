using Game.Core.Items;

namespace Game.Core.Inventory;

public sealed class Inventory
{
    private readonly IItemDefinitionProvider _items;
    private readonly InventorySlot[] _slots;

    public Inventory(int slotCount, IItemDefinitionProvider items)
    {
        if (slotCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(slotCount), "Inventory must have at least one slot.");
        }

        _items = items;
        _slots = Enumerable.Range(0, slotCount).Select(_ => new InventorySlot()).ToArray();
    }

    public IReadOnlyList<InventorySlot> Slots => _slots;

    public bool AddItem(ItemStack stack)
    {
        if (stack.IsEmpty)
        {
            return true;
        }

        var remaining = stack.Count;
        remaining = MergeIntoExistingStacks(stack.ItemId, remaining);
        remaining = PlaceIntoEmptySlots(stack.ItemId, remaining);

        return remaining == 0;
    }

    public bool CanAddItem(ItemStack stack)
    {
        if (stack.IsEmpty)
        {
            return true;
        }

        var remaining = stack.Count;
        var maxStack = GetMaxStack(stack.ItemId);

        foreach (var slot in _slots)
        {
            if (remaining == 0)
            {
                break;
            }

            if (IsSameItem(slot.Stack, stack.ItemId))
            {
                remaining -= Math.Max(0, maxStack - slot.Stack.Count);
            }
        }

        foreach (var slot in _slots)
        {
            if (remaining == 0)
            {
                break;
            }

            if (slot.IsEmpty)
            {
                remaining -= maxStack;
            }
        }

        return remaining <= 0;
    }

    public Inventory Clone()
    {
        var clone = new Inventory(_slots.Length, _items);

        for (var index = 0; index < _slots.Length; index++)
        {
            clone._slots[index].SetStack(_slots[index].Stack);
        }

        return clone;
    }

    public bool RemoveItem(string itemId, int count)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(itemId);

        if (count <= 0)
        {
            return true;
        }

        if (CountItem(itemId) < count)
        {
            return false;
        }

        var remaining = count;
        for (var i = _slots.Length - 1; i >= 0 && remaining > 0; i--)
        {
            var slot = _slots[i];
            if (!IsSameItem(slot.Stack, itemId))
            {
                continue;
            }

            var remove = Math.Min(slot.Stack.Count, remaining);
            slot.SetStack(slot.Stack.WithCount(slot.Stack.Count - remove));
            remaining -= remove;
        }

        return true;
    }

    public void SwapSlots(int a, int b)
    {
        ValidateSlotIndex(a);
        ValidateSlotIndex(b);

        if (a == b)
        {
            return;
        }

        var left = _slots[a].Stack;
        _slots[a].SetStack(_slots[b].Stack);
        _slots[b].SetStack(left);
    }

    public ItemStack SplitStack(int slotIndex)
    {
        ValidateSlotIndex(slotIndex);

        var slot = _slots[slotIndex];
        if (slot.Stack.IsEmpty || slot.Stack.Count <= 1)
        {
            return ItemStack.Empty;
        }

        var splitCount = slot.Stack.Count / 2;
        slot.SetStack(slot.Stack.WithCount(slot.Stack.Count - splitCount));

        return new ItemStack(slot.Stack.ItemId, splitCount);
    }

    public bool MergeStack(int source, int target)
    {
        ValidateSlotIndex(source);
        ValidateSlotIndex(target);

        if (source == target)
        {
            return true;
        }

        var sourceSlot = _slots[source];
        var targetSlot = _slots[target];

        if (sourceSlot.IsEmpty)
        {
            return true;
        }

        if (!targetSlot.CanAccept(sourceSlot.Stack))
        {
            return false;
        }

        if (targetSlot.IsEmpty)
        {
            targetSlot.SetStack(sourceSlot.Clear());
            return true;
        }

        var maxStack = GetMaxStack(sourceSlot.Stack.ItemId);
        var space = maxStack - targetSlot.Stack.Count;
        if (space <= 0)
        {
            return false;
        }

        var moved = Math.Min(space, sourceSlot.Stack.Count);
        targetSlot.SetStack(targetSlot.Stack.WithCount(targetSlot.Stack.Count + moved));
        sourceSlot.SetStack(sourceSlot.Stack.WithCount(sourceSlot.Stack.Count - moved));

        return sourceSlot.IsEmpty;
    }

    public int CountItem(string itemId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(itemId);
        return _slots.Where(slot => IsSameItem(slot.Stack, itemId)).Sum(slot => slot.Stack.Count);
    }

    private int MergeIntoExistingStacks(string itemId, int count)
    {
        var remaining = count;
        var maxStack = GetMaxStack(itemId);

        foreach (var slot in _slots)
        {
            if (remaining == 0)
            {
                break;
            }

            if (!IsSameItem(slot.Stack, itemId) || slot.Stack.Count >= maxStack)
            {
                continue;
            }

            var space = maxStack - slot.Stack.Count;
            var moved = Math.Min(space, remaining);
            slot.SetStack(slot.Stack.WithCount(slot.Stack.Count + moved));
            remaining -= moved;
        }

        return remaining;
    }

    private int PlaceIntoEmptySlots(string itemId, int count)
    {
        var remaining = count;
        var maxStack = GetMaxStack(itemId);

        foreach (var slot in _slots)
        {
            if (remaining == 0)
            {
                break;
            }

            if (!slot.IsEmpty)
            {
                continue;
            }

            var moved = Math.Min(maxStack, remaining);
            slot.SetStack(new ItemStack(itemId, moved));
            remaining -= moved;
        }

        return remaining;
    }

    private int GetMaxStack(string itemId)
    {
        return _items.GetById(itemId).MaxStack;
    }

    private void ValidateSlotIndex(int index)
    {
        if (index < 0 || index >= _slots.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(index), $"Slot {index} is outside the inventory.");
        }
    }

    private static bool IsSameItem(ItemStack stack, string itemId)
    {
        return !stack.IsEmpty && string.Equals(stack.ItemId, itemId, StringComparison.OrdinalIgnoreCase);
    }
}
