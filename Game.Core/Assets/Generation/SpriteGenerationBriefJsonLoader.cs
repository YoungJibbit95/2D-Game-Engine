using Game.Core.Data;
using System.Text.Json;

namespace Game.Core.Assets.Generation;

public sealed class SpriteGenerationBriefJsonLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public IReadOnlyList<SpriteGenerationBrief> LoadBriefsFromDirectory(string directoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        if (!Directory.Exists(directoryPath))
        {
            return Array.Empty<SpriteGenerationBrief>();
        }

        return Directory
            .EnumerateFiles(directoryPath, "*.json", SearchOption.AllDirectories)
            .Order(StringComparer.OrdinalIgnoreCase)
            .SelectMany(LoadBriefsFromFile)
            .ToArray();
    }

    public IReadOnlyList<SpriteGenerationBrief> LoadBriefsFromFile(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        return LoadBriefsFromJson(File.ReadAllText(filePath), filePath);
    }

    public IReadOnlyList<SpriteGenerationBrief> LoadBriefsFromJson(string json)
    {
        return LoadBriefsFromJson(json, "inline json");
    }

    private static IReadOnlyList<SpriteGenerationBrief> LoadBriefsFromJson(string json, string source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        var manifest = JsonSerializer.Deserialize<SpriteGenerationManifestDto>(json, Options);
        if (manifest?.Briefs is not { Count: > 0 })
        {
            throw new JsonException($"Sprite generation brief manifest had no briefs: {source}");
        }

        var globalStyle = manifest.GlobalStyle?.Trim();
        var globalNegative = manifest.GlobalNegativePrompt?.Trim();
        var globalRequirements = manifest.GlobalRequirements ?? Array.Empty<string>();
        return manifest.Briefs
            .Select(brief => brief.ToDefinition(globalStyle, globalNegative, globalRequirements))
            .ToArray();
    }

    private sealed record SpriteGenerationManifestDto
    {
        public string? GlobalStyle { get; init; }

        public string? GlobalNegativePrompt { get; init; }

        public string[] GlobalRequirements { get; init; } = Array.Empty<string>();

        public List<SpriteGenerationBriefDto> Briefs { get; init; } = new();
    }

    private sealed record SpriteGenerationBriefDto
    {
        public string? SpriteId { get; init; }

        public string? OutputPath { get; init; }

        public int Width { get; init; } = 16;

        public int Height { get; init; } = 16;

        public string? Subject { get; init; }

        public string? Prompt { get; init; }

        public string? NegativePrompt { get; init; }

        public string Background { get; init; } = "transparent";

        public string[] Requirements { get; init; } = Array.Empty<string>();

        public string[] Palette { get; init; } = Array.Empty<string>();

        public string[] Tags { get; init; } = Array.Empty<string>();

        public SpriteGenerationBrief ToDefinition(
            string? globalStyle,
            string? globalNegativePrompt,
            IReadOnlyList<string> globalRequirements)
        {
            var prompt = string.Join(
                " ",
                new[] { globalStyle, Prompt }.Where(value => !string.IsNullOrWhiteSpace(value)));
            var negativePrompt = string.Join(
                " ",
                new[] { globalNegativePrompt, NegativePrompt }.Where(value => !string.IsNullOrWhiteSpace(value)));

            return new SpriteGenerationBrief
            {
                SpriteId = SpriteId ?? string.Empty,
                OutputPath = OutputPath ?? string.Empty,
                Width = Width,
                Height = Height,
                Subject = Subject ?? string.Empty,
                Prompt = prompt,
                NegativePrompt = negativePrompt,
                Background = Background,
                Requirements = globalRequirements.Concat(Requirements).Where(value => !string.IsNullOrWhiteSpace(value)).ToArray(),
                Palette = Palette.Where(value => !string.IsNullOrWhiteSpace(value)).ToArray(),
                Tags = DefinitionTags.Normalize(Tags)
            };
        }
    }
}
