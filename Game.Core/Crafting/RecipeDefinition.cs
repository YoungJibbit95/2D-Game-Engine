using Game.Core.Inventory;

namespace Game.Core.Crafting;

public sealed record RecipeDefinition
{
    public required string Id { get; init; }

    public required ItemStack Result { get; init; }

    public required IReadOnlyList<RecipeIngredient> Ingredients { get; init; }

    public string? Station { get; init; }

    public string Category { get; init; } = "general";

    public int SortOrder { get; init; }

    public bool KnownByDefault { get; init; } = true;
}
