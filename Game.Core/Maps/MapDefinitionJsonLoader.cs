using Game.Core.Data;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Game.Core.Maps;

public sealed class MapDefinitionJsonLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public MapRegistry LoadRegistryFromDirectory(string directoryPath)
    {
        return MapRegistry.Create(LoadDefinitionsFromDirectory(directoryPath));
    }

    public IReadOnlyList<MapDefinition> LoadDefinitionsFromDirectory(string directoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);

        if (!Directory.Exists(directoryPath))
        {
            return Array.Empty<MapDefinition>();
        }

        return Directory
            .EnumerateFiles(directoryPath, "*.json", SearchOption.AllDirectories)
            .Order(StringComparer.OrdinalIgnoreCase)
            .Select(LoadDefinitionFromFile)
            .ToArray();
    }

    public MapDefinition LoadDefinitionFromFile(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        return LoadDefinitionFromJson(File.ReadAllText(filePath), filePath);
    }

    public MapDefinition LoadDefinitionFromJson(string json)
    {
        return LoadDefinitionFromJson(json, "inline json");
    }

    private static MapDefinition LoadDefinitionFromJson(string json, string source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        var dto = JsonSerializer.Deserialize<MapDefinitionDto>(json, Options);
        if (dto is null)
        {
            throw new JsonException($"Map definition was empty: {source}");
        }

        return dto.ToDefinition();
    }

    private sealed record MapDefinitionDto
    {
        public string? Id { get; init; }

        public string? DisplayName { get; init; }

        public int WidthTiles { get; init; }

        public int HeightTiles { get; init; }

        public int TileSize { get; init; } = GameConstants.TileSize;

        public List<MapTileLayerDto> Layers { get; init; } = new();

        public List<MapObjectDto> Objects { get; init; } = new();

        public List<MapSpawnPointDto> SpawnPoints { get; init; } = new();

        public string[] Tags { get; init; } = Array.Empty<string>();

        public MapDefinition ToDefinition()
        {
            return new MapDefinition
            {
                Id = Id ?? string.Empty,
                DisplayName = DisplayName ?? string.Empty,
                WidthTiles = WidthTiles,
                HeightTiles = HeightTiles,
                TileSize = TileSize,
                Layers = Layers.Select(layer => layer.ToDefinition()).ToArray(),
                Objects = Objects.Select(mapObject => mapObject.ToDefinition()).ToArray(),
                SpawnPoints = SpawnPoints.Select(spawn => spawn.ToDefinition()).ToArray(),
                Tags = DefinitionTags.Normalize(Tags)
            };
        }
    }

    private sealed record MapTileLayerDto
    {
        public string? Id { get; init; }

        public MapLayerKind Kind { get; init; }

        public int Width { get; init; }

        public int Height { get; init; }

        public int ZIndex { get; init; }

        public bool IsVisible { get; init; } = true;

        public bool BlocksMovement { get; init; }

        public int[] Tiles { get; init; } = Array.Empty<int>();

        public MapTileLayerDefinition ToDefinition()
        {
            return new MapTileLayerDefinition
            {
                Id = Id ?? string.Empty,
                Kind = Kind,
                Width = Width,
                Height = Height,
                ZIndex = ZIndex,
                IsVisible = IsVisible,
                BlocksMovement = BlocksMovement,
                Tiles = Tiles
            };
        }
    }

    private sealed record MapObjectDto
    {
        public string? Id { get; init; }

        public MapObjectKind Kind { get; init; }

        public int TileX { get; init; }

        public int TileY { get; init; }

        public int Width { get; init; } = 1;

        public int Height { get; init; } = 1;

        public bool BlocksMovement { get; init; }

        public bool IsInteractable { get; init; }

        public string? InteractionId { get; init; }

        public string? TargetMapId { get; init; }

        public string? TargetSpawnId { get; init; }

        public Dictionary<string, string> Properties { get; init; } = new(StringComparer.OrdinalIgnoreCase);

        public MapObjectDefinition ToDefinition()
        {
            return new MapObjectDefinition
            {
                Id = Id ?? string.Empty,
                Kind = Kind,
                TileX = TileX,
                TileY = TileY,
                Width = Width,
                Height = Height,
                BlocksMovement = BlocksMovement,
                IsInteractable = IsInteractable,
                InteractionId = InteractionId,
                TargetMapId = TargetMapId,
                TargetSpawnId = TargetSpawnId,
                Properties = new Dictionary<string, string>(Properties, StringComparer.OrdinalIgnoreCase)
            };
        }
    }

    private sealed record MapSpawnPointDto
    {
        public string? Id { get; init; }

        public int TileX { get; init; }

        public int TileY { get; init; }

        public string Facing { get; init; } = "down";

        public MapSpawnPointDefinition ToDefinition()
        {
            return new MapSpawnPointDefinition
            {
                Id = Id ?? string.Empty,
                TileX = TileX,
                TileY = TileY,
                Facing = Facing
            };
        }
    }
}
