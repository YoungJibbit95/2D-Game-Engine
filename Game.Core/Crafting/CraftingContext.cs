using Game.Core.Inventory;

namespace Game.Core.Crafting;

public sealed record CraftingContext(
    PlayerInventory Inventory,
    IReadOnlySet<string> AvailableStations,
    IReadOnlySet<string> KnownRecipeIds)
{
    public bool Knows(RecipeDefinition recipe)
    {
        return recipe.KnownByDefault || KnownRecipeIds.Contains(recipe.Id);
    }

    public bool HasStation(RecipeDefinition recipe)
    {
        return recipe.Station is null || AvailableStations.Contains(recipe.Station);
    }
}
