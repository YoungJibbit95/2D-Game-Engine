using Game.Core.Data;

namespace Game.Core.Startup;

public sealed record GameStartupDefinition
{
    public required string Id { get; init; }

    public required string DisplayName { get; init; }

    public string? WorldProfileId { get; init; }

    public string? StartupMapId { get; init; }

    public int SelectedHotbarSlot { get; init; }

    public IReadOnlyList<StarterItemDefinition> StarterItems { get; init; } =
        Array.Empty<StarterItemDefinition>();

    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();

    public bool HasTag(string tag)
    {
        return DefinitionTags.HasTag(Tags, tag);
    }
}
