using Game.Core.Events;
using Game.Core.Inventory;

namespace Game.Core.Crafting;

public sealed record CraftingBatchResult(
    CraftingBatchPlan Plan,
    bool InventoryChanged,
    CraftingBatchCompletedEvent? CompletedEvent,
    CraftingBatchFailedEvent? FailedEvent)
{
    public bool IsSuccess => InventoryChanged && CompletedEvent is not null;

    public bool IsPartial => IsSuccess && !Plan.FulfillsDesiredQuantity;

    public int CraftedQuantity => IsSuccess ? Plan.ActualQuantity : 0;

    public ItemStack Output => IsSuccess ? Plan.PlannedOutput : ItemStack.Empty;

    public IGameEvent Event => (IGameEvent?)CompletedEvent ?? FailedEvent
        ?? throw new InvalidOperationException("A crafting result must contain a completed or failed event.");
}
