using System.Text.Json;

namespace Game.Core.Spawning;

public sealed class SpawnRuleJsonLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public SpawnRuleRegistry LoadRegistryFromDirectory(string directoryPath)
    {
        return SpawnRuleRegistry.Create(LoadDefinitionsFromDirectory(directoryPath));
    }

    public IReadOnlyList<SpawnRuleDefinition> LoadDefinitionsFromDirectory(string directoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);

        if (!Directory.Exists(directoryPath))
        {
            return Array.Empty<SpawnRuleDefinition>();
        }

        return Directory
            .EnumerateFiles(directoryPath, "*.json", SearchOption.AllDirectories)
            .Order(StringComparer.OrdinalIgnoreCase)
            .Select(LoadDefinitionFromFile)
            .ToArray();
    }

    public SpawnRuleDefinition LoadDefinitionFromFile(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        using var stream = File.OpenRead(filePath);
        return LoadDefinition(stream, filePath);
    }

    public SpawnRuleDefinition LoadDefinitionFromJson(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
        return LoadDefinition(stream, "inline json");
    }

    private static SpawnRuleDefinition LoadDefinition(Stream stream, string source)
    {
        var definition = JsonSerializer.Deserialize<SpawnRuleDefinition>(stream, Options);
        if (definition is null)
        {
            throw new JsonException($"Spawn rule definition was empty: {source}");
        }

        return definition;
    }
}
