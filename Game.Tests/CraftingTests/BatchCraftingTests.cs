using Game.Core.Crafting;
using Game.Core.Events;
using Game.Core.Inventory;
using Game.Core.Items;
using Xunit;

namespace Game.Tests.CraftingTests;

public sealed class BatchCraftingTests
{
    [Fact]
    public void PlanCraft_AggregatesDuplicateIngredientsAndReportsPartialRequest()
    {
        var inventory = new Inventory(4, CreateItems());
        inventory.AddItem(new ItemStack("wood", 5));
        var recipe = CreateRecipe(
            ingredients:
            [
                new RecipeIngredient { ItemId = "wood", Count = 2 },
                new RecipeIngredient { ItemId = "WOOD", Count = 1 }
            ]);

        var plan = new CraftingSystem().PlanCraft(inventory, recipe, desiredQuantity: 3);

        var ingredient = Assert.Single(plan.Ingredients);
        Assert.Equal("wood", ingredient.ItemId, ignoreCase: true);
        Assert.Equal(3, ingredient.PerCraft);
        Assert.Equal(5, ingredient.Available);
        Assert.Equal(9, ingredient.DesiredTotal);
        Assert.Equal(3, ingredient.ActualTotal);
        Assert.Equal(1, plan.MaxCraftable);
        Assert.Equal(1, plan.ActualQuantity);
        Assert.Contains(CraftingFailureReason.MissingIngredients, plan.FailureReasons);
        Assert.Contains(CraftingFailureReason.RequestedQuantityUnavailable, plan.FailureReasons);
    }

    [Fact]
    public void PlanCraft_ReportsOutputCapacityWhenIngredientStackCannotFreeSlot()
    {
        var inventory = new Inventory(2, CreateItems());
        inventory.AddItem(new ItemStack("wood", 3));
        inventory.AddItem(new ItemStack("filler", 999));

        var plan = new CraftingSystem().PlanCraft(inventory, CreateRecipe(), desiredQuantity: 1);

        Assert.Equal(0, plan.ActualQuantity);
        Assert.Equal(0, plan.MaxCraftable);
        Assert.Equal(0, plan.OutputCapacity.MaxItemCount);
        Assert.Contains(CraftingFailureReason.InsufficientOutputCapacity, plan.FailureReasons);
        Assert.Contains(CraftingFailureReason.RequestedQuantityUnavailable, plan.FailureReasons);
    }

    [Fact]
    public void PlanCraft_AccountsForSlotsFreedByTheWholeBatch()
    {
        var inventory = new Inventory(2, CreateItems());
        inventory.AddItem(new ItemStack("wood", 2));
        inventory.AddItem(new ItemStack("filler", 999));
        var recipe = CreateRecipe(ingredients: [new RecipeIngredient { ItemId = "wood", Count = 2 }]);

        var plan = new CraftingSystem().PlanCraft(inventory, recipe, desiredQuantity: 1);

        Assert.True(plan.CanCraft);
        Assert.Equal(1, plan.MaxCraftable);
        Assert.Equal(1, plan.OutputCapacity.MaxItemCount);
    }

    [Fact]
    public void CraftMaximum_CanUseLargerBatchThatFreesAnOutputSlot()
    {
        var inventory = new Inventory(2, CreateItems());
        inventory.AddItem(new ItemStack("wood", 2));
        inventory.AddItem(new ItemStack("filler", 999));
        var recipe = CreateRecipe(ingredients: [new RecipeIngredient { ItemId = "wood", Count = 1 }]);
        var crafting = new CraftingSystem();

        var singlePlan = crafting.PlanCraft(inventory, recipe, desiredQuantity: 1);
        var maximumResult = crafting.CraftMaximum(inventory, recipe);

        Assert.Equal(0, singlePlan.ActualQuantity);
        Assert.Equal(2, singlePlan.MaxCraftable);
        Assert.True(maximumResult.IsSuccess);
        Assert.Equal(2, maximumResult.CraftedQuantity);
        Assert.Equal(0, inventory.CountItem("wood"));
        Assert.Equal(2, inventory.CountItem("plank"));
    }

