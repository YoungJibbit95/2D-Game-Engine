using Game.Core.Crafting;

namespace Game.Core.Events;

public sealed record CraftingBatchFailedEvent(
    string RecipeId,
    int DesiredQuantity,
    IReadOnlyList<CraftingFailureReason> Reasons) : IGameEvent;
