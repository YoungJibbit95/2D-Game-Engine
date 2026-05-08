namespace Game.Core.Spawning;

public sealed record SpawnRuleDefinition
{
    public required string Id { get; init; }

    public required string EntityId { get; init; }

    public string? BiomeId { get; init; }

    public bool? RequiresNight { get; init; }

    public int? MinTileY { get; init; }

    public int? MaxTileY { get; init; }

    public float Chance { get; init; } = 1f;

    public int MaxActive { get; init; } = 5;
}
