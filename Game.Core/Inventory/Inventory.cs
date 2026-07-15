using Game.Core.Items;

namespace Game.Core.Inventory;

public sealed class Inventory
{
    private readonly IItemDefinitionProvider _items;
    private readonly InventorySlot[] _slots;
    private long _version;

    public Inventory(int slotCount, IItemDefinitionProvider items)
    {
        if (slotCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(slotCount), "Inventory must have at least one slot.");
        }

        ArgumentNullException.ThrowIfNull(items);
        _items = items;
        _slots = Enumerable.Range(0, slotCount).Select(_ => new InventorySlot(OnSlotChanged)).ToArray();
    }

    public IReadOnlyList<InventorySlot> Slots => _slots;

    public long Version => _version;

    internal IItemDefinitionProvider ItemDefinitions => _items;

    private void OnSlotChanged()
    {
        _version = _version == long.MaxValue ? 1 : _version + 1;
    }

    // Compatibility API: preserves the historical partial-add behavior.
    public bool AddItem(ItemStack stack)
    {
        if (stack.IsEmpty)
        {
            return true;
        }

        return AddCore(stack.ItemId, stack.Count).Remaining == 0;
    }

    public InventoryTransactionResult AddTransaction(
        ItemStack stack,
        InventoryTransactionOptions options = default)
    {
        if (stack.Count <= 0)
        {
            return InventoryTransactionResult.NoChange(stack.ItemId);
        }

        if (string.IsNullOrWhiteSpace(stack.ItemId))
        {
            return InventoryTransactionResult.Rejected(
                InventoryTransactionStatus.InvalidRequest,
                stack.ItemId,
                stack.Count,
                "An item id is required.");
        }

        if (!_items.TryGetById(stack.ItemId, out _))
        {
            return InventoryTransactionResult.Rejected(
                InventoryTransactionStatus.UnknownItem,
                stack.ItemId,
                stack.Count,
                $"Unknown item '{stack.ItemId}'.");
        }

        var capacity = GetAvailableCapacity(stack.ItemId);
        if (capacity < stack.Count && !options.AllowPartial)
        {
            return InventoryTransactionResult.Rejected(
                InventoryTransactionStatus.NoSpace,
                stack.ItemId,
                stack.Count,
                $"Only {capacity} item(s) fit.");
        }

        var toMove = Math.Min(capacity, stack.Count);
        if (toMove <= 0)
        {
            return InventoryTransactionResult.Rejected(
                InventoryTransactionStatus.NoSpace,
                stack.ItemId,
                stack.Count,
                "The inventory has no available space.");
        }

        var applied = AddCore(stack.ItemId, toMove);
        var moved = toMove - applied.Remaining;
        return moved == stack.Count
            ? InventoryTransactionResult.Complete(stack.ItemId, stack.Count, moved)
            : InventoryTransactionResult.Partial(stack.ItemId, stack.Count, moved, $"{stack.Count - moved} item(s) did not fit.");
    }

    public bool CanAddItem(ItemStack stack)
    {
        return stack.IsEmpty || GetAvailableCapacity(stack.ItemId) >= stack.Count;
    }

    public int GetAvailableCapacity(string itemId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(itemId);

        var maxStack = GetMaxStack(itemId);
        var capacity = 0L;
        foreach (var slot in _slots)
        {
            if (slot.IsEmpty)
            {
                capacity += maxStack;
            }
            else if (IsSameItem(slot.Stack, itemId))
            {
                capacity += Math.Max(0, maxStack - slot.Stack.Count);
            }
        }

        return (int)Math.Min(int.MaxValue, capacity);
    }

    public Inventory Clone()
    {
        var clone = new Inventory(_slots.Length, _items);

        for (var index = 0; index < _slots.Length; index++)
        {
            clone._slots[index].SetState(_slots[index].GetState());
        }

        return clone;
    }

    public bool RemoveItem(string itemId, int count)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(itemId);
        return RemoveTransaction(itemId, count).Completed;
    }

    public InventoryTransactionResult RemoveTransaction(
        string itemId,
        int count,
        InventoryTransactionOptions options = default)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return InventoryTransactionResult.Rejected(
                InventoryTransactionStatus.InvalidRequest,
                itemId ?? string.Empty,
                count,
                "An item id is required.");
        }

        if (count <= 0)
        {
            return InventoryTransactionResult.NoChange(itemId);
        }

        var available = CountAvailableItem(itemId, options.IncludeFavorites);
        if (available < count && !options.AllowPartial)
        {
            var protectedItems = CountItem(itemId) - available;
            var status = protectedItems > 0 && available + protectedItems >= count
                ? InventoryTransactionStatus.Protected
                : InventoryTransactionStatus.InsufficientItems;
            var message = status == InventoryTransactionStatus.Protected
                ? "Favorite items are protected from removal."
                : $"Only {available} removable item(s) are available.";
            return InventoryTransactionResult.Rejected(status, itemId, count, message);
        }

        var toMove = Math.Min(available, count);
        if (toMove <= 0)
        {
            var status = CountItem(itemId) > 0
                ? InventoryTransactionStatus.Protected
                : InventoryTransactionStatus.InsufficientItems;
            return InventoryTransactionResult.Rejected(status, itemId, count, "No removable items are available.");
        }

        var moved = RemovePreferred(itemId, toMove, options.IncludeFavorites);
        return moved == count
            ? InventoryTransactionResult.Complete(itemId, count, moved)
            : InventoryTransactionResult.Partial(itemId, count, moved, $"{count - moved} item(s) remain.");
    }

    public InventoryTransactionResult TransferTo(
        Inventory target,
        string itemId,
        int count,
        InventoryTransactionOptions options = default)
    {
        ArgumentNullException.ThrowIfNull(target);

        if (ReferenceEquals(this, target) || count <= 0)
        {
            return InventoryTransactionResult.NoChange(itemId);
        }

        if (string.IsNullOrWhiteSpace(itemId))
        {
            return InventoryTransactionResult.Rejected(
                InventoryTransactionStatus.InvalidRequest,
                itemId ?? string.Empty,
                count,
                "An item id is required.");
        }

        var available = CountAvailableItem(itemId, options.IncludeFavorites);
        var capacity = target.GetAvailableCapacity(itemId);
        if (!options.AllowPartial)
        {
            if (available < count)
            {
                var status = CountItem(itemId) >= count
                    ? InventoryTransactionStatus.Protected
                    : InventoryTransactionStatus.InsufficientItems;
                return InventoryTransactionResult.Rejected(status, itemId, count, "The source cannot provide the requested amount.");
            }

            if (capacity < count)
            {
                return InventoryTransactionResult.Rejected(
                    InventoryTransactionStatus.NoSpace,
                    itemId,
                    count,
                    $"The target only has space for {capacity} item(s).");
            }
        }

        var toMove = Math.Min(count, Math.Min(available, capacity));
        if (toMove <= 0)
        {
            var status = available <= 0
                ? CountItem(itemId) > 0
                    ? InventoryTransactionStatus.Protected
                    : InventoryTransactionStatus.InsufficientItems
                : InventoryTransactionStatus.NoSpace;
            return InventoryTransactionResult.Rejected(status, itemId, count, "Nothing could be transferred.");
        }

        var regularRequested = Math.Min(toMove, CountAvailableItem(itemId));
        var favoriteRequested = toMove - regularRequested;
        var removedRegular = RemoveCore(itemId, regularRequested, includeFavorites: false);
        var removedFavorite = favoriteRequested > 0
            ? RemoveFavoriteCore(itemId, favoriteRequested)
            : 0;
        var regularAdded = target.AddCore(itemId, removedRegular);
        var favoriteAdded = target.AddCore(itemId, removedFavorite, favorite: true);
        var moved = removedRegular + removedFavorite - regularAdded.Remaining - favoriteAdded.Remaining;
        if (regularAdded.Remaining > 0)
        {
            AddCore(itemId, regularAdded.Remaining);
        }

        if (favoriteAdded.Remaining > 0)
        {
            AddCore(itemId, favoriteAdded.Remaining, favorite: true);
        }

        return moved == count
            ? InventoryTransactionResult.Complete(itemId, count, moved)
            : InventoryTransactionResult.Partial(itemId, count, moved, $"{count - moved} item(s) remain in the source.");
    }

    public InventoryTransactionResult TransferSlotTo(
        Inventory target,
        int sourceSlot,
        InventoryTransactionOptions options = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        ValidateSlotIndex(sourceSlot);

        var slot = _slots[sourceSlot];
        if (slot.IsEmpty || ReferenceEquals(this, target))
        {
            return InventoryTransactionResult.NoChange(slot.Stack.ItemId);
        }

        var stack = slot.Stack;
        if (slot.IsFavorite && !options.IncludeFavorites)
        {
            return InventoryTransactionResult.Rejected(
                InventoryTransactionStatus.Protected,
                stack.ItemId,
                stack.Count,
                "Favorite stacks must be unfavorited before moving them.");
        }

        var capacity = target.GetAvailableCapacity(stack.ItemId);
        if (capacity < stack.Count && !options.AllowPartial)
        {
            return InventoryTransactionResult.Rejected(
                InventoryTransactionStatus.NoSpace,
                stack.ItemId,
                stack.Count,
                $"The target only has space for {capacity} item(s).");
        }

        var toMove = Math.Min(stack.Count, capacity);
        if (toMove <= 0)
        {
            return InventoryTransactionResult.Rejected(
                InventoryTransactionStatus.NoSpace,
                stack.ItemId,
                stack.Count,
                "The target has no available space.");
        }

        var added = target.AddCore(stack.ItemId, toMove, slot.IsFavorite);
        var moved = toMove - added.Remaining;
        slot.SetStack(stack.WithCount(stack.Count - moved));

        return moved == stack.Count
            ? InventoryTransactionResult.Complete(stack.ItemId, stack.Count, moved)
            : InventoryTransactionResult.Partial(stack.ItemId, stack.Count, moved, $"{stack.Count - moved} item(s) remain in the slot.");
    }

    public InventoryTransactionResult TrashSlot(
        int slotIndex,
        InventoryTransactionOptions options = default)
    {
        ValidateSlotIndex(slotIndex);
        var slot = _slots[slotIndex];
        if (slot.IsEmpty)
        {
            return InventoryTransactionResult.NoChange();
        }

        var stack = slot.Stack;
        var item = _items.GetById(stack.ItemId);
        if (slot.IsFavorite && !options.IncludeFavorites)
        {
            return InventoryTransactionResult.Rejected(
                InventoryTransactionStatus.Protected,
                stack.ItemId,
                stack.Count,
                "Favorite stacks cannot be trashed.");
        }

        if (!item.CanTrash && !options.IgnoreTrashProtection)
        {
            return InventoryTransactionResult.Rejected(
                InventoryTransactionStatus.Protected,
                stack.ItemId,
                stack.Count,
                $"{item.DisplayName} is protected from trashing.");
        }

        slot.Clear();
        return InventoryTransactionResult.Complete(stack.ItemId, stack.Count, stack.Count);
    }

    public bool SetFavorite(int slotIndex, bool favorite)
    {
        ValidateSlotIndex(slotIndex);
        var slot = _slots[slotIndex];
        if (slot.IsEmpty)
        {
            return false;
        }

        if (favorite && !_items.GetById(slot.Stack.ItemId).CanFavorite)
        {
            return false;
        }

        return slot.SetFavorite(favorite);
    }

    public void SwapSlots(int a, int b)
    {
        ValidateSlotIndex(a);
        ValidateSlotIndex(b);

        if (a == b)
        {
            return;
        }

        var left = _slots[a].GetState();
        _slots[a].SetState(_slots[b].GetState());
        _slots[b].SetState(left);
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
            targetSlot.SetState(sourceSlot.GetState());
            sourceSlot.Clear();
            return true;
        }

        var maxStack = GetMaxStack(sourceSlot.Stack.ItemId);
        var space = maxStack - targetSlot.Stack.Count;
        if (space <= 0)
        {
            return false;
        }

        var moved = Math.Min(space, sourceSlot.Stack.Count);
        targetSlot.SetState(new InventorySlotState(
            targetSlot.Stack.WithCount(targetSlot.Stack.Count + moved),
            targetSlot.IsFavorite || sourceSlot.IsFavorite));
        sourceSlot.SetStack(sourceSlot.Stack.WithCount(sourceSlot.Stack.Count - moved));
        return sourceSlot.IsEmpty;
    }

    public int CountItem(string itemId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(itemId);
        return _slots.Where(slot => IsSameItem(slot.Stack, itemId)).Sum(slot => slot.Stack.Count);
    }

    public int CountAvailableItem(string itemId, bool includeFavorites = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(itemId);
        return _slots
            .Where(slot => (includeFavorites || !slot.IsFavorite) && IsSameItem(slot.Stack, itemId))
            .Sum(slot => slot.Stack.Count);
    }

    public InventoryOrganizationResult CompactStacks()
    {
        return InventoryOrganizer.Compact(this, _items);
    }

    public InventoryOrganizationResult Sort(InventorySortMode mode)
    {
        return InventoryOrganizer.Sort(this, _items, mode);
    }

    private InventoryTransactionResult AddCore(string itemId, int count, bool favorite = false)
    {
        var remaining = MergeIntoExistingStacks(itemId, count, favorite);
        remaining = PlaceIntoEmptySlots(itemId, remaining, favorite);
        var moved = count - remaining;
        return remaining == 0
            ? InventoryTransactionResult.Complete(itemId, count, moved)
            : InventoryTransactionResult.Partial(itemId, count, moved);
    }

    private int RemoveCore(string itemId, int count, bool includeFavorites)
    {
        var remaining = count;
        for (var index = _slots.Length - 1; index >= 0 && remaining > 0; index--)
        {
            var slot = _slots[index];
            if ((!includeFavorites && slot.IsFavorite) || !IsSameItem(slot.Stack, itemId))
            {
                continue;
            }

            var remove = Math.Min(slot.Stack.Count, remaining);
            slot.SetStack(slot.Stack.WithCount(slot.Stack.Count - remove));
            remaining -= remove;
        }

        return count - remaining;
    }

    private int RemovePreferred(string itemId, int count, bool includeFavorites)
    {
        var removed = RemoveCore(itemId, count, includeFavorites: false);
        return includeFavorites && removed < count
            ? removed + RemoveFavoriteCore(itemId, count - removed)
            : removed;
    }

    private int RemoveFavoriteCore(string itemId, int count)
    {
        var remaining = count;
        for (var index = _slots.Length - 1; index >= 0 && remaining > 0; index--)
        {
            var slot = _slots[index];
            if (!slot.IsFavorite || !IsSameItem(slot.Stack, itemId))
            {
                continue;
            }

            var remove = Math.Min(slot.Stack.Count, remaining);
            slot.SetStack(slot.Stack.WithCount(slot.Stack.Count - remove));
            remaining -= remove;
        }

        return count - remaining;
    }

    private int MergeIntoExistingStacks(string itemId, int count, bool favorite = false)
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

            var moved = Math.Min(maxStack - slot.Stack.Count, remaining);
            slot.SetState(new InventorySlotState(
                slot.Stack.WithCount(slot.Stack.Count + moved),
                slot.IsFavorite || favorite));
            remaining -= moved;
        }

        return remaining;
    }

    private int PlaceIntoEmptySlots(string itemId, int count, bool favorite = false)
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
            slot.SetState(new InventorySlotState(new ItemStack(itemId, moved), favorite));
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
