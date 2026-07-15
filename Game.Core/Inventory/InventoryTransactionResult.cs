namespace Game.Core.Inventory;

public readonly record struct InventoryTransactionResult(
    InventoryTransactionStatus Status,
    string ItemId,
    int Requested,
    int Moved,
    int Remaining,
    string? Error = null)
{
    public bool Changed => Moved > 0;

    public bool Completed => Remaining == 0 && Status is InventoryTransactionStatus.Completed or InventoryTransactionStatus.NoChange;

    public static InventoryTransactionResult Complete(string itemId, int requested, int moved)
    {
        return new InventoryTransactionResult(InventoryTransactionStatus.Completed, itemId, requested, moved, 0);
    }

    public static InventoryTransactionResult Partial(string itemId, int requested, int moved, string? error = null)
    {
        return new InventoryTransactionResult(InventoryTransactionStatus.Partial, itemId, requested, moved, Math.Max(0, requested - moved), error);
    }

    public static InventoryTransactionResult Rejected(
        InventoryTransactionStatus status,
        string itemId,
        int requested,
        string error)
    {
        return new InventoryTransactionResult(status, itemId, Math.Max(0, requested), 0, Math.Max(0, requested), error);
    }

    public static InventoryTransactionResult NoChange(string itemId = "")
    {
        return new InventoryTransactionResult(InventoryTransactionStatus.NoChange, itemId, 0, 0, 0);
    }
}
