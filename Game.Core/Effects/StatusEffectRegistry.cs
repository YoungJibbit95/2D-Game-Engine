using Game.Core.Data;

namespace Game.Core.Effects;

public sealed class StatusEffectRegistry
{
    private readonly Dictionary<string, StatusEffectDefinition> _byId;

    private StatusEffectRegistry(IEnumerable<StatusEffectDefinition> definitions)
    {
        _byId = new Dictionary<string, StatusEffectDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var definition in definitions)
        {
            AddValidated(definition);
        }
    }

    public IReadOnlyCollection<StatusEffectDefinition> Definitions => _byId.Values;

    public static StatusEffectRegistry Create(IEnumerable<StatusEffectDefinition> definitions)
    {
        return new StatusEffectRegistry(definitions);
    }

    public StatusEffectDefinition GetById(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return _byId.TryGetValue(id, out var definition)
            ? definition
            : throw new KeyNotFoundException($"Status effect '{id}' is not registered.");
    }

    public bool TryGetById(string id, out StatusEffectDefinition definition)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return _byId.TryGetValue(id, out definition!);
    }

    private void AddValidated(StatusEffectDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        RequireText(definition.Id, nameof(definition.Id));
        RequireText(definition.DisplayName, nameof(definition.DisplayName));

        if (definition.DurationSeconds <= 0)
        {
            throw new RegistryValidationException($"Status effect '{definition.Id}' must have a positive duration.");
        }

        if (definition.TickIntervalSeconds < 0)
        {
            throw new RegistryValidationException($"Status effect '{definition.Id}' has a negative tick interval.");
        }

        if (!_byId.TryAdd(definition.Id, definition))
        {
            throw new RegistryValidationException($"Duplicate status effect id '{definition.Id}'.");
        }
    }

    private static void RequireText(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new RegistryValidationException($"Status effect definition field '{name}' is required.");
        }
    }
}
