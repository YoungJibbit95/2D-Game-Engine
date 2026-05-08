using Game.Core.Inventory;

namespace Game.Core.Crafting;

public sealed record RecipeDefinition
{
    public required string Id { get; init; }

    public required ItemStack Result { get; init; }

    public required IReadOnlyList<RecipeIngredient> Ingredients { get; init; }

    public string? Station { get; init; }
}
