using Game.Core.Data;

namespace Game.Core.Entities;

public sealed class EntityDefinitionRegistry
{
    private readonly Dictionary<string, EntityDefinition> _byId;

    private EntityDefinitionRegistry(IEnumerable<EntityDefinition> definitions)
    {
        _byId = new Dictionary<string, EntityDefinition>(StringComparer.OrdinalIgnoreCase);

        foreach (var definition in definitions)
        {
            AddValidated(definition);
        }
    }

    public IReadOnlyCollection<EntityDefinition> Definitions => _byId.Values;

    public static EntityDefinitionRegistry Create(IEnumerable<EntityDefinition> definitions)
    {
        return new EntityDefinitionRegistry(definitions);
    }

    public EntityDefinition GetById(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        if (!_byId.TryGetValue(id, out var definition))
        {
            throw new KeyNotFoundException($"Entity definition '{id}' was not registered.");
        }

        return definition;
    }

    public bool TryGetById(string id, out EntityDefinition definition)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return _byId.TryGetValue(id, out definition!);
    }

    private void AddValidated(EntityDefinition definition)
    {
        Validate(definition);

        if (_byId.ContainsKey(definition.Id))
        {
            throw new RegistryValidationException($"Duplicate entity definition id '{definition.Id}'.");
        }

        _byId.Add(definition.Id, definition);
    }

    private static void Validate(EntityDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        RequireText(definition.Id, nameof(definition.Id));
        RequireText(definition.DisplayName, nameof(definition.DisplayName));
        RequireText(definition.TexturePath, nameof(definition.TexturePath));

        if (definition.MaxHealth <= 0)
        {
            throw new RegistryValidationException($"Entity '{definition.Id}' must have positive max health.");
        }

        if (definition.Width <= 0 || definition.Height <= 0)
        {
            throw new RegistryValidationException($"Entity '{definition.Id}' must have positive size.");
        }
    }

    private static void RequireText(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new RegistryValidationException($"Entity definition field '{name}' is required.");
        }
    }
}
