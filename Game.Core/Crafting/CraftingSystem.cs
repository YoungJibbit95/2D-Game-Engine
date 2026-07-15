using Game.Core.Events;
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
            .Where(recipe => CanCraft(inventory, recipe, station))
            .OrderBy(recipe => recipe.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(recipe => recipe.SortOrder)
            .ThenBy(recipe => recipe.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public bool CanCraft(InventoryModel inventory, RecipeDefinition recipe, string? station = null)
    {
        return PlanCraft(inventory, recipe, 1, station).ActualQuantity == 1;
    }

    public bool Craft(InventoryModel inventory, RecipeDefinition recipe, string? station = null)
    {
        return CraftMany(inventory, recipe, 1, station).IsSuccess;
    }

    public bool Craft(CraftingContext context, RecipeDefinition recipe)
    {
        return CraftMany(context, recipe, 1).IsSuccess;
    }

    public CraftingBatchPlan PlanCraft(
        InventoryModel inventory,
        RecipeDefinition recipe,
        int desiredQuantity,
        string? station = null)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(recipe);

        return BuildPlan(
            recipe,
            desiredQuantity,
            maximumRequested: false,
            isKnown: true,
            hasStation: IsStationAvailable(recipe, station),
            itemId => inventory.CountAvailableItem(itemId),
            quantity => CanExecute(inventory, recipe, quantity));
    }

    public CraftingBatchPlan PlanCraft(CraftingContext context, RecipeDefinition recipe, int desiredQuantity)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(recipe);

        return BuildPlan(
            recipe,
            desiredQuantity,
            maximumRequested: false,
            context.Knows(recipe),
            context.HasStation(recipe),
            itemId => CountAvailable(context.Inventory, itemId),
            quantity => CanExecute(context.Inventory, recipe, quantity));
    }

    public CraftingBatchPlan PlanCraftMaximum(InventoryModel inventory, RecipeDefinition recipe, string? station = null)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(recipe);

        return BuildPlan(
            recipe,
            desiredQuantity: 1,
            maximumRequested: true,
            isKnown: true,
            hasStation: IsStationAvailable(recipe, station),
            itemId => inventory.CountAvailableItem(itemId),
            quantity => CanExecute(inventory, recipe, quantity));
    }

    public CraftingBatchPlan PlanCraftMaximum(CraftingContext context, RecipeDefinition recipe)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(recipe);

        return BuildPlan(
            recipe,
            desiredQuantity: 1,
            maximumRequested: true,
            context.Knows(recipe),
            context.HasStation(recipe),
            itemId => CountAvailable(context.Inventory, itemId),
            quantity => CanExecute(context.Inventory, recipe, quantity));
    }

    public CraftingBatchResult CraftMany(
        InventoryModel inventory,
        RecipeDefinition recipe,
        int desiredQuantity,
        string? station = null,
        GameEventBus? events = null)
    {
        var plan = PlanCraft(inventory, recipe, desiredQuantity, station);
        return ExecutePlan(inventory, plan, events);
    }

    public CraftingBatchResult CraftMany(
        CraftingContext context,
        RecipeDefinition recipe,
        int desiredQuantity,
        GameEventBus? events = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        var plan = PlanCraft(context, recipe, desiredQuantity);
        return ExecutePlan(context.Inventory, plan, events);
    }

    public CraftingBatchResult CraftMaximum(
        InventoryModel inventory,
        RecipeDefinition recipe,
        string? station = null,
        GameEventBus? events = null)
    {
        var plan = PlanCraftMaximum(inventory, recipe, station);
        return ExecutePlan(inventory, plan, events);
    }

    public CraftingBatchResult CraftMaximum(
        CraftingContext context,
        RecipeDefinition recipe,
        GameEventBus? events = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        var plan = PlanCraftMaximum(context, recipe);
        return ExecutePlan(context.Inventory, plan, events);
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
                var plan = PlanCraft(context, recipe, 1);
                return new CraftingQueryResult(
                    recipe,
                    context.Knows(recipe),
                    context.HasStation(recipe),
                    plan.Ingredients.All(ingredient => ingredient.Available >= ingredient.PerCraft),
                    plan.ActualQuantity == 1)
                {
                    MaxCraftable = plan.MaxCraftable,
                    FailureReasons = plan.FailureReasons
                };
            })
            .ToArray();
    }

    private static CraftingBatchPlan BuildPlan(
        RecipeDefinition recipe,
        int desiredQuantity,
        bool maximumRequested,
        bool isKnown,
        bool hasStation,
        Func<string, int> countItem,
        Func<int, bool> canExecute)
    {
        var aggregate = AggregateIngredients(recipe);
        var maxByIngredients = aggregate.Count == 0
            ? 0
            : aggregate.Min(ingredient => countItem(ingredient.ItemId) / ingredient.Count);
        var outputLimitedMaximum = FindLargestExecutable(maxByIngredients, canExecute);
        var maxCraftable = isKnown && hasStation ? outputLimitedMaximum : 0;
        var requested = maximumRequested ? Math.Max(1, maxCraftable) : desiredQuantity;
        var validQuantity = requested > 0;
        var candidate = validQuantity && isKnown && hasStation
            ? Math.Min(requested, maxByIngredients)
            : 0;
        var actual = FindLargestExecutable(candidate, canExecute);

        if (!isKnown || !hasStation || !validQuantity)
        {
            actual = 0;
        }

        var reasons = BuildFailureReasons(
            requested,
            actual,
            maxByIngredients,
            candidate,
            isKnown,
            hasStation,
            validQuantity,
            maximumRequested);
        var ingredientPlans = aggregate
            .Select(ingredient => new CraftingIngredientPlan(
                ingredient.ItemId,
                ingredient.Count,
                countItem(ingredient.ItemId),
                SaturatingMultiply(ingredient.Count, Math.Max(0, requested)),
                SaturatingMultiply(ingredient.Count, actual)))
            .ToArray();
        var maxItemCount = SaturatingMultiply(recipe.Result.Count, maxCraftable);
        var desiredItemCount = SaturatingMultiply(recipe.Result.Count, Math.Max(0, requested));
        var actualItemCount = SaturatingMultiply(recipe.Result.Count, actual);

        return new CraftingBatchPlan(
            recipe,
            requested,
            actual,
            maxCraftable,
            ingredientPlans,
            new CraftingOutputCapacity(maxCraftable, maxItemCount, desiredItemCount, actualItemCount),
            reasons);
    }

    private static IReadOnlyList<CraftingFailureReason> BuildFailureReasons(
        int desired,
        int actual,
        int maxByIngredients,
        int candidate,
        bool isKnown,
        bool hasStation,
        bool validQuantity,
        bool maximumRequested)
    {
        var reasons = new List<CraftingFailureReason>();
        if (!validQuantity)
        {
            reasons.Add(CraftingFailureReason.InvalidQuantity);
        }

        if (!isKnown)
        {
            reasons.Add(CraftingFailureReason.UnknownRecipe);
        }

        if (!hasStation)
        {
            reasons.Add(CraftingFailureReason.MissingStation);
        }

        if (maxByIngredients == 0 || (!maximumRequested && validQuantity && desired > maxByIngredients))
        {
            reasons.Add(CraftingFailureReason.MissingIngredients);
        }

        if (maxByIngredients > 0 && actual < candidate)
        {
            reasons.Add(CraftingFailureReason.InsufficientOutputCapacity);
        }

        if (!maximumRequested && validQuantity && actual < desired)
        {
            reasons.Add(CraftingFailureReason.RequestedQuantityUnavailable);
        }

        return reasons;
    }

    private static IReadOnlyList<CraftingIngredientAmount> AggregateIngredients(RecipeDefinition recipe)
    {
        return recipe.Ingredients
            .GroupBy(ingredient => ingredient.ItemId, StringComparer.OrdinalIgnoreCase)
            .Select(group => new CraftingIngredientAmount(
                group.First().ItemId,
                group.Aggregate(0, (total, ingredient) => SaturatingAdd(total, ingredient.Count))))
            .OrderBy(ingredient => ingredient.ItemId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static int FindLargestExecutable(int upperBound, Func<int, bool> canExecute)
    {
        for (var quantity = Math.Max(0, upperBound); quantity > 0; quantity--)
        {
            if (canExecute(quantity))
            {
                return quantity;
            }
        }

        return 0;
    }

    private static bool CanExecute(InventoryModel inventory, RecipeDefinition recipe, int quantity)
    {
        var simulated = inventory.Clone();
        return Apply(simulated, recipe, quantity);
    }

    private static bool CanExecute(PlayerInventory inventory, RecipeDefinition recipe, int quantity)
    {
        var simulated = Clone(inventory);
        return Apply(simulated, recipe, quantity);
    }

    private static CraftingBatchResult ExecutePlan(InventoryModel inventory, CraftingBatchPlan plan, GameEventBus? events)
    {
        if (!plan.CanCraft)
        {
            return CompleteFailure(plan, events);
        }

        var simulated = inventory.Clone();
        if (!Apply(simulated, plan.Recipe, plan.ActualQuantity))
        {
            return CompleteFailure(AsInventoryChangedFailure(plan), events);
        }

        Copy(simulated, inventory);
        return CompleteSuccess(plan, events);
    }

    private static CraftingBatchResult ExecutePlan(PlayerInventory inventory, CraftingBatchPlan plan, GameEventBus? events)
    {
        if (!plan.CanCraft)
        {
            return CompleteFailure(plan, events);
        }

        var simulated = Clone(inventory);
        if (!Apply(simulated, plan.Recipe, plan.ActualQuantity))
        {
            return CompleteFailure(AsInventoryChangedFailure(plan), events);
        }

        Copy(simulated.Hotbar, inventory.Hotbar);
        Copy(simulated.Main, inventory.Main);
        return CompleteSuccess(plan, events);
    }

    private static bool Apply(InventoryModel inventory, RecipeDefinition recipe, int quantity)
    {
        if (quantity <= 0 || !TryCreateOutput(recipe, quantity, out var output))
        {
            return false;
        }

        foreach (var ingredient in AggregateIngredients(recipe))
        {
            if (!TryMultiply(ingredient.Count, quantity, out var total) || !inventory.RemoveItem(ingredient.ItemId, total))
            {
                return false;
            }
        }

        return inventory.AddItem(output);
    }

    private static bool Apply(PlayerInventory inventory, RecipeDefinition recipe, int quantity)
    {
        if (quantity <= 0 || !TryCreateOutput(recipe, quantity, out var output))
        {
            return false;
        }

        foreach (var ingredient in AggregateIngredients(recipe))
        {
            if (!TryMultiply(ingredient.Count, quantity, out var total) || !RemoveAvailable(inventory, ingredient.ItemId, total))
            {
                return false;
            }
        }

        return inventory.AddItem(output);
    }

    private static CraftingBatchResult CompleteSuccess(CraftingBatchPlan plan, GameEventBus? events)
    {
        var completed = new CraftingBatchCompletedEvent(
            plan.Recipe.Id,
            plan.DesiredQuantity,
            plan.ActualQuantity,
            plan.PlannedOutput,
            plan.Ingredients
                .Where(ingredient => ingredient.ActualTotal > 0)
                .Select(ingredient => new CraftingIngredientAmount(ingredient.ItemId, ingredient.ActualTotal))
                .ToArray(),
            !plan.FulfillsDesiredQuantity);
        events?.Publish(completed);
        return new CraftingBatchResult(plan, true, completed, null);
    }

    private static CraftingBatchResult CompleteFailure(CraftingBatchPlan plan, GameEventBus? events)
    {
        var reasons = plan.FailureReasons.Count == 0
            ? new[] { CraftingFailureReason.RequestedQuantityUnavailable }
            : plan.FailureReasons;
        var failed = new CraftingBatchFailedEvent(plan.Recipe.Id, plan.DesiredQuantity, reasons);
        events?.Publish(failed);
        return new CraftingBatchResult(plan, false, null, failed);
    }

    private static CraftingBatchPlan AsInventoryChangedFailure(CraftingBatchPlan plan)
    {
        return plan with
        {
            ActualQuantity = 0,
            Ingredients = plan.Ingredients.Select(ingredient => ingredient with { ActualTotal = 0 }).ToArray(),
            OutputCapacity = plan.OutputCapacity with { ActualItemCount = 0 },
            FailureReasons = plan.FailureReasons
                .Append(CraftingFailureReason.InventoryChanged)
                .Distinct()
                .ToArray()
        };
    }

    private static PlayerInventory Clone(PlayerInventory inventory)
    {
        return new PlayerInventory(inventory.Hotbar.Clone(), inventory.Main.Clone(), inventory.ItemDefinitions);
    }

    private static void Copy(InventoryModel source, InventoryModel destination)
    {
        if (source.Slots.Count != destination.Slots.Count)
        {
            throw new InvalidOperationException("Cannot copy inventories with different slot counts.");
        }

        for (var index = 0; index < source.Slots.Count; index++)
        {
            destination.Slots[index].SetState(source.Slots[index].GetState());
        }
    }

    private static int CountAvailable(PlayerInventory inventory, string itemId)
    {
        return inventory.Hotbar.CountAvailableItem(itemId) + inventory.Main.CountAvailableItem(itemId);
    }

    private static bool RemoveAvailable(PlayerInventory inventory, string itemId, int count)
    {
        if (CountAvailable(inventory, itemId) < count)
        {
            return false;
        }

        var fromHotbar = Math.Min(inventory.Hotbar.CountAvailableItem(itemId), count);
        if (fromHotbar > 0 && !inventory.Hotbar.RemoveItem(itemId, fromHotbar))
        {
            return false;
        }

        var fromMain = count - fromHotbar;
        return fromMain <= 0 || inventory.Main.RemoveItem(itemId, fromMain);
    }

    private static bool IsStationAvailable(RecipeDefinition recipe, string? station)
    {
        return recipe.Station is null || string.Equals(recipe.Station, station, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryCreateOutput(RecipeDefinition recipe, int quantity, out ItemStack output)
    {
        if (!TryMultiply(recipe.Result.Count, quantity, out var count))
        {
            output = ItemStack.Empty;
            return false;
        }

        output = new ItemStack(recipe.Result.ItemId, count);
        return true;
    }

    private static bool TryMultiply(int left, int right, out int result)
    {
        var value = (long)left * right;
        if (left <= 0 || right <= 0 || value > int.MaxValue)
        {
            result = 0;
            return false;
        }

        result = (int)value;
        return true;
    }

    private static int SaturatingMultiply(int left, int right)
    {
        return (int)Math.Min(int.MaxValue, Math.Max(0L, (long)left * right));
    }

    private static int SaturatingAdd(int left, int right)
    {
        return (int)Math.Min(int.MaxValue, (long)left + right);
    }
}
