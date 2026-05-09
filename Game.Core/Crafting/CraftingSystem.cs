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

    public IReadOnlyList<CraftingQueryResult> QueryRecipes(CraftingContext context, RecipeRegistry recipes)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(recipes);

        return recipes.Definitions
            .OrderBy(recipe => recipe.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(recipe => recipe.SortOrder)
            .ThenBy(recipe => recipe.Id, StringComparer.OrdinalIgnoreCase)
            .Select(recipe =>
            {
                var known = context.Knows(recipe);
                var station = context.HasStation(recipe);
                var ingredients = HasIngredients(context.Inventory, recipe);
                return new CraftingQueryResult(recipe, known, station, ingredients, known && station && ingredients && CanFitResult(context.Inventory, recipe));
            })
            .ToArray();
    }

    public bool Craft(CraftingContext context, RecipeDefinition recipe)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(recipe);

        if (!context.Knows(recipe) || !context.HasStation(recipe) || !HasIngredients(context.Inventory, recipe) || !CanFitResult(context.Inventory, recipe))
        {
            return false;
        }

        foreach (var ingredient in recipe.Ingredients)
        {
            context.Inventory.RemoveItem(ingredient.ItemId, ingredient.Count);
        }

        return context.Inventory.AddItem(recipe.Result);
    }

    private static bool IsStationAvailable(RecipeDefinition recipe, string? station)
    {
        return recipe.Station is null || string.Equals(recipe.Station, station, StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasIngredients(PlayerInventory inventory, RecipeDefinition recipe)
    {
        return recipe.Ingredients.All(ingredient => inventory.CountItem(ingredient.ItemId) >= ingredient.Count);
    }

    private static bool CanFitResult(PlayerInventory inventory, RecipeDefinition recipe)
    {
        if (!HasIngredients(inventory, recipe))
        {
            return false;
        }

        var hotbar = inventory.Hotbar.Clone();
        var main = inventory.Main.Clone();
        var simulated = new PlayerInventory(hotbar, main, inventory.ItemDefinitions);
        foreach (var ingredient in recipe.Ingredients)
        {
            simulated.RemoveItem(ingredient.ItemId, ingredient.Count);
        }

        return simulated.CanAddItem(recipe.Result);
    }
}