    [Fact]
    public void CraftMany_CraftsLargestAtomicPartialBatch()
    {
        var inventory = new Inventory(4, CreateItems());
        inventory.AddItem(new ItemStack("wood", 10));

        var result = new CraftingSystem().CraftMany(inventory, CreateRecipe(), desiredQuantity: 8);

        Assert.True(result.IsSuccess);
        Assert.True(result.IsPartial);
        Assert.Equal(5, result.CraftedQuantity);
        Assert.Equal(new ItemStack("plank", 5), result.Output);
        Assert.Equal(0, inventory.CountItem("wood"));
        Assert.Equal(5, inventory.CountItem("plank"));
        Assert.NotNull(result.CompletedEvent);
        Assert.Null(result.FailedEvent);
        Assert.Equal(10, Assert.Single(result.CompletedEvent!.ConsumedIngredients).Count);
    }

    [Fact]
    public void CraftMaximum_CraftsAllAvailableBatches()
    {
        var inventory = new Inventory(4, CreateItems());
        inventory.AddItem(new ItemStack("wood", 9));

        var result = new CraftingSystem().CraftMaximum(inventory, CreateRecipe());

        Assert.True(result.IsSuccess);
        Assert.False(result.IsPartial);
        Assert.Equal(4, result.Plan.DesiredQuantity);
        Assert.Equal(4, result.CraftedQuantity);
        Assert.Equal(1, inventory.CountItem("wood"));
        Assert.Equal(4, inventory.CountItem("plank"));
    }

    [Fact]
    public void CraftMany_FailedCapacityCheckDoesNotMutateAnySlot()
    {
        var inventory = new Inventory(2, CreateItems());
        inventory.AddItem(new ItemStack("wood", 3));
        inventory.AddItem(new ItemStack("filler", 999));
        var before = inventory.Slots.Select(slot => slot.GetState()).ToArray();

        var result = new CraftingSystem().CraftMany(inventory, CreateRecipe(), desiredQuantity: 1);

        Assert.False(result.IsSuccess);
        Assert.False(result.InventoryChanged);
        Assert.Equal(before, inventory.Slots.Select(slot => slot.GetState()).ToArray());
        Assert.NotNull(result.FailedEvent);
        Assert.Contains(CraftingFailureReason.InsufficientOutputCapacity, result.FailedEvent!.Reasons);
    }

    [Fact]
    public void PlanCraft_InvalidQuantityReturnsExplicitReason()
    {
        var inventory = new Inventory(4, CreateItems());
        inventory.AddItem(new ItemStack("wood", 10));

        var plan = new CraftingSystem().PlanCraft(inventory, CreateRecipe(), desiredQuantity: 0);

        Assert.False(plan.CanCraft);
        Assert.Equal(0, plan.ActualQuantity);
        Assert.Contains(CraftingFailureReason.InvalidQuantity, plan.FailureReasons);
    }

    [Fact]
    public void PlanCraft_ContextReportsUnknownRecipeAndMissingStation()
    {
        var items = CreateItems();
        var inventory = new PlayerInventory(items);
        inventory.AddItem(new ItemStack("wood", 10));
        var recipe = CreateRecipe() with { KnownByDefault = false, Station = "workbench" };
        var context = new CraftingContext(
            inventory,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(StringComparer.OrdinalIgnoreCase));

        var plan = new CraftingSystem().PlanCraft(context, recipe, desiredQuantity: 1);

        Assert.Equal(0, plan.MaxCraftable);
        Assert.Contains(CraftingFailureReason.UnknownRecipe, plan.FailureReasons);
        Assert.Contains(CraftingFailureReason.MissingStation, plan.FailureReasons);
    }

