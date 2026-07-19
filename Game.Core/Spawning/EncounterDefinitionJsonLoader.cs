using System.Text.Json;
using System.Text.Json.Serialization;

namespace Game.Core.Spawning;

public sealed class EncounterDefinitionJsonLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public EncounterDefinitionRegistry LoadRegistryFromDirectory(string directoryPath)
    {
        return EncounterDefinitionRegistry.Create(LoadDefinitionsFromDirectory(directoryPath));
    }

    public IReadOnlyList<EncounterDefinition> LoadDefinitionsFromDirectory(string directoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        if (!Directory.Exists(directoryPath))
        {
            return Array.Empty<EncounterDefinition>();
        }

        return Directory
            .EnumerateFiles(directoryPath, "*.json", SearchOption.AllDirectories)
            .Order(StringComparer.OrdinalIgnoreCase)
            .Select(LoadDefinitionFromFile)
            .ToArray();
    }

    public EncounterDefinition LoadDefinitionFromFile(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        using var stream = File.OpenRead(filePath);
        return LoadDefinition(stream, filePath);
    }

    public EncounterDefinition LoadDefinitionFromJson(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
        return LoadDefinition(stream, "inline json");
    }

    private static EncounterDefinition LoadDefinition(Stream stream, string source)
    {
        var definition = JsonSerializer.Deserialize<EncounterDefinition>(stream, Options);
        if (definition is null)
        {
            throw new JsonException($"Encounter definition was empty: {source}");
        }

        return definition;
    }
}
