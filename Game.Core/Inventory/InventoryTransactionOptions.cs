namespace Game.Core.Inventory;

public readonly record struct InventoryTransactionOptions(
    bool AllowPartial = false,
    bool IncludeFavorites = false,
    bool IgnoreTrashProtection = false)
{
    public static InventoryTransactionOptions Atomic { get; } = new();

    public static InventoryTransactionOptions Partial { get; } = new(AllowPartial: true);
}
