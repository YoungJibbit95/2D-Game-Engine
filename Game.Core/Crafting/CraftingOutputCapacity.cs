namespace Game.Core.Crafting;

public sealed record CraftingOutputCapacity(
    int MaxCraftCount,
    int MaxItemCount,
    int DesiredItemCount,
    int ActualItemCount)
{
    public bool FitsDesiredQuantity => DesiredItemCount <= MaxItemCount;
}
