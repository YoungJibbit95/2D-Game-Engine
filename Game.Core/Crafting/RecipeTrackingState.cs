namespace Game.Core.Crafting;

public sealed class RecipeTrackingState
{
    private readonly HashSet<string> _pinnedRecipeIds;

    public RecipeTrackingState(IEnumerable<string>? pinnedRecipeIds = null)
    {
        _pinnedRecipeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (pinnedRecipeIds is null)
        {
            return;
        }

        foreach (var recipeId in pinnedRecipeIds)
        {
            if (!string.IsNullOrWhiteSpace(recipeId))
            {
                _pinnedRecipeIds.Add(recipeId.Trim());
            }
        }
    }

    public event Action<RecipeTrackingChange>? Changed;

    public long Version { get; private set; }

    public IReadOnlyList<string> PinnedRecipeIds => _pinnedRecipeIds
        .OrderBy(recipeId => recipeId, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    public bool IsPinned(string recipeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recipeId);
        return _pinnedRecipeIds.Contains(recipeId);
    }

    public bool Pin(string recipeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recipeId);
        var normalized = recipeId.Trim();
        if (!_pinnedRecipeIds.Add(normalized))
        {
            return false;
        }

        Version = Version == long.MaxValue ? 1 : Version + 1;
        Changed?.Invoke(new RecipeTrackingChange(normalized, IsPinned: true));
        return true;
    }

    public bool Unpin(string recipeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recipeId);
        if (!_pinnedRecipeIds.Remove(recipeId))
        {
            return false;
        }

        Version = Version == long.MaxValue ? 1 : Version + 1;
        Changed?.Invoke(new RecipeTrackingChange(recipeId, IsPinned: false));
        return true;
    }

    public bool TogglePin(string recipeId)
    {
        return IsPinned(recipeId) ? !Unpin(recipeId) : Pin(recipeId);
    }

    public void Clear()
    {
        foreach (var recipeId in PinnedRecipeIds)
        {
            Unpin(recipeId);
        }
    }
}
