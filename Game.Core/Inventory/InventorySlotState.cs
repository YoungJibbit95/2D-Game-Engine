namespace Game.Core.Inventory;

public readonly record struct InventorySlotState(ItemStack Stack, bool IsFavorite)
{
    public static InventorySlotState Empty { get; } = new(ItemStack.Empty, false);
}
