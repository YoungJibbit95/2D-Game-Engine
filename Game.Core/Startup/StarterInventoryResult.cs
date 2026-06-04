using Game.Core.Inventory;

namespace Game.Core.Startup;

public sealed record StarterInventoryResult(
    PlayerInventory Inventory,
    IReadOnlyList<StarterInventoryAppliedItem> AppliedItems,
    IReadOnlyList<StarterInventoryFailedItem> FailedItems)
{
    public bool Success => FailedItems.Count == 0;
}
