namespace Game.Core.Crafting;

public sealed record CraftingQueryResult(RecipeDefinition Recipe, bool IsKnown, bool HasStation, bool HasIngredients, bool CanCraft)
{
    public int MaxCraftable { get; init; }

    public IReadOnlyList<CraftingFailureReason> FailureReasons { get; init; } = Array.Empty<CraftingFailureReason>();
}
