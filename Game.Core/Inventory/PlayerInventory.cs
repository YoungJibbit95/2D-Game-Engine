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

    public int SelectedHotbarSlot { get; private set; }

    public ItemStack SelectedStack => Hotbar.Slots[SelectedHotbarSlot].Stack;

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

        var remaining = stack.Count;
        remaining = MergeIntoExistingStacks(Hotbar, stack.ItemId, remaining);
        remaining = MergeIntoExistingStacks(Main, stack.ItemId, remaining);
        remaining = PlaceIntoEmptySlots(Hotbar, stack.ItemId, remaining);
        remaining = PlaceIntoEmptySlots(Main, stack.ItemId, remaining);
        return remaining == 0;
    }

    public bool CanAddItem(ItemStack stack)
    {
        if (stack.IsEmpty)
        {
            return true;
        }

        var remaining = stack.Count;
        remaining = CountAvailableStackSpace(Hotbar, stack.ItemId, remaining);
        remaining = CountAvailableStackSpace(Main, stack.ItemId, remaining);
        remaining = CountEmptySlotSpace(Hotbar, stack.ItemId, remaining);
        remaining = CountEmptySlotSpace(Main, stack.ItemId, remaining);
        return remaining <= 0;
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
        var hotbarCount = Math.Min(Hotbar.CountItem(itemId), remaining);
        if (hotbarCount > 0)
        {
            Hotbar.RemoveItem(itemId, hotbarCount);
            remaining -= hotbarCount;
        }

        if (remaining > 0)
        {
            Main.RemoveItem(itemId, remaining);
        }

        return true;
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
        ValidateHotbarSlot(hotbarSlot);
        return MoveSlotToInventory(Hotbar, hotbarSlot, Main);
    }

    public bool QuickMoveToHotbar(int mainSlot)
    {
        if (mainSlot < 0 || mainSlot >= Main.Slots.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(mainSlot));
        }

        return MoveSlotToInventory(Main, mainSlot, Hotbar);
    }

    private int MergeIntoExistingStacks(Inventory inventory, string itemId, int count)
    {
        var remaining = count;
        var maxStack = _items.GetById(itemId).MaxStack;

        foreach (var slot in inventory.Slots)
        {
            if (remaining == 0)
            {
                break;
            }

            if (slot.IsEmpty || !string.Equals(slot.Stack.ItemId, itemId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var space = maxStack - slot.Stack.Count;
            if (space <= 0)
            {
                continue;
            }

            var moved = Math.Min(space, remaining);
            slot.SetStack(slot.Stack.WithCount(slot.Stack.Count + moved));
            remaining -= moved;
        }

        return remaining;
    }

    private int CountAvailableStackSpace(Inventory inventory, string itemId, int count)
    {
        var remaining = count;
        var maxStack = _items.GetById(itemId).MaxStack;

        foreach (var slot in inventory.Slots)
        {
            if (remaining <= 0)
            {
                break;
            }

            if (slot.IsEmpty || !string.Equals(slot.Stack.ItemId, itemId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            remaining -= Math.Max(0, maxStack - slot.Stack.Count);
        }

        return remaining;
    }

    private int CountEmptySlotSpace(Inventory inventory, string itemId, int count)
    {
        var remaining = count;
        var maxStack = _items.GetById(itemId).MaxStack;

        foreach (var slot in inventory.Slots)
        {
            if (remaining <= 0)
            {
                break;
            }

            if (slot.IsEmpty)
            {
                remaining -= maxStack;
            }
        }

        return remaining;
    }

    private int PlaceIntoEmptySlots(Inventory inventory, string itemId, int count)
    {
        var remaining = count;
        var maxStack = _items.GetById(itemId).MaxStack;

        foreach (var slot in inventory.Slots)
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

    private static bool MoveSlotToInventory(Inventory source, int sourceSlot, Inventory target)
    {
        var stack = source.Slots[sourceSlot].Stack;
        if (stack.IsEmpty)
        {
            return true;
        }

        if (!target.CanAddItem(stack))
        {
            return false;
        }

        target.AddItem(stack);
        source.Slots[sourceSlot].Clear();
        return true;
    }

    private static void ValidateHotbarSlot(int slot)
    {
        if (slot < 0 || slot >= HotbarSlotCount)
        {
            throw new ArgumentOutOfRangeException(nameof(slot));
        }
    }
}
