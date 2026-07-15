using Game.Core.Inventory;

namespace Game.Core.Crafting;

public sealed record CraftingBatchPlan(
    RecipeDefinition Recipe,
    int DesiredQuantity,
    int ActualQuantity,
    int MaxCraftable,
    IReadOnlyList<CraftingIngredientPlan> Ingredients,
    CraftingOutputCapacity OutputCapacity,
    IReadOnlyList<CraftingFailureReason> FailureReasons)
{
    public bool CanCraft => ActualQuantity > 0;

    public bool FulfillsDesiredQuantity => DesiredQuantity > 0 && ActualQuantity == DesiredQuantity;

    public ItemStack PlannedOutput => ActualQuantity <= 0
        ? ItemStack.Empty
        : new ItemStack(Recipe.Result.ItemId, SaturatingMultiply(Recipe.Result.Count, ActualQuantity));

    public CraftingFailureReason PrimaryFailureReason => FailureReasons.Count == 0
        ? CraftingFailureReason.None
        : FailureReasons[0];

    private static int SaturatingMultiply(int left, int right)
    {
        return (int)Math.Min(int.MaxValue, (long)left * right);
    }
}
