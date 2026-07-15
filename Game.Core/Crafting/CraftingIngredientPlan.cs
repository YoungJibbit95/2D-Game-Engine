namespace Game.Core.Crafting;

public sealed record CraftingIngredientPlan(
    string ItemId,
    int PerCraft,
    int Available,
    int DesiredTotal,
    int ActualTotal)
{
    public int MissingForDesired => Math.Max(0, DesiredTotal - Available);

    public int MaxCraftable => PerCraft <= 0 ? 0 : Available / PerCraft;
}
