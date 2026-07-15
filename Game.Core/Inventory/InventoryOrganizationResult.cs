namespace Game.Core.Inventory;

public readonly record struct InventoryOrganizationResult(int ChangedSlots, int FreedSlots)
{
    public bool Changed => ChangedSlots > 0;
}
