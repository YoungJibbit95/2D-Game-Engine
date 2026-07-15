using Game.Core.Items;

namespace Game.Core.Crafting;

public sealed class RecipeSearchService
{
    public IReadOnlyList<CraftingQueryResult> Search(
        IEnumerable<CraftingQueryResult> results,
        RecipeSearchQuery? query = null,
        IItemDefinitionProvider? items = null,
        RecipeTrackingState? tracking = null)
    {
        ArgumentNullException.ThrowIfNull(results);
        query ??= new RecipeSearchQuery();

        var terms = query.SearchText
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var entries = results
            .Where(result => IsVisible(result, query.Visibility))
            .Where(result => MatchesCategory(result, query.Category))
            .Where(result => MatchesStation(result, query.Station))
            .Select(result => CreateEntry(result, terms, items, tracking))
            .Where(entry => entry.MatchesSearch)
            .ToArray();

        IOrderedEnumerable<SearchEntry> ordered = entries
            .OrderByDescending(entry => query.PinnedFirst && entry.IsPinned);
        ordered = query.Sort switch
        {
            RecipeSortMode.Relevance => ordered
                .ThenByDescending(entry => entry.SearchScore),
            RecipeSortMode.Name when query.SortDescending => ordered
                .ThenByDescending(entry => entry.DisplayName, StringComparer.OrdinalIgnoreCase),
            RecipeSortMode.Name => ordered
                .ThenBy(entry => entry.DisplayName, StringComparer.OrdinalIgnoreCase),
            RecipeSortMode.Category when query.SortDescending => ordered
                .ThenByDescending(entry => entry.Result.Recipe.Category, StringComparer.OrdinalIgnoreCase)
                .ThenByDescending(entry => entry.DisplayName, StringComparer.OrdinalIgnoreCase),
            RecipeSortMode.Category => ordered
                .ThenBy(entry => entry.Result.Recipe.Category, StringComparer.OrdinalIgnoreCase)
                .ThenBy(entry => entry.DisplayName, StringComparer.OrdinalIgnoreCase),
            RecipeSortMode.MaxCraftable when query.SortDescending => ordered
                .ThenByDescending(entry => entry.Result.MaxCraftable),
            RecipeSortMode.MaxCraftable => ordered
                .ThenBy(entry => entry.Result.MaxCraftable),
            _ => ordered
                .ThenByDescending(entry => entry.Result.CanCraft)
                .ThenByDescending(entry => entry.Result.MaxCraftable)
        };

        return ordered
            .ThenBy(entry => entry.Result.Recipe.SortOrder)
            .ThenBy(entry => entry.Result.Recipe.Id, StringComparer.OrdinalIgnoreCase)
            .Select(entry => entry.Result)
            .ToArray();
    }

    private static SearchEntry CreateEntry(
        CraftingQueryResult result,
        IReadOnlyList<string> terms,
        IItemDefinitionProvider? items,
        RecipeTrackingState? tracking)
    {
        var recipe = result.Recipe;
        var displayName = TryGetDisplayName(items, recipe.Result.ItemId) ?? recipe.Result.ItemId;
        var fields = new List<string>
        {
            recipe.Id,
            recipe.Result.ItemId,
            displayName,
            recipe.Category,
            recipe.Station ?? string.Empty
        };

        foreach (var ingredient in recipe.Ingredients)
        {
            fields.Add(ingredient.ItemId);
            var ingredientName = TryGetDisplayName(items, ingredient.ItemId);
            if (ingredientName is not null)
            {
                fields.Add(ingredientName);
            }
        }

        var matches = terms.All(term => fields.Any(field => field.Contains(term, StringComparison.OrdinalIgnoreCase)));
        var score = terms.Sum(term => ScoreTerm(term, recipe, displayName, fields));
        var pinned = tracking?.IsPinned(recipe.Id) ?? false;
        return new SearchEntry(result, displayName, matches, score, pinned);
    }

    private static bool IsVisible(CraftingQueryResult result, RecipeVisibilityMode visibility)
    {
        return visibility switch
        {
            RecipeVisibilityMode.Known => result.IsKnown,
            RecipeVisibilityMode.Craftable => result.CanCraft,
            RecipeVisibilityMode.All => true,
            _ => throw new ArgumentOutOfRangeException(nameof(visibility), visibility, "Unsupported recipe visibility mode.")
        };
    }

    private static bool MatchesCategory(CraftingQueryResult result, string? category)
    {
        return string.IsNullOrWhiteSpace(category) ||
               string.Equals(result.Recipe.Category, category.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesStation(CraftingQueryResult result, string? station)
    {
        return string.IsNullOrWhiteSpace(station) ||
               string.Equals(result.Recipe.Station, station.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static int ScoreTerm(
        string term,
        RecipeDefinition recipe,
        string displayName,
        IReadOnlyList<string> fields)
    {
        if (displayName.StartsWith(term, StringComparison.OrdinalIgnoreCase))
        {
            return 100;
        }

        if (recipe.Result.ItemId.StartsWith(term, StringComparison.OrdinalIgnoreCase) ||
            recipe.Id.StartsWith(term, StringComparison.OrdinalIgnoreCase))
        {
            return 75;
        }

        if (displayName.Contains(term, StringComparison.OrdinalIgnoreCase))
        {
            return 50;
        }

        return fields.Any(field => field.Contains(term, StringComparison.OrdinalIgnoreCase)) ? 20 : 0;
    }

    private static string? TryGetDisplayName(IItemDefinitionProvider? items, string itemId)
    {
        return items is not null && items.TryGetById(itemId, out var definition)
            ? definition.DisplayName
            : null;
    }

    private sealed record SearchEntry(
        CraftingQueryResult Result,
        string DisplayName,
        bool MatchesSearch,
        int SearchScore,
        bool IsPinned);
}
