namespace Game.Core.Mods;

public sealed record ContentPackManifest
{
    public required string Id { get; init; }

    public string DisplayName { get; init; } = string.Empty;

    public string Version { get; init; } = "0.0.0";
}
