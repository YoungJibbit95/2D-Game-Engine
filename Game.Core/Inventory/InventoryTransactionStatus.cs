namespace Game.Core.Inventory;

public enum InventoryTransactionStatus
{
    Completed,
    Partial,
    NoChange,
    InvalidRequest,
    UnknownItem,
    InsufficientItems,
    NoSpace,
    Protected
}
