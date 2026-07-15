using System.Text.Json;

namespace Game.Core.Audio;

public sealed class SoundscapeDefinitionJsonLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public SoundscapeCatalog LoadCatalogFromDirectory(string directoryPath)
    {
        var catalog = new SoundscapeCatalog();
        foreach (var definition in LoadDefinitionsFromDirectory(directoryPath))
        {
            catalog.Register(definition);
        }

        return catalog;
    }

    public IReadOnlyList<SoundscapeDefinition> LoadDefinitionsFromDirectory(string directoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        if (!Directory.Exists(directoryPath))
        {
            return Array.Empty<SoundscapeDefinition>();
        }

        return Directory
            .EnumerateFiles(directoryPath, "*.json", SearchOption.AllDirectories)
            .Order(StringComparer.OrdinalIgnoreCase)
            .Select(LoadDefinitionFromFile)
            .ToArray();
    }

    public SoundscapeDefinition LoadDefinitionFromFile(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        using var stream = File.OpenRead(filePath);
        return LoadDefinition(stream, filePath);
    }

    public SoundscapeDefinition LoadDefinitionFromJson(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
        return LoadDefinition(stream, "inline json");
    }

    private static SoundscapeDefinition LoadDefinition(Stream stream, string source)
    {
        var definition = JsonSerializer.Deserialize<SoundscapeDefinition>(stream, Options);
        if (definition is null)
        {
            throw new JsonException($"Soundscape definition was empty: {source}");
        }

        SoundscapeDefinition.Validate(definition);
        return definition;
    }
}
