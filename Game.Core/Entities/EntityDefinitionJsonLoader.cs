using System.Text.Json;
using System.Text.Json.Serialization;

namespace Game.Core.Entities;

public sealed class EntityDefinitionJsonLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public EntityDefinitionRegistry LoadRegistryFromDirectory(string directoryPath)
    {
        return EntityDefinitionRegistry.Create(LoadDefinitionsFromDirectory(directoryPath));
    }

    public IReadOnlyList<EntityDefinition> LoadDefinitionsFromDirectory(string directoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);

        if (!Directory.Exists(directoryPath))
        {
            return Array.Empty<EntityDefinition>();
        }

        return Directory
            .EnumerateFiles(directoryPath, "*.json", SearchOption.AllDirectories)
            .Order(StringComparer.OrdinalIgnoreCase)
            .Select(LoadDefinitionFromFile)
            .ToArray();
    }

    public EntityDefinition LoadDefinitionFromFile(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        using var stream = File.OpenRead(filePath);
        return LoadDefinition(stream, filePath);
    }

    public EntityDefinition LoadDefinitionFromJson(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
        return LoadDefinition(stream, "inline json");
    }

    private static EntityDefinition LoadDefinition(Stream stream, string source)
    {
        var dto = JsonSerializer.Deserialize<EntityDefinitionDto>(stream, Options);
        if (dto is null)
        {
            throw new JsonException($"Entity definition was empty: {source}");
        }

        return dto.ToDefinition();
    }

    private sealed record EntityDefinitionDto
    {
        public string? Id { get; init; }

        public string? DisplayName { get; init; }

        public string? TexturePath { get; init; }

        [JsonPropertyName("texture")]
        public string? Texture { get; init; }

        public int MaxHealth { get; init; }

        public float Width { get; init; } = 16;

        public float Height { get; init; } = 16;

        public string? AiBehavior { get; init; }

        public string? LootTableId { get; init; }

        [JsonPropertyName("lootTable")]
        public string? LootTable { get; init; }

        public EntityDefinition ToDefinition()
        {
            return new EntityDefinition
            {
                Id = Id ?? string.Empty,
                DisplayName = DisplayName ?? string.Empty,
                TexturePath = TexturePath ?? Texture ?? string.Empty,
                MaxHealth = MaxHealth,
                Width = Width,
                Height = Height,
                AiBehavior = AiBehavior,
                LootTableId = LootTableId ?? LootTable
            };
        }
    }
}
