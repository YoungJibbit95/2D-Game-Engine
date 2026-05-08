namespace Game.Core.Biomes;

public sealed record BiomeDefinition
{
    public required string Id { get; init; }

    public required string DisplayName { get; init; }

    public required string SurfaceTile { get; init; }

    public required string UndergroundTile { get; init; }

    public string? TreeType { get; init; }

    public string? EnemySpawnTable { get; init; }
}
