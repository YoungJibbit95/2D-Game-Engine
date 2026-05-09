using Game.Core.Data;

namespace Game.Core.Assets;

public sealed record SpriteAssetDefinition
{
    public required string Id { get; init; }

    public required string Path { get; init; }

    public SpriteAssetCategory Category { get; init; }

    public int Width { get; init; } = 16;

    public int Height { get; init; } = 16;

    public int PixelsPerUnit { get; init; } = 16;

    public string? AtlasId { get; init; }

    public IReadOnlyList<SpriteFrameDefinition> Frames { get; init; } = Array.Empty<SpriteFrameDefinition>();

    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();

    public bool HasTag(string tag)
    {
        return DefinitionTags.HasTag(Tags, tag);
    }
}
