using Game.Core.Crafting;
using Game.Core.Inventory;

namespace Game.Core.Events;

public sealed record CraftingBatchCompletedEvent(
    string RecipeId,
    int DesiredQuantity,
    int CraftedQuantity,
    ItemStack Output,
    IReadOnlyList<CraftingIngredientAmount> ConsumedIngredients,
    bool IsPartial) : IGameEvent;
