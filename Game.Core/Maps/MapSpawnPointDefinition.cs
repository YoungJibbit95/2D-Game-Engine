namespace Game.Core.Maps;

public sealed record MapSpawnPointDefinition
{
    public required string Id { get; init; }

    public int TileX { get; init; }

    public int TileY { get; init; }

    public string Facing { get; init; } = "down";
}
