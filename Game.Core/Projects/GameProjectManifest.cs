using Game.Core.Data;

namespace Game.Core.Projects;

public sealed record GameProjectManifest
{
    public int SchemaVersion { get; init; } = 1;

    public required string Id { get; init; }

    public required string DisplayName { get; init; }

    public string Version { get; init; } = "0.1.0";

    public string EngineVersion { get; init; } = "local";

    public string ContentRoot { get; init; } = "Game.Data";

    public string ModsRoot { get; init; } = "Mods";

    public string SavesRootName { get; init; } = "Saves";

    public string? DefaultWorldProfileId { get; init; }

    public string? StartupMapId { get; init; }

    public string? StartupDefinitionId { get; init; }

    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();

    public bool HasTag(string tag)
    {
        return DefinitionTags.HasTag(Tags, tag);
    }
}
