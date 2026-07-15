using System.Text.Json;

using Game.Core.WorldEvents;
namespace Game.Core.World.Generation;

public sealed class RegionalGenerationProfileJsonLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public IReadOnlyList<RegionalGenerationProfile> LoadProfilesFromDirectory(string directoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        if (!Directory.Exists(directoryPath))
        {
            return Array.Empty<RegionalGenerationProfile>();
        }

        return Directory
            .EnumerateFiles(directoryPath, "*.region.json", SearchOption.AllDirectories)
            .Order(StringComparer.OrdinalIgnoreCase)
            .Select(LoadProfileFromFile)
            .ToArray();
    }

    public RegionalGenerationProfile LoadProfileFromFile(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        using var stream = File.OpenRead(filePath);
        return LoadProfile(stream, filePath);
    }

    public RegionalGenerationProfile LoadProfileFromJson(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
        return LoadProfile(stream, "inline json");
    }

    private static RegionalGenerationProfile LoadProfile(Stream stream, string source)
    {
        var profile = JsonSerializer.Deserialize<RegionalGenerationProfile>(stream, Options)
            ?? throw new JsonException($"Regional generation profile was empty: {source}");
        Validate(profile);
        return profile;
    }

    private static void Validate(RegionalGenerationProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.Id))
        {
            throw new InvalidDataException("Regional generation profile id is required.");
        }

        if (profile.RegionWidthTiles < 32 || profile.BiomeSpanRegions <= 0 ||
            profile.WorldHeightTiles <= 0 || profile.CaveRegionAttempts < 0)
        {
            throw new InvalidDataException($"Regional generation profile '{profile.Id}' has invalid dimensions or counts.");
        }

        if (profile.CaveMinDepth < 0 || profile.CaveMaxDepth < profile.CaveMinDepth ||
            profile.CaveMinRadiusX <= 0 || profile.CaveMaxRadiusX < profile.CaveMinRadiusX ||
            profile.CaveMinRadiusY <= 0 || profile.CaveMaxRadiusY < profile.CaveMinRadiusY)
        {
            throw new InvalidDataException($"Regional generation profile '{profile.Id}' has an invalid cave range.");
        }

        foreach (var feature in profile.Features)
        {
            if (string.IsNullOrWhiteSpace(feature.Id) || string.IsNullOrWhiteSpace(feature.Kind) ||
                feature.ChancePerRegion is < 0f or > 1f || feature.MinCount < 0 ||
                feature.MaxCount < feature.MinCount || feature.MinSpacingTiles < 0 ||
                feature.MaxTileY < feature.MinTileY)
            {
                throw new InvalidDataException($"Regional feature '{feature.Id}' is invalid.");
            }
        }

        var layerIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var layer in profile.BiomeLayers)
        {
            if (string.IsNullOrWhiteSpace(layer.Id) || !layerIds.Add(layer.Id) ||
                layer.MinTileY < 0 || layer.MaxTileYInclusive < layer.MinTileY ||
                layer.MaxTileYInclusive >= profile.WorldHeightTiles || layer.BiomeIds.Count == 0 ||
                layer.BiomeIds.Any(string.IsNullOrWhiteSpace) ||
                layer.BiomeIds.Distinct(StringComparer.OrdinalIgnoreCase).Count() != layer.BiomeIds.Count)
            {
                throw new InvalidDataException($"Regional biome layer '{layer.Id}' is invalid.");
            }
        }

        foreach (var definition in profile.WorldEvents)
        {
            WorldEventDefinition.Validate(definition);
        }
    }
}
