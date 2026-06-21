using Game.Core.Data;

namespace Game.Core.Characters;

public sealed record CharacterDefinition
{
    public required string Id { get; init; }

    public required string DisplayName { get; init; }

    public float Width { get; init; } = 12f;

    public float Height { get; init; } = 28f;

    public CharacterAppearance DefaultAppearance { get; init; } = new();

    public required CharacterAnimationSetDefinition AnimationSet { get; init; }

    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();

    public bool HasTag(string tag)
    {
        return DefinitionTags.HasTag(Tags, tag);
    }
}
