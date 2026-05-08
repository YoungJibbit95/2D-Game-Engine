using Game.Core.Data;

namespace Game.Core.Tiles;

public sealed class TileRegistry
{
    private readonly Dictionary<ushort, TileDefinition> _byNumericId;
    private readonly Dictionary<string, TileDefinition> _byId;

    private TileRegistry(IEnumerable<TileDefinition> definitions, TileDefinition fallback)
    {
        Fallback = fallback;
        _byNumericId = new Dictionary<ushort, TileDefinition>();
        _byId = new Dictionary<string, TileDefinition>(StringComparer.OrdinalIgnoreCase);

        foreach (var definition in definitions)
        {
            AddValidated(definition);
        }

        if (!_byNumericId.ContainsKey(Fallback.NumericId))
        {
            AddValidated(Fallback);
        }
    }

    public TileDefinition Fallback { get; }

    public IReadOnlyCollection<TileDefinition> Definitions => _byNumericId.Values;

    public static TileRegistry Create(IEnumerable<TileDefinition> definitions)
    {
        return new TileRegistry(definitions, CreateFallbackDefinition());
    }

    public TileDefinition GetByNumericId(ushort numericId)
    {
        return _byNumericId.GetValueOrDefault(numericId, Fallback);
    }

    public TileDefinition GetById(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return _byId.GetValueOrDefault(id, Fallback);
    }

    public bool TryGetById(string id, out TileDefinition definition)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return _byId.TryGetValue(id, out definition!);
    }

    public bool TryGetByNumericId(ushort numericId, out TileDefinition definition)
    {
        return _byNumericId.TryGetValue(numericId, out definition!);
    }

    private void AddValidated(TileDefinition definition)
    {
        Validate(definition);

        if (_byNumericId.ContainsKey(definition.NumericId))
        {
            throw new RegistryValidationException($"Duplicate tile numeric id {definition.NumericId}.");
        }

        if (_byId.ContainsKey(definition.Id))
        {
            throw new RegistryValidationException($"Duplicate tile id '{definition.Id}'.");
        }

        _byNumericId.Add(definition.NumericId, definition);
        _byId.Add(definition.Id, definition);
    }

    private static void Validate(TileDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        RequireText(definition.Id, nameof(definition.Id));
        RequireText(definition.DisplayName, nameof(definition.DisplayName));
        RequireText(definition.TexturePath, nameof(definition.TexturePath));

        if (definition.Hardness < 0)
        {
            throw new RegistryValidationException($"Tile '{definition.Id}' has negative hardness.");
        }

        if (definition.MiningPowerRequired < 0)
        {
            throw new RegistryValidationException($"Tile '{definition.Id}' has negative mining power requirement.");
        }
    }

    private static void RequireText(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new RegistryValidationException($"Tile definition field '{name}' is required.");
        }
    }

    private static TileDefinition CreateFallbackDefinition()
    {
        return new TileDefinition
        {
            NumericId = ushort.MaxValue,
            Id = "missing_tile",
            DisplayName = "Missing Tile",
            TexturePath = "tiles/missing",
            Solid = false,
            BlocksLight = false,
            Hardness = 0,
            MiningPowerRequired = 0
        };
    }
}
