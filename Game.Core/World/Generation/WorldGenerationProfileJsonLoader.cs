using System.Text.Json;

namespace Game.Core.World.Generation;

public sealed class WorldGenerationProfileJsonLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public IReadOnlyList<WorldGenerationProfile> LoadProfilesFromDirectory(string directoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        if (!Directory.Exists(directoryPath))
        {
            return Array.Empty<WorldGenerationProfile>();
        }

        return Directory
            .EnumerateFiles(directoryPath, "*.json", SearchOption.AllDirectories)
            .Order(StringComparer.OrdinalIgnoreCase)
            .Select(LoadProfileFromFile)
            .ToArray();
    }

    public WorldGenerationProfile LoadProfileFromFile(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        using var stream = File.OpenRead(filePath);
        return LoadProfile(stream, filePath);
    }

    public WorldGenerationProfile LoadProfileFromJson(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
        return LoadProfile(stream, "inline json");
    }

    private static WorldGenerationProfile LoadProfile(Stream stream, string source)
    {
        var profile = JsonSerializer.Deserialize<WorldGenerationProfile>(stream, Options);
        if (profile is null)
        {
            throw new JsonException($"World generation profile was empty: {source}");
        }

        if (string.IsNullOrWhiteSpace(profile.Id) || profile.WidthTiles <= 0 || profile.HeightTiles <= 0)
        {
            throw new InvalidDataException($"Invalid world generation profile: {source}");
        }

        return profile;
    }
}
