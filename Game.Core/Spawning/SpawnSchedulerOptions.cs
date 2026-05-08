namespace Game.Core.Spawning;

public sealed record SpawnSchedulerOptions
{
    public float SpawnIntervalSeconds { get; init; } = 5f;

    public int AttemptsPerInterval { get; init; } = 8;

    public int MinDistanceTiles { get; init; } = 18;

    public int MaxDistanceTiles { get; init; } = 46;

    public int VerticalSearchRadiusTiles { get; init; } = 24;

    public int MaxTotalActiveEnemies { get; init; } = 32;

    public int DespawnDistanceTiles { get; init; } = 80;

    public static SpawnSchedulerOptions Default { get; } = new();
}
