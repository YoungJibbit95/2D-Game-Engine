namespace Game.Core.Crafting;

public enum CraftingFailureReason
{
    None = 0,
    InvalidQuantity,
    UnknownRecipe,
    MissingStation,
    MissingIngredients,
    InsufficientOutputCapacity,
    RequestedQuantityUnavailable,
    InventoryChanged
}
