using System.Text.Json;
using System.Text.Json.Serialization;

namespace Game.Core.Items;

public sealed class ItemDefinitionJsonLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public ItemRegistry LoadRegistryFromDirectory(string directoryPath)
    {
        return ItemRegistry.Create(LoadDefinitionsFromDirectory(directoryPath));
    }

    public IReadOnlyList<ItemDefinition> LoadDefinitionsFromDirectory(string directoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);

        if (!Directory.Exists(directoryPath))
        {
            return Array.Empty<ItemDefinition>();
        }

        return Directory
            .EnumerateFiles(directoryPath, "*.json", SearchOption.AllDirectories)
            .Order(StringComparer.OrdinalIgnoreCase)
            .Select(LoadDefinitionFromFile)
            .ToArray();
    }

    public ItemDefinition LoadDefinitionFromFile(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        using var stream = File.OpenRead(filePath);
        return LoadDefinition(stream, filePath);
    }

    public ItemDefinition LoadDefinitionFromJson(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
        return LoadDefinition(stream, "inline json");
    }

    private static ItemDefinition LoadDefinition(Stream stream, string source)
    {
        var definition = JsonSerializer.Deserialize<ItemDefinitionDto>(stream, Options);
        if (definition is null)
        {
            throw new JsonException($"Item definition was empty: {source}");
        }

        return definition.ToDefinition();
    }

    private sealed record ItemDefinitionDto
    {
        public string? Id { get; init; }

        public string? DisplayName { get; init; }

        public ItemType Type { get; init; }

        public string? TexturePath { get; init; }

        [JsonPropertyName("texture")]
        public string? Texture { get; init; }

        public int MaxStack { get; init; } = 1;

        public float UseTime { get; init; }

        public int Damage { get; init; }

        public int ToolPower { get; init; }

        public float Knockback { get; init; }

        public string? PlacesTileId { get; init; }

        [JsonPropertyName("placesTile")]
        public string? PlacesTile { get; init; }

        public ItemDefinition ToDefinition()
        {
            return new ItemDefinition
            {
                Id = Id ?? string.Empty,
                DisplayName = DisplayName ?? string.Empty,
                Type = Type,
                TexturePath = TexturePath ?? Texture ?? string.Empty,
                MaxStack = MaxStack,
                UseTime = UseTime,
                Damage = Damage,
                ToolPower = ToolPower,
                Knockback = Knockback,
                PlacesTileId = PlacesTileId ?? PlacesTile
            };
        }
    }
}
