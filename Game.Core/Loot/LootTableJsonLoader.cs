using System.Text.Json;
using System.Text.Json.Serialization;

namespace Game.Core.Loot;

public sealed class LootTableJsonLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public LootTableRegistry LoadRegistryFromDirectory(string directoryPath)
    {
        return LootTableRegistry.Create(LoadDefinitionsFromDirectory(directoryPath));
    }

    public IReadOnlyList<LootTableDefinition> LoadDefinitionsFromDirectory(string directoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);

        if (!Directory.Exists(directoryPath))
        {
            return Array.Empty<LootTableDefinition>();
        }

        return Directory
            .EnumerateFiles(directoryPath, "*.json", SearchOption.AllDirectories)
            .Order(StringComparer.OrdinalIgnoreCase)
            .Select(LoadDefinitionFromFile)
            .ToArray();
    }

    public LootTableDefinition LoadDefinitionFromFile(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        using var stream = File.OpenRead(filePath);
        return LoadDefinition(stream, filePath);
    }

    public LootTableDefinition LoadDefinitionFromJson(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
        return LoadDefinition(stream, "inline json");
    }

    private static LootTableDefinition LoadDefinition(Stream stream, string source)
    {
        var definition = JsonSerializer.Deserialize<LootTableDefinition>(stream, Options);
        if (definition is null)
        {
            throw new JsonException($"Loot table definition was empty: {source}");
        }

        return definition;
    }
}
