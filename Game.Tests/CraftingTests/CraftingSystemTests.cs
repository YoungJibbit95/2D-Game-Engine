using Game.Core.Crafting;
using Game.Core.Inventory;
using Game.Core.Items;
using Xunit;

namespace Game.Tests.CraftingTests;

public sealed class CraftingSystemTests
{
    [Fact]
    public void CanCraft_ReturnsTrueWhenIngredientsAndSpaceExist()
    {
        var inventory = CreateInventory();
        inventory.AddItem(new ItemStack("dirt_block", 2));
        var recipe = CreateRecipe();

        Assert.True(new CraftingSystem().CanCraft(inventory, recipe));
    }

    [Fact]
    public void Craft_ConsumesIngredientsAndAddsResult()
    {
        var inventory = CreateInventory();
        inventory.AddItem(new ItemStack("dirt_block", 3));
        var recipe = CreateRecipe();

        var crafted = new CraftingSystem().Craft(inventory, recipe);

        Assert.True(crafted);
        Assert.Equal(1, inventory.CountItem("dirt_block"));
        Assert.Equal(1, inventory.CountItem("stone_block"));
    }

    [Fact]
    public void Loader_ReadsRecipeJson()
    {
        const string json = """
        {
          "id": "stone_from_dirt",
          "result": { "itemId": "stone_block", "count": 1 },
          "ingredients": [
            { "itemId": "dirt_block", "count": 2 }
          ],
          "station": null
        }
        """;

        var recipe = new RecipeDefinitionJsonLoader().LoadDefinitionFromJson(json);

        Assert.Equal("stone_from_dirt", recipe.Id);
        Assert.Equal(new ItemStack("stone_block", 1), recipe.Result);
        Assert.Equal("dirt_block", recipe.Ingredients[0].ItemId);
    }

    private static RecipeDefinition CreateRecipe()
    {
        return new RecipeDefinition
        {
            Id = "stone_from_dirt",
            Result = new ItemStack("stone_block", 1),
            Ingredients = new[]
            {
                new RecipeIngredient { ItemId = "dirt_block", Count = 2 }
            }
        };
    }

    private static Inventory CreateInventory()
    {
        return new Inventory(4, ItemRegistry.Create(new[]
        {
            new ItemDefinition
            {
                Id = "dirt_block",
                DisplayName = "Dirt Block",
                Type = ItemType.PlaceableTile,
                TexturePath = "items/dirt_block",
                MaxStack = 999
            },
            new ItemDefinition
            {
                Id = "stone_block",
                DisplayName = "Stone Block",
                Type = ItemType.PlaceableTile,
                TexturePath = "items/stone_block",
                MaxStack = 999
            }
        }));
    }
}
