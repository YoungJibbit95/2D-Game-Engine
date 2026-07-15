using Game.Core.Items;

namespace Game.Core.Inventory;

public sealed record InventoryQueryEntry(
    Inventory Inventory,
    int SlotIndex,
    ItemStack Stack,
    bool IsFavorite,
    ItemDefinition Definition);
