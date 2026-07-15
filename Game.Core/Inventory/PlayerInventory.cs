using Game.Core.Items;

namespace Game.Core.Inventory;

public sealed class PlayerInventory
{
    public const int HotbarSlotCount = 10;
    public const int MainSlotCount = 40;
    private readonly IItemDefinitionProvider _items;

    public PlayerInventory(IItemDefinitionProvider items)
        : this(new Inventory(HotbarSlotCount, items), new Inventory(MainSlotCount, items), items)
    {
    }

    public PlayerInventory(Inventory hotbar, Inventory main, IItemDefinitionProvider items)
    {
        ArgumentNullException.ThrowIfNull(hotbar);
        ArgumentNullException.ThrowIfNull(main);
        ArgumentNullException.ThrowIfNull(items);

        if (hotbar.Slots.Count != HotbarSlotCount)
        {
            throw new ArgumentException($"Hotbar must have {HotbarSlotCount} slots.", nameof(hotbar));
        }

        Hotbar = hotbar;
        Main = main;
        _items = items;
    }

    public Inventory Hotbar { get; }

    public Inventory Main { get; }

    public IItemDefinitionProvider ItemDefinitions => _items;

    public int SelectedHotbarSlot { get; private set; }

    public ItemStack SelectedStack => Hotbar.Slots[SelectedHotbarSlot].Stack;

    // Compatibility API: PlayerInventory has historically been all-or-nothing.
    public bool AddItem(ItemStack stack)
    {
        if (stack.IsEmpty)
        {
            return true;
        }

        if (!CanAddItem(stack))
        {
            return false;
        }

        var remaining = MergeIntoExistingStacks(Hotbar, stack.ItemId, stack.Count);
        remaining = MergeIntoExistingStacks(Main, stack.ItemId, remaining);
        remaining = PlaceIntoEmptySlots(Hotbar, stack.ItemId, remaining);
        remaining = PlaceIntoEmptySlots(Main, stack.ItemId, remaining);
        return remaining == 0;
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

        var capacity = Hotbar.GetAvailableCapacity(stack.ItemId) + Main.GetAvailableCapacity(stack.ItemId);
        if (capacity < stack.Count && !options.AllowPartial)
        {
            return InventoryTransactionResult.Rejected(
                InventoryTransactionStatus.NoSpace,
                stack.ItemId,
                stack.Count,
                $"Only {capacity} item(s) fit.");
        }

        var requestedMove = Math.Min(stack.Count, capacity);
        if (requestedMove <= 0)
        {
            return InventoryTransactionResult.Rejected(
                InventoryTransactionStatus.NoSpace,
                stack.ItemId,
                stack.Count,
                "The inventory has no available space.");
        }

        var remaining = MergeIntoExistingStacks(Hotbar, stack.ItemId, requestedMove);
        remaining = MergeIntoExistingStacks(Main, stack.ItemId, remaining);
        remaining = PlaceIntoEmptySlots(Hotbar, stack.ItemId, remaining);
        remaining = PlaceIntoEmptySlots(Main, stack.ItemId, remaining);
        var moved = requestedMove - remaining;

        return moved == stack.Count
            ? InventoryTransactionResult.Complete(stack.ItemId, stack.Count, moved)
            : InventoryTransactionResult.Partial(stack.ItemId, stack.Count, moved, $"{stack.Count - moved} item(s) did not fit.");
    }

