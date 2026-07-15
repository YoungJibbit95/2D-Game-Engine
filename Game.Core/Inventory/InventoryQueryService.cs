using Game.Core.Items;

namespace Game.Core.Inventory;

public sealed class InventoryQueryService
{
    private readonly IItemDefinitionProvider _items;

    public InventoryQueryService(IItemDefinitionProvider items)
    {
        ArgumentNullException.ThrowIfNull(items);
        _items = items;
    }

    public IReadOnlyList<InventoryQueryEntry> Query(Inventory inventory, InventoryQuery? query = null)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        query ??= new InventoryQuery();

        return inventory.Slots
            .Select((slot, index) => (slot, index))
            .Where(entry => !entry.slot.IsEmpty && Matches(entry.slot, query))
            .Select(entry => new InventoryQueryEntry(
                inventory,
                entry.index,
                entry.slot.Stack,
                entry.slot.IsFavorite,
                _items.GetById(entry.slot.Stack.ItemId)))
            .ToArray();
    }

    public bool Matches(InventorySlot slot, InventoryQuery? query)
    {
        ArgumentNullException.ThrowIfNull(slot);
        if (slot.IsEmpty)
        {
            return false;
        }

        query ??= new InventoryQuery();
        var definition = _items.GetById(slot.Stack.ItemId);
        if (query.Category is { } category && definition.ResolvedCategory != category)
        {
            return false;
        }

        if (query.MinimumRarity is { } rarity && definition.Rarity < rarity)
        {
            return false;
        }

        if (query.FavoritesOnly && !slot.IsFavorite)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(query.SearchText))
        {
            return true;
        }

        var search = query.SearchText.Trim();
        return definition.Id.Contains(search, StringComparison.OrdinalIgnoreCase) ||
               definition.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
               definition.Description.Contains(search, StringComparison.OrdinalIgnoreCase) ||
               definition.Tags.Any(tag => tag.Contains(search, StringComparison.OrdinalIgnoreCase));
    }

    public InventoryStatistics GetStatistics(Inventory inventory)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        return GetStatistics(new[] { inventory });
    }

    public InventoryStatistics GetStatistics(PlayerInventory inventory)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        return GetStatistics(new[] { inventory.Hotbar, inventory.Main });
    }

    private InventoryStatistics GetStatistics(IReadOnlyList<Inventory> inventories)
    {
        var slots = inventories.SelectMany(inventory => inventory.Slots).ToArray();
        var occupied = slots.Where(slot => !slot.IsEmpty).ToArray();
        var categoryCounts = new Dictionary<ItemCategory, int>();
        var rarityCounts = new Dictionary<ItemRarity, int>();
        var uniqueIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var totalItems = 0;
        var totalValue = 0L;

        foreach (var slot in occupied)
        {
            var definition = _items.GetById(slot.Stack.ItemId);
            totalItems = checked(totalItems + slot.Stack.Count);
            totalValue = checked(totalValue + (long)definition.Value * slot.Stack.Count);
            uniqueIds.Add(slot.Stack.ItemId);
            AddCount(categoryCounts, definition.ResolvedCategory, slot.Stack.Count);
            AddCount(rarityCounts, definition.Rarity, slot.Stack.Count);
        }

        return new InventoryStatistics(
            slots.Length,
            occupied.Length,
            slots.Length - occupied.Length,
            totalItems,
            uniqueIds.Count,
            occupied.Count(slot => slot.IsFavorite),
            totalValue,
            categoryCounts,
            rarityCounts);
    }

    private static void AddCount<TKey>(Dictionary<TKey, int> counts, TKey key, int count)
        where TKey : notnull
    {
        counts[key] = counts.GetValueOrDefault(key) + count;
    }
}
