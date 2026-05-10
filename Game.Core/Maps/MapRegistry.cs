using Game.Core.Data;

namespace Game.Core.Maps;

public sealed class MapRegistry
{
    private readonly Dictionary<string, MapDefinition> _byId;

    private MapRegistry(IEnumerable<MapDefinition> definitions)
    {
        _byId = new Dictionary<string, MapDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var definition in definitions)
        {
            AddValidated(definition);
        }
    }

    public IReadOnlyCollection<MapDefinition> Definitions => _byId.Values;

    public static MapRegistry Create(IEnumerable<MapDefinition> definitions)
    {
        return new MapRegistry(definitions);
    }

    public bool TryGetById(string id, out MapDefinition definition)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return _byId.TryGetValue(id, out definition!);
    }

    public MapDefinition GetById(string id)
    {
        return TryGetById(id, out var definition)
            ? definition
            : throw new KeyNotFoundException($"Map definition '{id}' was not registered.");
    }

    private void AddValidated(MapDefinition definition)
    {
        Validate(definition);
        if (_byId.ContainsKey(definition.Id))
        {
            throw new RegistryValidationException($"Duplicate map id '{definition.Id}'.");
        }

        _byId.Add(definition.Id, definition);
    }

    private static void Validate(MapDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        RequireText(definition.Id, nameof(definition.Id));
        RequireText(definition.DisplayName, nameof(definition.DisplayName));

        if (definition.WidthTiles <= 0 || definition.HeightTiles <= 0)
        {
            throw new RegistryValidationException($"Map '{definition.Id}' must have positive dimensions.");
        }

        if (definition.TileSize <= 0)
        {
            throw new RegistryValidationException($"Map '{definition.Id}' must have a positive tile size.");
        }

        ValidateLayers(definition);
        ValidateObjects(definition);
        ValidateSpawns(definition);
    }

    private static void ValidateLayers(MapDefinition definition)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var layer in definition.Layers)
        {
            RequireText(layer.Id, nameof(layer.Id));
            if (!ids.Add(layer.Id))
            {
                throw new RegistryValidationException($"Map '{definition.Id}' has duplicate layer id '{layer.Id}'.");
            }

            if (layer.Width != definition.WidthTiles || layer.Height != definition.HeightTiles)
            {
                throw new RegistryValidationException($"Map '{definition.Id}' layer '{layer.Id}' dimensions must match the map.");
            }

            if (layer.Tiles.Count != layer.Width * layer.Height)
            {
                throw new RegistryValidationException($"Map '{definition.Id}' layer '{layer.Id}' tile data length is invalid.");
            }
        }
    }

    private static void ValidateObjects(MapDefinition definition)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var mapObject in definition.Objects)
        {
            RequireText(mapObject.Id, nameof(mapObject.Id));
            if (!ids.Add(mapObject.Id))
            {
                throw new RegistryValidationException($"Map '{definition.Id}' has duplicate object id '{mapObject.Id}'.");
            }

            if (mapObject.Width <= 0 || mapObject.Height <= 0)
            {
                throw new RegistryValidationException($"Map '{definition.Id}' object '{mapObject.Id}' has invalid dimensions.");
            }

            if (!definition.Bounds.Contains(mapObject.TileX, mapObject.TileY) ||
                mapObject.TileX + mapObject.Width > definition.WidthTiles ||
                mapObject.TileY + mapObject.Height > definition.HeightTiles)
            {
                throw new RegistryValidationException($"Map '{definition.Id}' object '{mapObject.Id}' is outside the map.");
            }
        }
    }

    private static void ValidateSpawns(MapDefinition definition)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var spawn in definition.SpawnPoints)
        {
            RequireText(spawn.Id, nameof(spawn.Id));
            if (!ids.Add(spawn.Id))
            {
                throw new RegistryValidationException($"Map '{definition.Id}' has duplicate spawn id '{spawn.Id}'.");
            }

            if (!definition.Bounds.Contains(spawn.TileX, spawn.TileY))
            {
                throw new RegistryValidationException($"Map '{definition.Id}' spawn '{spawn.Id}' is outside the map.");
            }
        }
    }

    private static void RequireText(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new RegistryValidationException($"Map definition field '{name}' is required.");
        }
    }
}
