using Game.Core.Data;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Game.Core.Tiles;

public sealed class TileDefinitionJsonLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public TileRegistry LoadRegistryFromDirectory(string directoryPath)
    {
        return TileRegistry.Create(LoadDefinitionsFromDirectory(directoryPath));
    }

    public IReadOnlyList<TileDefinition> LoadDefinitionsFromDirectory(string directoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);

        if (!Directory.Exists(directoryPath))
        {
            return Array.Empty<TileDefinition>();
        }

        return Directory
            .EnumerateFiles(directoryPath, "*.json", SearchOption.AllDirectories)
            .Order(StringComparer.OrdinalIgnoreCase)
            .Select(LoadDefinitionFromFile)
            .ToArray();
    }

    public TileDefinition LoadDefinitionFromFile(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        using var stream = File.OpenRead(filePath);
        return LoadDefinition(stream, filePath);
    }

    public TileDefinition LoadDefinitionFromJson(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
        return LoadDefinition(stream, "inline json");
    }

    private static TileDefinition LoadDefinition(Stream stream, string source)
    {
        var definition = JsonSerializer.Deserialize<TileDefinitionDto>(stream, Options);
        if (definition is null)
        {
            throw new JsonException($"Tile definition was empty: {source}");
        }

        return definition.ToDefinition();
    }

    private sealed record TileDefinitionDto
    {
        public ushort NumericId { get; init; }

        public string? Id { get; init; }

        public string? DisplayName { get; init; }

        public string? TexturePath { get; init; }

        [JsonPropertyName("texture")]
        public string? Texture { get; init; }

        public bool Solid { get; init; }

        public bool BlocksLight { get; init; }

        public byte EmittedLight { get; init; }

        public int LightRadius { get; init; }

        public float Hardness { get; init; }

        public int MiningPowerRequired { get; init; }

        public string? DropItemId { get; init; }

        [JsonPropertyName("dropItem")]
        public string? DropItem { get; init; }

        public string? MergeGroup { get; init; }

        public string? CraftingStationId { get; init; }

        [JsonPropertyName("craftingStation")]
        public string? CraftingStation { get; init; }

        public string[] Tags { get; init; } = Array.Empty<string>();

        public TileDefinition ToDefinition()
        {
            return new TileDefinition
            {
                NumericId = NumericId,
                Id = Id ?? string.Empty,
                DisplayName = DisplayName ?? string.Empty,
                TexturePath = TexturePath ?? Texture ?? string.Empty,
                Solid = Solid,
                BlocksLight = BlocksLight,
                EmittedLight = EmittedLight,
                LightRadius = LightRadius,
                Hardness = Hardness,
                MiningPowerRequired = MiningPowerRequired,
                DropItemId = DropItemId ?? DropItem,
                MergeGroup = MergeGroup,
                CraftingStationId = CraftingStationId ?? CraftingStation,
                Tags = DefinitionTags.Normalize(Tags)
            };
        }
    }
}
