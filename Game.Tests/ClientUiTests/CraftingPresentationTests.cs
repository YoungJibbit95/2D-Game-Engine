using Game.Client.UI;
using Game.Core.Crafting;
using Game.Core.Inventory;
using Microsoft.Xna.Framework;
using Xunit;

namespace Game.Tests.ClientUiTests;

public sealed class CraftingPresentationTests
{
    [Theory]
    [InlineData(320, 240, PixelUiDensity.Compact)]
    [InlineData(360, 240, PixelUiDensity.Compact)]
    [InlineData(1280, 720, PixelUiDensity.Regular)]
    [InlineData(1920, 1080, PixelUiDensity.Expanded)]
    [InlineData(2560, 1440, PixelUiDensity.Expanded)]
    public void TwoPaneLayout_ContainsReadableRecipeDetailAndActionRegions(
        int width,
        int height,
        PixelUiDensity expectedDensity)
    {
        var viewport = new Rectangle(0, 0, width, height);

        var layout = PixelCraftingLayoutPlanner.Resolve(viewport);
        var actions = PixelCraftingActionLayoutPlanner.Resolve(layout.ActionBar);

        Assert.Equal(expectedDensity, layout.Density);
        AssertContained(viewport, layout.Panel);
        AssertContained(layout.RecipeList, layout.RecipeHeader);
        AssertContained(layout.RecipeList, layout.RecipeRows);
        AssertContained(layout.Details, layout.DetailsHeader);
        AssertContained(layout.Details, layout.IngredientList);
        AssertContained(layout.Details, layout.ActionBar);
        Assert.True(layout.RecipeList.Right < layout.Details.X);
        Assert.False(layout.RecipeList.Intersects(layout.Details));
        Assert.False(layout.DetailsHeader.Intersects(layout.ActionBar));
        AssertContained(layout.ActionBar, actions.Decrease);
        AssertContained(layout.ActionBar, actions.Quantity);
        AssertContained(layout.ActionBar, actions.Increase);
        AssertContained(layout.ActionBar, actions.Maximum);
        AssertContained(layout.ActionBar, actions.Craft);
        Assert.True(actions.Decrease.Right <= actions.Quantity.X);
        Assert.True(actions.Quantity.Right <= actions.Increase.X);
        Assert.True(actions.Increase.Right <= actions.Maximum.X);
        Assert.True(actions.Maximum.Right <= actions.Craft.X);
    }

    [Fact]
    public void Availability_PresentsEmptyLockedMissingAndCraftableImmediately()
    {
        var recipe = CreateRecipe();

        var empty = CraftingAvailabilityPresenter.Resolve((CraftingQueryResult?)null);
        var locked = CraftingAvailabilityPresenter.Resolve(
            new CraftingQueryResult(recipe, IsKnown: false, HasStation: false, HasIngredients: false, CanCraft: false));
        var station = CraftingAvailabilityPresenter.Resolve(
            new CraftingQueryResult(recipe, IsKnown: true, HasStation: false, HasIngredients: true, CanCraft: false));
        var materials = CraftingAvailabilityPresenter.Resolve(
            new CraftingQueryResult(recipe, IsKnown: true, HasStation: true, HasIngredients: false, CanCraft: false));
        var craftable = CraftingAvailabilityPresenter.Resolve(
            new CraftingQueryResult(recipe, IsKnown: true, HasStation: true, HasIngredients: true, CanCraft: true)
            {
                MaxCraftable = 4
            });

        Assert.Equal(CraftingAvailabilityState.Empty, empty.State);
        Assert.Equal(CraftingAvailabilityState.Locked, locked.State);
        Assert.Equal(CraftingAvailabilityState.MissingStation, station.State);
        Assert.Equal(CraftingAvailabilityState.MissingMaterials, materials.State);
        Assert.Equal(CraftingAvailabilityState.Craftable, craftable.State);
        Assert.False(empty.CanCraft);
        Assert.False(locked.CanCraft);
        Assert.False(station.CanCraft);
        Assert.False(materials.CanCraft);
        Assert.True(craftable.CanCraft);
        Assert.Contains("DISCOVER", locked.Label, StringComparison.Ordinal);
        Assert.Contains("STATION", station.Label, StringComparison.Ordinal);
        Assert.Contains("MATERIAL", materials.Label, StringComparison.Ordinal);
        Assert.Contains("READY", craftable.Label, StringComparison.Ordinal);
    }

