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

    [Fact]
    public void QueryRecipes_ReturnsKnownStationAndCraftabilityState()
    {
        var items = CreateItems();
        var inventory = new PlayerInventory(items);
        inventory.AddItem(new ItemStack("dirt_block", 3));
        var recipe = CreateRecipe() with { Station = "workbench", Category = "blocks", SortOrder = 7 };
        var registry = RecipeRegistry.Create(new[] { recipe });
        var context = new CraftingContext(
            inventory,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "workbench" },
            new HashSet<string>(StringComparer.OrdinalIgnoreCase));

        var query = Assert.Single(new CraftingSystem().QueryRecipes(context, registry));

        Assert.True(query.IsKnown);
        Assert.True(query.HasStation);
        Assert.True(query.HasIngredients);
        Assert.True(query.CanCraft);
    }

    [Fact]
    public void Craft_WithPlayerInventory_ConsumesAcrossHotbarAndMain()
    {
        var items = CreateItems();
        var inventory = new PlayerInventory(items);
        inventory.Hotbar.Slots[0].SetStack(new ItemStack("dirt_block", 1));
        inventory.Main.Slots[0].SetStack(new ItemStack("dirt_block", 1));
        var context = new CraftingContext(
            inventory,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(StringComparer.OrdinalIgnoreCase));

        var crafted = new CraftingSystem().Craft(context, CreateRecipe());

        Assert.True(crafted);
        Assert.Equal(0, inventory.CountItem("dirt_block"));
        Assert.Equal(1, inventory.CountItem("stone_block"));
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
        return new Inventory(4, CreateItems());
    }

    private static ItemRegistry CreateItems()
    {
        return ItemRegistry.Create(new[]
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
        });
    }
}
