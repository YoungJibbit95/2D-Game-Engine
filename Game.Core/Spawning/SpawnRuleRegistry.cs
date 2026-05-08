using Game.Core.Data;

namespace Game.Core.Spawning;

public sealed class SpawnRuleRegistry
{
    private readonly Dictionary<string, SpawnRuleDefinition> _byId;

    private SpawnRuleRegistry(IEnumerable<SpawnRuleDefinition> definitions)
    {
        _byId = new Dictionary<string, SpawnRuleDefinition>(StringComparer.OrdinalIgnoreCase);

        foreach (var definition in definitions)
        {
            AddValidated(definition);
        }
    }

    public IReadOnlyCollection<SpawnRuleDefinition> Definitions => _byId.Values;

    public static SpawnRuleRegistry Create(IEnumerable<SpawnRuleDefinition> definitions)
    {
        return new SpawnRuleRegistry(definitions);
    }

    public bool TryGetById(string id, out SpawnRuleDefinition definition)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return _byId.TryGetValue(id, out definition!);
    }

    private void AddValidated(SpawnRuleDefinition definition)
    {
        Validate(definition);

        if (_byId.ContainsKey(definition.Id))
        {
            throw new RegistryValidationException($"Duplicate spawn rule id '{definition.Id}'.");
        }

        _byId.Add(definition.Id, definition);
    }

    private static void Validate(SpawnRuleDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (string.IsNullOrWhiteSpace(definition.Id))
        {
            throw new RegistryValidationException("Spawn rule id is required.");
        }

        if (string.IsNullOrWhiteSpace(definition.EntityId))
        {
            throw new RegistryValidationException($"Spawn rule '{definition.Id}' must specify an entity id.");
        }

        if (definition.Chance < 0 || definition.Chance > 1)
        {
            throw new RegistryValidationException($"Spawn rule '{definition.Id}' chance must be in the range 0..1.");
        }

        if (definition.MaxActive <= 0)
        {
            throw new RegistryValidationException($"Spawn rule '{definition.Id}' must allow at least one active entity.");
        }

        if (definition.MinTileY is not null && definition.MaxTileY is not null && definition.MinTileY > definition.MaxTileY)
        {
            throw new RegistryValidationException($"Spawn rule '{definition.Id}' has an invalid vertical range.");
        }
    }
}
