using System.Text.Json;

namespace Game.Core.World.Generation;

public sealed class StructurePlanDefinitionJsonLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public IReadOnlyList<StructurePlanDefinition> LoadDefinitionsFromDirectory(string directoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        if (!Directory.Exists(directoryPath))
        {
            return Array.Empty<StructurePlanDefinition>();
        }

        return Directory
            .EnumerateFiles(directoryPath, "*.json", SearchOption.AllDirectories)
            .Order(StringComparer.OrdinalIgnoreCase)
            .Select(LoadDefinitionFromFile)
            .ToArray();
    }

    public StructurePlanDefinition LoadDefinitionFromFile(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        using var stream = File.OpenRead(filePath);
        return LoadDefinition(stream, filePath);
    }

    public StructurePlanDefinition LoadDefinitionFromJson(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
        return LoadDefinition(stream, "inline json");
    }

    private static StructurePlanDefinition LoadDefinition(Stream stream, string source)
    {
        var definition = JsonSerializer.Deserialize<StructurePlanDefinition>(stream, Options)
            ?? throw new JsonException($"Structure planning definition was empty: {source}");
        if (string.IsNullOrWhiteSpace(definition.Id) || string.IsNullOrWhiteSpace(definition.TemplateId) ||
            string.IsNullOrWhiteSpace(definition.Placement) || definition.ChancePerRegion is < 0f or > 1f ||
            definition.MinSpacingRegions < 0 || definition.WidthTiles <= 0 || definition.HeightTiles <= 0 ||
            definition.MaxTileY < definition.MinTileY ||
            HasInvalidValues(definition.AllowedBiomeIds) ||
            HasInvalidValues(definition.AllowedSubBiomeIds) ||
            HasInvalidValues(definition.AllowedLayerIds) ||
            !HasValidTemplate(definition))
        {
            throw new InvalidDataException($"Structure planning definition '{definition.Id}' is invalid.");
        }

        return definition;
    }

    private static bool HasInvalidValues(IReadOnlyList<string> values)
    {
        return values.Any(string.IsNullOrWhiteSpace) ||
            values.Distinct(StringComparer.OrdinalIgnoreCase).Count() != values.Count;
    }

    private static bool HasValidTemplate(StructurePlanDefinition definition)
    {
        if (definition.Rows.Count == 0)
        {
            return definition.Legend.Count == 0;
        }

        if (definition.Rows.Count != definition.HeightTiles ||
            definition.TransparentSymbol.Length != 1 ||
            definition.Rows.Any(row => row.Length != definition.WidthTiles) ||
            definition.Legend.Any(entry =>
                entry.Key.Length != 1 ||
                string.IsNullOrWhiteSpace(entry.Value) ||
                entry.Key == definition.TransparentSymbol))
        {
            return false;
        }

        var transparent = definition.TransparentSymbol[0];
        foreach (var row in definition.Rows)
        {
            foreach (var symbol in row)
            {
                if (symbol != transparent && !definition.Legend.ContainsKey(symbol.ToString()))
                {
                    return false;
                }
            }
        }

        return true;
    }
}
