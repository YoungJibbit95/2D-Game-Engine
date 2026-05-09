namespace Game.Core.Inventory;

public readonly record struct InventoryInteractionResult(bool Changed, string? Error)
{
    public static InventoryInteractionResult NoChange { get; } = new(false, null);

    public static InventoryInteractionResult Success { get; } = new(true, null);

    public static InventoryInteractionResult Failed(string error)
    {
        return new InventoryInteractionResult(false, error);
    }
}
