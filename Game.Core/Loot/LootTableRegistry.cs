using Game.Core.Data;

namespace Game.Core.Loot;

public sealed class LootTableRegistry
{
    private readonly Dictionary<string, LootTableDefinition> _byId;

    private LootTableRegistry(IEnumerable<LootTableDefinition> definitions)
    {
        _byId = new Dictionary<string, LootTableDefinition>(StringComparer.OrdinalIgnoreCase);

        foreach (var definition in definitions)
        {
            AddValidated(definition);
        }
    }

    public IReadOnlyCollection<LootTableDefinition> Definitions => _byId.Values;

    public static LootTableRegistry Create(IEnumerable<LootTableDefinition> definitions)
    {
        return new LootTableRegistry(definitions);
    }

    public LootTableDefinition GetById(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        if (!_byId.TryGetValue(id, out var table))
        {
            throw new KeyNotFoundException($"Loot table '{id}' was not registered.");
        }

        return table;
    }

    public bool TryGetById(string id, out LootTableDefinition definition)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return _byId.TryGetValue(id, out definition!);
    }

    private void AddValidated(LootTableDefinition definition)
    {
        Validate(definition);

        if (_byId.ContainsKey(definition.Id))
        {
            throw new RegistryValidationException($"Duplicate loot table id '{definition.Id}'.");
        }

        _byId.Add(definition.Id, definition);
    }

    private static void Validate(LootTableDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (string.IsNullOrWhiteSpace(definition.Id))
        {
            throw new RegistryValidationException("Loot table id is required.");
        }

        foreach (var entry in definition.Entries)
        {
            if (string.IsNullOrWhiteSpace(entry.ItemId) || entry.Min <= 0 || entry.Max < entry.Min)
            {
                throw new RegistryValidationException($"Loot table '{definition.Id}' has an invalid entry.");
            }

            if (entry.Chance < 0 || entry.Chance > 1)
            {
                throw new RegistryValidationException($"Loot table '{definition.Id}' has a chance outside 0..1.");
            }
        }
    }
}
