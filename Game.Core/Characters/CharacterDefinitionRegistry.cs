using Game.Core.Data;

namespace Game.Core.Characters;

public sealed class CharacterDefinitionRegistry
{
    private readonly Dictionary<string, CharacterDefinition> _byId;

    private CharacterDefinitionRegistry(IEnumerable<CharacterDefinition> definitions)
    {
        _byId = new Dictionary<string, CharacterDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var definition in definitions)
        {
            AddValidated(definition);
        }
    }

    public IReadOnlyCollection<CharacterDefinition> Definitions => _byId.Values;

    public static CharacterDefinitionRegistry Create(IEnumerable<CharacterDefinition> definitions)
    {
        return new CharacterDefinitionRegistry(definitions);
    }

    public CharacterDefinition GetById(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (!_byId.TryGetValue(id, out var definition))
        {
            throw new KeyNotFoundException($"Character definition '{id}' was not registered.");
        }

        return definition;
    }

    public bool TryGetById(string id, out CharacterDefinition definition)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return _byId.TryGetValue(id, out definition!);
    }

    private void AddValidated(CharacterDefinition definition)
    {
        Validate(definition);
        if (_byId.ContainsKey(definition.Id))
        {
            throw new RegistryValidationException($"Duplicate character definition id '{definition.Id}'.");
        }

        _byId.Add(definition.Id, definition);
    }

    private static void Validate(CharacterDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        RequireText(definition.Id, nameof(definition.Id));
        RequireText(definition.DisplayName, nameof(definition.DisplayName));
        RequireText(definition.AnimationSet.Id, nameof(definition.AnimationSet.Id));
        RequireText(definition.DefaultAppearance.BodySpriteId, nameof(definition.DefaultAppearance.BodySpriteId));

        if (definition.Width <= 0 || definition.Height <= 0)
        {
            throw new RegistryValidationException($"Character '{definition.Id}' must have positive dimensions.");
        }

        if (definition.AnimationSet.StateClips.Count == 0 && string.IsNullOrWhiteSpace(definition.AnimationSet.DefaultClipId))
        {
            throw new RegistryValidationException($"Character '{definition.Id}' needs at least one animation clip reference.");
        }

        foreach (var clipId in definition.AnimationSet.StateClips.Values)
        {
            RequireText(clipId, "animation clip id");
        }
    }

    private static void RequireText(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new RegistryValidationException($"Character definition field '{name}' is required.");
        }
    }
}