    public InventoryTransactionResult AddSlotStateTransaction(
        InventorySlotState state,
        InventoryTransactionOptions options = default)
    {
        var stack = state.Stack;
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

        if (!_items.TryGetById(stack.ItemId, out var definition))
        {
            return InventoryTransactionResult.Rejected(
                InventoryTransactionStatus.UnknownItem,
                stack.ItemId,
                stack.Count,
                $"Unknown item '{stack.ItemId}'.");
        }

        var capacity = GetStateCapacity(Hotbar, stack.ItemId, definition.MaxStack, state.IsFavorite) +
                       GetStateCapacity(Main, stack.ItemId, definition.MaxStack, state.IsFavorite);
        if (capacity < stack.Count && !options.AllowPartial)
        {
            return InventoryTransactionResult.Rejected(
                InventoryTransactionStatus.NoSpace,
                stack.ItemId,
                stack.Count,
                $"Only {capacity} item(s) fit without changing favorite state.");
        }

        var requestedMove = Math.Min(stack.Count, capacity);
        var remaining = AddStateCore(Hotbar, stack.ItemId, requestedMove, definition.MaxStack, state.IsFavorite);
        remaining = AddStateCore(Main, stack.ItemId, remaining, definition.MaxStack, state.IsFavorite);
        var moved = requestedMove - remaining;
        return moved == stack.Count
            ? InventoryTransactionResult.Complete(stack.ItemId, stack.Count, moved)
            : InventoryTransactionResult.Partial(stack.ItemId, stack.Count, moved, $"{stack.Count - moved} item(s) did not fit.");
    }

    public bool CanAddItem(ItemStack stack)
    {
        return stack.IsEmpty ||
               Hotbar.GetAvailableCapacity(stack.ItemId) + Main.GetAvailableCapacity(stack.ItemId) >= stack.Count;
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

        var hotbarAvailable = Hotbar.CountAvailableItem(itemId, options.IncludeFavorites);
        var mainAvailable = Main.CountAvailableItem(itemId, options.IncludeFavorites);
        var available = hotbarAvailable + mainAvailable;
        if (available < count && !options.AllowPartial)
        {
            var status = CountItem(itemId) >= count
                ? InventoryTransactionStatus.Protected
                : InventoryTransactionStatus.InsufficientItems;
            return InventoryTransactionResult.Rejected(
                status,
                itemId,
                count,
                status == InventoryTransactionStatus.Protected
                    ? "Favorite items are protected from removal."
                    : $"Only {available} removable item(s) are available.");
        }

        var toMove = Math.Min(count, available);
        if (toMove <= 0)
        {
            var status = CountItem(itemId) > 0
                ? InventoryTransactionStatus.Protected
                : InventoryTransactionStatus.InsufficientItems;
            return InventoryTransactionResult.Rejected(status, itemId, count, "No removable items are available.");
        }

        var fromHotbar = Math.Min(hotbarAvailable, toMove);
        var hotbarResult = fromHotbar > 0
            ? Hotbar.RemoveTransaction(
                itemId,
                fromHotbar,
                options with { AllowPartial = false })
            : InventoryTransactionResult.NoChange(itemId);
        var fromMain = toMove - hotbarResult.Moved;
        var mainResult = fromMain > 0
            ? Main.RemoveTransaction(
                itemId,
                fromMain,
                options with { AllowPartial = false })
            : InventoryTransactionResult.NoChange(itemId);
        var moved = hotbarResult.Moved + mainResult.Moved;

        return moved == count
            ? InventoryTransactionResult.Complete(itemId, count, moved)
            : InventoryTransactionResult.Partial(itemId, count, moved, $"{count - moved} item(s) remain.");
    }

    public int CountItem(string itemId)
    {
        return Hotbar.CountItem(itemId) + Main.CountItem(itemId);
    }

    public void SelectHotbarSlot(int slot)
    {
        if (slot < 0 || slot >= HotbarSlotCount)
        {
            throw new ArgumentOutOfRangeException(nameof(slot), $"Hotbar slot {slot} is outside 0..{HotbarSlotCount - 1}.");
        }

        SelectedHotbarSlot = slot;
    }

    public void ScrollHotbar(int delta)
    {
        if (delta == 0)
        {
            return;
        }

        SelectedHotbarSlot = ((SelectedHotbarSlot + delta) % HotbarSlotCount + HotbarSlotCount) % HotbarSlotCount;
    }

    public bool QuickMoveToMain(int hotbarSlot)
    {
        return QuickMoveToMainTransaction(hotbarSlot).Completed;
    }

