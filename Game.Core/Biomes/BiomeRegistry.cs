using Game.Core.Data;

namespace Game.Core.Biomes;

public sealed class BiomeRegistry
{
    private readonly Dictionary<string, BiomeDefinition> _byId;

    private BiomeRegistry(IEnumerable<BiomeDefinition> definitions)
    {
        _byId = new Dictionary<string, BiomeDefinition>(StringComparer.OrdinalIgnoreCase);

        foreach (var definition in definitions)
        {
            AddValidated(definition);
        }
    }

    public IReadOnlyCollection<BiomeDefinition> Definitions => _byId.Values;

    public static BiomeRegistry Create(IEnumerable<BiomeDefinition> definitions)
    {
        return new BiomeRegistry(definitions);
    }

    public BiomeDefinition GetById(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        if (!_byId.TryGetValue(id, out var biome))
        {
            throw new KeyNotFoundException($"Biome '{id}' was not registered.");
        }

        return biome;
    }

    public bool TryGetById(string id, out BiomeDefinition definition)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return _byId.TryGetValue(id, out definition!);
    }

    private void AddValidated(BiomeDefinition definition)
    {
        Validate(definition);

        if (_byId.ContainsKey(definition.Id))
        {
            throw new RegistryValidationException($"Duplicate biome id '{definition.Id}'.");
        }

        _byId.Add(definition.Id, definition);
    }

    private static void Validate(BiomeDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        RequireText(definition.Id, nameof(definition.Id));
        RequireText(definition.DisplayName, nameof(definition.DisplayName));
        RequireText(definition.SurfaceTile, nameof(definition.SurfaceTile));
        RequireText(definition.UndergroundTile, nameof(definition.UndergroundTile));
    }

    private static void RequireText(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new RegistryValidationException($"Biome definition field '{name}' is required.");
        }
    }
}
