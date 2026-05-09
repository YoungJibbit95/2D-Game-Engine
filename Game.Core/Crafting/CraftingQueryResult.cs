namespace Game.Core.Crafting;

public sealed record CraftingQueryResult(RecipeDefinition Recipe, bool IsKnown, bool HasStation, bool HasIngredients, bool CanCraft);
