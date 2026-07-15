using System.Text.Json;
using System.Text.Json.Serialization;

namespace Game.Core.WorldEvents;

public sealed class WorldEventDefinitionJsonLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public WorldEventDefinitionRegistry LoadRegistryFromDirectory(string directoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        if (!Directory.Exists(directoryPath))
        {
            return WorldEventDefinitionRegistry.Create(Array.Empty<WorldEventDefinition>());
        }

        var definitions = Directory
            .EnumerateFiles(directoryPath, "*.json", SearchOption.AllDirectories)
            .Order(StringComparer.OrdinalIgnoreCase)
            .Select(LoadFromFile)
            .ToArray();
        return WorldEventDefinitionRegistry.Create(definitions);
    }

    public WorldEventDefinition LoadFromFile(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        using var stream = File.OpenRead(filePath);
        return Load(stream, filePath);
    }

    public WorldEventDefinition LoadFromJson(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
        return Load(stream, "inline JSON");
    }

    private static WorldEventDefinition Load(Stream stream, string source)
    {
        try
        {
            var definition = JsonSerializer.Deserialize<WorldEventDefinition>(stream, Options)
                ?? throw new JsonException("Definition was empty.");
            WorldEventDefinition.Validate(definition);
            return definition;
        }
        catch (Exception exception) when (exception is JsonException or InvalidDataException)
        {
            throw new InvalidDataException($"Failed to load world event from {source}: {exception.Message}", exception);
        }
    }
}
