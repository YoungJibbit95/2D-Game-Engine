using Game.Core.Data;

namespace Game.Core.Assets.Generation;

public sealed record SpriteGenerationBrief
{
    public required string SpriteId { get; init; }

    public required string OutputPath { get; init; }

    public int Width { get; init; } = 16;

    public int Height { get; init; } = 16;

    public string Subject { get; init; } = string.Empty;

    public string Prompt { get; init; } = string.Empty;

    public string NegativePrompt { get; init; } = string.Empty;

    public string Background { get; init; } = "transparent";

    public IReadOnlyList<string> Requirements { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> Palette { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();

    public bool HasTag(string tag)
    {
        return DefinitionTags.HasTag(Tags, tag);
    }
}