    [Fact]
    public void Navigation_SupportsRecipeMovementFocusCyclingAdjustmentAndActivation()
    {
        var recipeDown = CraftingNavigationPlanner.Resolve(
            CraftingFocusArea.Recipes,
            new CraftingNavigationInput(false, true, false, false, false, false, false, false));
        var quantityRight = CraftingNavigationPlanner.Resolve(
            CraftingFocusArea.Quantity,
            new CraftingNavigationInput(false, false, false, true, false, false, false, false));
        var quantityDown = CraftingNavigationPlanner.Resolve(
            CraftingFocusArea.Quantity,
            new CraftingNavigationInput(false, true, false, false, false, false, false, false));
        var craftConfirm = CraftingNavigationPlanner.Resolve(
            CraftingFocusArea.Craft,
            new CraftingNavigationInput(false, false, false, false, true, false, false, false));
        var shoulderWrap = CraftingNavigationPlanner.Resolve(
            CraftingFocusArea.Search,
            new CraftingNavigationInput(false, false, false, false, false, false, true, false));

        Assert.Equal(1, recipeDown.RecipeDelta);
        Assert.Equal(CraftingFocusArea.Recipes, recipeDown.Focus);
        Assert.Equal(1, quantityRight.ValueDelta);
        Assert.Equal(CraftingFocusArea.Pin, quantityDown.Focus);
        Assert.True(craftConfirm.Activate);
        Assert.Equal(CraftingFocusArea.Craft, shoulderWrap.Focus);
    }

    [Fact]
    public void PresentationAndNavigation_AreAllocationFreeInSteadyState()
    {
        var recipe = CreateRecipe();
        var result = new CraftingQueryResult(
            recipe,
            IsKnown: true,
            HasStation: true,
            HasIngredients: true,
            CanCraft: true)
        {
            MaxCraftable = 5
        };
        var input = new CraftingNavigationInput(false, true, false, false, false, false, false, false);
        var viewport = new Rectangle(0, 0, 1920, 1080);
        _ = CraftingAvailabilityPresenter.Resolve(result);
        _ = PixelCraftingLayoutPlanner.Resolve(viewport);
        _ = PixelCraftingActionLayoutPlanner.Resolve(new Rectangle(0, 0, 420, 34));
        _ = CraftingNavigationPlanner.Resolve(CraftingFocusArea.Recipes, input);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var checksum = 0;

        for (var iteration = 0; iteration < 10_000; iteration++)
        {
            checksum += (int)CraftingAvailabilityPresenter.Resolve(result).State;
            var layout = PixelCraftingLayoutPlanner.Resolve(viewport);
            checksum += PixelCraftingActionLayoutPlanner.Resolve(layout.ActionBar).Craft.Width;
            checksum += CraftingNavigationPlanner.Resolve(CraftingFocusArea.Recipes, input).RecipeDelta;
        }

        Assert.True(checksum > 0);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    private static RecipeDefinition CreateRecipe()
    {
        return new RecipeDefinition
        {
            Id = "test_recipe",
            Result = new ItemStack("test_item", 1),
            Ingredients = [new RecipeIngredient { ItemId = "wood", Count = 2 }],
            Station = "workbench"
        };
    }

    private static void AssertContained(Rectangle outer, Rectangle inner)
    {
        Assert.True(
            inner.X >= outer.X && inner.Y >= outer.Y && inner.Right <= outer.Right && inner.Bottom <= outer.Bottom,
            $"Expected {inner} to be contained by {outer}.");
    }
}
