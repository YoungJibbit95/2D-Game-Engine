using Game.Core.Items;

namespace Game.Core.Inventory;

public sealed record InventoryStatistics(
    int TotalSlots,
    int UsedSlots,
    int AvailableSlots,
    int TotalItems,
    int UniqueItemTypes,
    int FavoriteSlots,
    long TotalValue,
    IReadOnlyDictionary<ItemCategory, int> ItemsByCategory,
    IReadOnlyDictionary<ItemRarity, int> ItemsByRarity);
