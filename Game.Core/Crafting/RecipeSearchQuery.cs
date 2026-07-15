namespace Game.Core.Crafting;

public sealed record RecipeSearchQuery
{
    public string SearchText { get; init; } = string.Empty;

    public string? Category { get; init; }

    public string? Station { get; init; }

    public RecipeVisibilityMode Visibility { get; init; } = RecipeVisibilityMode.Known;

    public RecipeSortMode Sort { get; init; } = RecipeSortMode.CraftableFirst;

    public bool SortDescending { get; init; }

    public bool PinnedFirst { get; init; } = true;
}