    public InventoryTransactionResult QuickMoveToMainTransaction(
        int hotbarSlot,
        InventoryTransactionOptions options = default)
    {
        ValidateHotbarSlot(hotbarSlot);
        return Hotbar.TransferSlotTo(Main, hotbarSlot, options);
    }

    public bool QuickMoveToHotbar(int mainSlot)
    {
        return QuickMoveToHotbarTransaction(mainSlot).Completed;
    }

    public InventoryTransactionResult QuickMoveToHotbarTransaction(
        int mainSlot,
        InventoryTransactionOptions options = default)
    {
        if (mainSlot < 0 || mainSlot >= Main.Slots.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(mainSlot));
        }

        return Main.TransferSlotTo(Hotbar, mainSlot, options);
    }

    public InventoryOrganizationResult CompactMain()
    {
        return Main.CompactStacks();
    }

    public InventoryOrganizationResult SortMain(InventorySortMode mode)
    {
        return Main.Sort(mode);
    }

    public InventoryOrganizationResult CompactHotbarExplicit()
    {
        return Hotbar.CompactStacks();
    }

    public InventoryOrganizationResult SortHotbarExplicit(InventorySortMode mode)
    {
        return Hotbar.Sort(mode);
    }

    private int MergeIntoExistingStacks(Inventory inventory, string itemId, int count)
    {
        var remaining = count;
        var maxStack = _items.GetById(itemId).MaxStack;
        foreach (var slot in inventory.Slots)
        {
            if (remaining <= 0)
            {
                break;
            }

            if (slot.IsEmpty ||
                !string.Equals(slot.Stack.ItemId, itemId, StringComparison.OrdinalIgnoreCase) ||
                slot.Stack.Count >= maxStack)
            {
                continue;
            }

            var moved = Math.Min(maxStack - slot.Stack.Count, remaining);
            slot.SetStack(slot.Stack.WithCount(slot.Stack.Count + moved));
            remaining -= moved;
        }

        return remaining;
    }

    private int PlaceIntoEmptySlots(Inventory inventory, string itemId, int count)
    {
        var remaining = count;
        var maxStack = _items.GetById(itemId).MaxStack;
        foreach (var slot in inventory.Slots)
        {
            if (remaining <= 0)
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

    private static int GetStateCapacity(Inventory inventory, string itemId, int maxStack, bool favorite)
    {
        var capacity = 0L;
        foreach (var slot in inventory.Slots)
        {
            if (slot.IsEmpty)
            {
                capacity += maxStack;
            }
            else if (slot.IsFavorite == favorite &&
                     string.Equals(slot.Stack.ItemId, itemId, StringComparison.OrdinalIgnoreCase))
            {
                capacity += Math.Max(0, maxStack - slot.Stack.Count);
            }
        }

        return (int)Math.Min(int.MaxValue, capacity);
    }

    private static int AddStateCore(
        Inventory inventory,
        string itemId,
        int count,
        int maxStack,
        bool favorite)
    {
        var remaining = count;
        foreach (var slot in inventory.Slots)
        {
            if (remaining <= 0)
            {
                return 0;
            }

            if (slot.IsEmpty || slot.IsFavorite != favorite ||
                !string.Equals(slot.Stack.ItemId, itemId, StringComparison.OrdinalIgnoreCase) ||
                slot.Stack.Count >= maxStack)
            {
                continue;
            }

            var moved = Math.Min(maxStack - slot.Stack.Count, remaining);
            slot.SetState(new InventorySlotState(slot.Stack.WithCount(slot.Stack.Count + moved), favorite));
            remaining -= moved;
        }

        foreach (var slot in inventory.Slots)
        {
            if (remaining <= 0)
            {
                return 0;
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

    private static void ValidateHotbarSlot(int slot)
    {
        if (slot < 0 || slot >= HotbarSlotCount)
        {
            throw new ArgumentOutOfRangeException(nameof(slot));
        }
    }
}