    [Fact]
    public void CraftMany_PlayerInventoryConsumesAggregatesAcrossContainers()
    {
        var items = CreateItems();
        var inventory = new PlayerInventory(items);
        inventory.Hotbar.Slots[0].SetStack(new ItemStack("wood", 2));
        inventory.Main.Slots[0].SetStack(new ItemStack("wood", 4));
        var context = CreateContext(inventory);

        var result = new CraftingSystem().CraftMany(context, CreateRecipe(), desiredQuantity: 3);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, inventory.CountItem("wood"));
        Assert.Equal(3, inventory.CountItem("plank"));
    }

    [Fact]
    public void Crafting_DoesNotConsumeFavoritedIngredients()
    {
        var inventory = new Inventory(4, CreateItems());
        inventory.AddItem(new ItemStack("wood", 2));
        inventory.Slots[0].SetFavorite(true);

        var result = new CraftingSystem().CraftMany(inventory, CreateRecipe(), desiredQuantity: 1);

        Assert.False(result.IsSuccess);
        Assert.Equal(2, inventory.CountItem("wood"));
        Assert.True(inventory.Slots[0].IsFavorite);
        Assert.Contains(CraftingFailureReason.MissingIngredients, result.Plan.FailureReasons);
    }

    [Fact]
    public void CraftMany_PublishesTypedCompletedEvent()
    {
        var inventory = new Inventory(4, CreateItems());
        inventory.AddItem(new ItemStack("wood", 4));
        var events = new GameEventBus();
        CraftingBatchCompletedEvent? published = null;
        events.Subscribe<CraftingBatchCompletedEvent>(gameEvent => published = gameEvent);

        var result = new CraftingSystem().CraftMany(inventory, CreateRecipe(), desiredQuantity: 2, events: events);

        Assert.True(result.IsSuccess);
        Assert.Same(result.CompletedEvent, published);
        Assert.Equal(2, published!.CraftedQuantity);
        Assert.Equal(new ItemStack("plank", 2), published.Output);
    }

    [Fact]
    public void CraftMany_PublishesTypedFailedEvent()
    {
        var inventory = new Inventory(4, CreateItems());
        var events = new GameEventBus();
        CraftingBatchFailedEvent? published = null;
        events.Subscribe<CraftingBatchFailedEvent>(gameEvent => published = gameEvent);

        var result = new CraftingSystem().CraftMany(inventory, CreateRecipe(), desiredQuantity: 1, events: events);

        Assert.False(result.IsSuccess);
        Assert.Same(result.FailedEvent, published);
        Assert.Contains(CraftingFailureReason.MissingIngredients, published!.Reasons);
    }

    [Fact]
    public void ExistingCraftApisRemainCompatible()
    {
        var inventory = new Inventory(4, CreateItems());
        inventory.AddItem(new ItemStack("wood", 2));
        var crafting = new CraftingSystem();

        Assert.True(crafting.CanCraft(inventory, CreateRecipe()));
        Assert.True(crafting.Craft(inventory, CreateRecipe()));
        Assert.False(crafting.Craft(inventory, CreateRecipe()));
    }

    private static CraftingContext CreateContext(PlayerInventory inventory)
    {
        return new CraftingContext(
            inventory,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(StringComparer.OrdinalIgnoreCase));
    }

    private static RecipeDefinition CreateRecipe(IReadOnlyList<RecipeIngredient>? ingredients = null)
    {
        return new RecipeDefinition
        {
            Id = "plank_from_wood",
            Result = new ItemStack("plank", 1),
            Ingredients = ingredients ?? [new RecipeIngredient { ItemId = "wood", Count = 2 }],
            Category = "materials"
        };
    }

    private static ItemRegistry CreateItems()
    {
        return ItemRegistry.Create(
        [
            CreateItem("wood", "Wood"),
            CreateItem("plank", "Wood Plank"),
            CreateItem("filler", "Filler")
        ]);
    }

    private static ItemDefinition CreateItem(string id, string displayName)
    {
        return new ItemDefinition
        {
            Id = id,
            DisplayName = displayName,
            Type = ItemType.Material,
            TexturePath = $"items/{id}",
            MaxStack = 999
        };
    }
}
