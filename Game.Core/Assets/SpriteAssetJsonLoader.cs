using Game.Core.Data;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Game.Core.Assets;

public sealed class SpriteAssetJsonLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public SpriteAssetRegistry LoadRegistryFromDirectory(string directoryPath)
    {
        return SpriteAssetRegistry.Create(LoadDefinitionsFromDirectory(directoryPath));
    }

    public IReadOnlyList<SpriteAssetDefinition> LoadDefinitionsFromDirectory(string directoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);

        if (!Directory.Exists(directoryPath))
        {
            return Array.Empty<SpriteAssetDefinition>();
        }

        return Directory
            .EnumerateFiles(directoryPath, "*.json", SearchOption.AllDirectories)
            .Order(StringComparer.OrdinalIgnoreCase)
            .SelectMany(LoadDefinitionsFromFile)
            .ToArray();
    }

    public IReadOnlyList<SpriteAssetDefinition> LoadDefinitionsFromFile(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        return LoadDefinitionsFromJson(File.ReadAllText(filePath), filePath);
    }

    public IReadOnlyList<SpriteAssetDefinition> LoadDefinitionsFromJson(string json)
    {
        return LoadDefinitionsFromJson(json, "inline json");
    }

    private static IReadOnlyList<SpriteAssetDefinition> LoadDefinitionsFromJson(string json, string source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        var manifest = JsonSerializer.Deserialize<SpriteAssetManifestDto>(json, Options);
        if (manifest?.Sprites is { Count: > 0 })
        {
            return manifest.Sprites.Select(sprite => sprite.ToDefinition()).ToArray();
        }

        var single = JsonSerializer.Deserialize<SpriteAssetDefinitionDto>(json, Options);
        if (single is null)
        {
            throw new JsonException($"Sprite asset definition was empty: {source}");
        }

        return new[] { single.ToDefinition() };
    }

    private sealed record SpriteAssetManifestDto
    {
        public List<SpriteAssetDefinitionDto> Sprites { get; init; } = new();
    }

    private sealed record SpriteAssetDefinitionDto
    {
        public string? Id { get; init; }

        public string? Path { get; init; }

        public SpriteAssetCategory Category { get; init; }

        public int Width { get; init; } = 16;

        public int Height { get; init; } = 16;

        public int PixelsPerUnit { get; init; } = 16;

        public string? AtlasId { get; init; }

        public string? SourceAliasOf { get; init; }

        public int? OriginX { get; init; }

        public int? OriginY { get; init; }

        public string? RenderLayer { get; init; }

        public string? License { get; init; }

        public string? Provenance { get; init; }

        public List<SpriteFrameDefinition> Frames { get; init; } = new();

        public string[] Tags { get; init; } = Array.Empty<string>();

        public SpriteAssetDefinition ToDefinition()
        {
            return new SpriteAssetDefinition
            {
                Id = Id ?? string.Empty,
                Path = Path ?? string.Empty,
                Category = Category,
                Width = Width,
                Height = Height,
                PixelsPerUnit = PixelsPerUnit,
                AtlasId = AtlasId,
                SourceAliasOf = NormalizeOptional(SourceAliasOf),
                OriginX = OriginX,
                OriginY = OriginY,
                RenderLayer = NormalizeOptional(RenderLayer),
                License = NormalizeOptional(License),
                Provenance = NormalizeOptional(Provenance),
                Frames = Frames.ToArray(),
                Tags = DefinitionTags.Normalize(Tags)
            };
        }

        private static string? NormalizeOptional(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }
}
