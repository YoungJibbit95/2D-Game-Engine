using Game.Core.Data;

namespace Game.Core.Crafting;

public sealed class RecipeRegistry
{
    private readonly Dictionary<string, RecipeDefinition> _byId;

    private RecipeRegistry(IEnumerable<RecipeDefinition> definitions)
    {
        _byId = new Dictionary<string, RecipeDefinition>(StringComparer.OrdinalIgnoreCase);

        foreach (var definition in definitions)
        {
            AddValidated(definition);
        }
    }

    public IReadOnlyCollection<RecipeDefinition> Definitions => _byId.Values;

    public static RecipeRegistry Create(IEnumerable<RecipeDefinition> definitions)
    {
        return new RecipeRegistry(definitions);
    }

    public RecipeDefinition GetById(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        if (!_byId.TryGetValue(id, out var recipe))
        {
            throw new KeyNotFoundException($"Recipe '{id}' was not registered.");
        }

        return recipe;
    }

    public bool TryGetById(string id, out RecipeDefinition definition)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return _byId.TryGetValue(id, out definition!);
    }

    private void AddValidated(RecipeDefinition definition)
    {
        Validate(definition);

        if (_byId.ContainsKey(definition.Id))
        {
            throw new RegistryValidationException($"Duplicate recipe id '{definition.Id}'.");
        }

        _byId.Add(definition.Id, definition);
    }

    private static void Validate(RecipeDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (string.IsNullOrWhiteSpace(definition.Id))
        {
            throw new RegistryValidationException("Recipe id is required.");
        }

        if (definition.Result.IsEmpty)
        {
            throw new RegistryValidationException($"Recipe '{definition.Id}' must produce a non-empty result.");
        }

        if (definition.Ingredients.Count == 0)
        {
            throw new RegistryValidationException($"Recipe '{definition.Id}' must have at least one ingredient.");
        }

        foreach (var ingredient in definition.Ingredients)
        {
            if (string.IsNullOrWhiteSpace(ingredient.ItemId) || ingredient.Count <= 0)
            {
                throw new RegistryValidationException($"Recipe '{definition.Id}' has an invalid ingredient.");
            }
        }
    }
}
