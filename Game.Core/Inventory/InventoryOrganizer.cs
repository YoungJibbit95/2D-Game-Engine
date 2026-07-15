using Game.Core.Items;

namespace Game.Core.Inventory;

public static class InventoryOrganizer
{
    public static InventoryOrganizationResult Compact(Inventory inventory, IItemDefinitionProvider items)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(items);

        var movableIndexes = GetMovableIndexes(inventory);
        var usedBefore = movableIndexes.Count(index => !inventory.Slots[index].IsEmpty);
        var totals = new Dictionary<string, (string ItemId, int Count)>(StringComparer.OrdinalIgnoreCase);

        foreach (var index in movableIndexes)
        {
            var stack = inventory.Slots[index].Stack;
            if (stack.IsEmpty)
            {
                continue;
            }

            if (totals.TryGetValue(stack.ItemId, out var total))
            {
                totals[stack.ItemId] = (total.ItemId, checked(total.Count + stack.Count));
            }
            else
            {
                totals.Add(stack.ItemId, (stack.ItemId, stack.Count));
            }
        }

        var packed = new List<InventorySlotState>();
        foreach (var total in totals.Values)
        {
            var remaining = total.Count;
            var maxStack = items.GetById(total.ItemId).MaxStack;
            while (remaining > 0)
            {
                var count = Math.Min(maxStack, remaining);
                packed.Add(new InventorySlotState(new ItemStack(total.ItemId, count), false));
                remaining -= count;
            }
        }

        if (packed.Count > movableIndexes.Count)
        {
            return new InventoryOrganizationResult(0, 0);
        }

        var changedSlots = ApplyStates(inventory, movableIndexes, packed);
        return new InventoryOrganizationResult(changedSlots, Math.Max(0, usedBefore - packed.Count));
    }

    public static InventoryOrganizationResult Sort(
        Inventory inventory,
        IItemDefinitionProvider items,
        InventorySortMode mode)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(items);

        var movableIndexes = GetMovableIndexes(inventory);
        var states = movableIndexes
            .Select(index => inventory.Slots[index].GetState())
            .Where(state => !state.Stack.IsEmpty)
            .OrderBy(state => state, CreateComparer(items, mode))
            .ToArray();

        var changedSlots = ApplyStates(inventory, movableIndexes, states);
        return new InventoryOrganizationResult(changedSlots, 0);
    }

    private static List<int> GetMovableIndexes(Inventory inventory)
    {
        return inventory.Slots
            .Select((slot, index) => (slot, index))
            .Where(entry => !entry.slot.IsFavorite)
            .Select(entry => entry.index)
            .ToList();
    }

    private static int ApplyStates(
        Inventory inventory,
        IReadOnlyList<int> indexes,
        IReadOnlyList<InventorySlotState> states)
    {
        var changed = 0;
        for (var position = 0; position < indexes.Count; position++)
        {
            var state = position < states.Count ? states[position] : InventorySlotState.Empty;
            var slot = inventory.Slots[indexes[position]];
            if (slot.GetState() == state)
            {
                continue;
            }

            slot.SetState(state);
            changed++;
        }

        return changed;
    }

    private static IComparer<InventorySlotState> CreateComparer(
        IItemDefinitionProvider items,
        InventorySortMode mode)
    {
        return Comparer<InventorySlotState>.Create((left, right) =>
        {
            var leftItem = items.GetById(left.Stack.ItemId);
            var rightItem = items.GetById(right.Stack.ItemId);

            var comparison = leftItem.SortPriority.CompareTo(rightItem.SortPriority);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = mode switch
            {
                InventorySortMode.ItemType => CompareItemType(leftItem, rightItem),
                InventorySortMode.Rarity => rightItem.Rarity.CompareTo(leftItem.Rarity),
                InventorySortMode.Name => StringComparer.OrdinalIgnoreCase.Compare(leftItem.DisplayName, rightItem.DisplayName),
                InventorySortMode.Value => rightItem.Value.CompareTo(leftItem.Value),
                _ => 0
            };

            if (comparison != 0)
            {
                return comparison;
            }

            comparison = leftItem.ResolvedCategory.CompareTo(rightItem.ResolvedCategory);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = StringComparer.OrdinalIgnoreCase.Compare(leftItem.DisplayName, rightItem.DisplayName);
            return comparison != 0
                ? comparison
                : StringComparer.OrdinalIgnoreCase.Compare(leftItem.Id, rightItem.Id);
        });
    }

    private static int CompareItemType(ItemDefinition left, ItemDefinition right)
    {
        var comparison = left.ResolvedCategory.CompareTo(right.ResolvedCategory);
        return comparison != 0 ? comparison : left.Type.CompareTo(right.Type);
    }
}
