using Game.Core.Inventory;
using InventoryModel = Game.Core.Inventory.Inventory;

namespace Game.Core.Crafting;

public sealed class CraftingSystem
{
    public IReadOnlyList<RecipeDefinition> GetAvailableRecipes(InventoryModel inventory, RecipeRegistry recipes, string? station = null)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(recipes);

        return recipes.Definitions
            .Where(recipe => IsStationAvailable(recipe, station) && CanCraft(inventory, recipe, station))
            .ToArray();
    }

    public bool CanCraft(InventoryModel inventory, RecipeDefinition recipe, string? station = null)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(recipe);

        if (!IsStationAvailable(recipe, station))
        {
            return false;
        }

        foreach (var ingredient in recipe.Ingredients)
        {
            if (inventory.CountItem(ingredient.ItemId) < ingredient.Count)
            {
                return false;
            }
        }

        var simulatedInventory = inventory.Clone();
        foreach (var ingredient in recipe.Ingredients)
        {
            simulatedInventory.RemoveItem(ingredient.ItemId, ingredient.Count);
        }

        return simulatedInventory.CanAddItem(recipe.Result);
    }

    public bool Craft(InventoryModel inventory, RecipeDefinition recipe, string? station = null)
    {
        if (!CanCraft(inventory, recipe, station))
        {
            return false;
        }

        foreach (var ingredient in recipe.Ingredients)
        {
            inventory.RemoveItem(ingredient.ItemId, ingredient.Count);
        }

        return inventory.AddItem(recipe.Result);
    }

    private static bool IsStationAvailable(RecipeDefinition recipe, string? station)
    {
        return recipe.Station is null || string.Equals(recipe.Station, station, StringComparison.OrdinalIgnoreCase);
    }
}
