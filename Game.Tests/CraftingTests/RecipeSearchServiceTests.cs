using Game.Core.Crafting;
using Game.Core.Inventory;
using Game.Core.Items;
using Xunit;

namespace Game.Tests.CraftingTests;

public sealed class RecipeSearchServiceTests
{
    [Fact]
    public void Search_AppliesKnownCraftableAndAllVisibilityModes()
    {
        var results = CreateResults();
        var service = new RecipeSearchService();

        var known = service.Search(results, new RecipeSearchQuery { Visibility = RecipeVisibilityMode.Known });
        var craftable = service.Search(results, new RecipeSearchQuery { Visibility = RecipeVisibilityMode.Craftable });
        var all = service.Search(results, new RecipeSearchQuery { Visibility = RecipeVisibilityMode.All });

        Assert.Equal(2, known.Count);
        Assert.Single(craftable);
        Assert.Equal("iron_sword", craftable[0].Recipe.Id);
        Assert.Equal(3, all.Count);
    }

    [Fact]
    public void Search_MatchesOutputDisplayNameAndIngredientDisplayName()
    {
        var service = new RecipeSearchService();
        var items = CreateItems();

        var byOutput = service.Search(
            CreateResults(),
            new RecipeSearchQuery { SearchText = "Iron Sword", Visibility = RecipeVisibilityMode.All },
            items);
        var byIngredient = service.Search(
            CreateResults(),
            new RecipeSearchQuery { SearchText = "Crystal", Visibility = RecipeVisibilityMode.All },
            items);

        Assert.Equal("iron_sword", Assert.Single(byOutput).Recipe.Id);
        Assert.Equal("magic_wand", Assert.Single(byIngredient).Recipe.Id);
    }

    [Fact]
    public void Search_FiltersCategoryAndStationCaseInsensitively()
    {
        var matches = new RecipeSearchService().Search(
            CreateResults(),
            new RecipeSearchQuery
            {
                Visibility = RecipeVisibilityMode.All,
                Category = "WEAPONS",
                Station = "ANVIL"
            });

        Assert.Equal("iron_sword", Assert.Single(matches).Recipe.Id);
    }

    [Fact]
    public void Search_PutsPinnedRecipesFirstWithoutDiscardingSortOrder()
    {
        var tracking = new RecipeTrackingState(["magic_wand"]);
        var matches = new RecipeSearchService().Search(
            CreateResults(),
            new RecipeSearchQuery
            {
                Visibility = RecipeVisibilityMode.All,
                Sort = RecipeSortMode.Name,
                PinnedFirst = true
            },
            CreateItems(),
            tracking);

        Assert.Equal("magic_wand", matches[0].Recipe.Id);
        Assert.Equal("campfire", matches[1].Recipe.Id);
        Assert.Equal("iron_sword", matches[2].Recipe.Id);
    }

    [Fact]
    public void Search_SortsByMaxCraftableDescendingWhenRequested()
    {
        var matches = new RecipeSearchService().Search(
            CreateResults(),
            new RecipeSearchQuery
            {
                Visibility = RecipeVisibilityMode.All,
                Sort = RecipeSortMode.MaxCraftable,
                SortDescending = true,
                PinnedFirst = false
            });

        Assert.Equal([7, 2, 0], matches.Select(result => result.MaxCraftable));
    }

    [Fact]
    public void Search_UsesRelevanceBeforeRecipeSortOrder()
    {
        var matches = new RecipeSearchService().Search(
            CreateResults(),
            new RecipeSearchQuery
            {
                SearchText = "iron",
                Visibility = RecipeVisibilityMode.All,
                Sort = RecipeSortMode.Relevance,
                PinnedFirst = false
            },
            CreateItems());

        Assert.Equal("iron_sword", Assert.Single(matches).Recipe.Id);
    }

    private static IReadOnlyList<CraftingQueryResult> CreateResults()
    {
        return
        [
            Result(Recipe("campfire", "campfire", "utility", null, "wood"), known: true, craftable: false, max: 2),
            Result(Recipe("iron_sword", "iron_sword", "weapons", "anvil", "iron_bar"), known: true, craftable: true, max: 7),
            Result(Recipe("magic_wand", "magic_wand", "magic", "altar", "mana_crystal"), known: false, craftable: false, max: 0)
        ];
    }

    private static CraftingQueryResult Result(RecipeDefinition recipe, bool known, bool craftable, int max)
    {
        return new CraftingQueryResult(recipe, known, HasStation: craftable, HasIngredients: craftable, CanCraft: craftable)
        {
            MaxCraftable = max
        };
    }

    private static RecipeDefinition Recipe(string id, string resultItemId, string category, string? station, string ingredientId)
    {
        return new RecipeDefinition
        {
            Id = id,
            Result = new ItemStack(resultItemId, 1),
            Ingredients = [new RecipeIngredient { ItemId = ingredientId, Count = 1 }],
            Category = category,
            Station = station
        };
    }

    private static ItemRegistry CreateItems()
    {
        return ItemRegistry.Create(
        [
            Item("campfire", "Campfire"),
            Item("iron_sword", "Iron Sword"),
            Item("magic_wand", "Magic Wand"),
            Item("wood", "Wood"),
            Item("iron_bar", "Iron Bar"),
            Item("mana_crystal", "Mana Crystal")
        ]);
    }

    private static ItemDefinition Item(string id, string displayName)
    {
        return new ItemDefinition
        {
            Id = id,
            DisplayName = displayName,
            TexturePath = $"items/{id}",
            Type = ItemType.Material,
            MaxStack = 999
        };
    }
}
