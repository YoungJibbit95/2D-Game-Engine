namespace Game.Core.Spawning;

public sealed record SpawnSchedulerOptions
{
    public float SpawnIntervalSeconds { get; init; } = 5f;

    public int AttemptsPerInterval { get; init; } = 8;

    public int MinDistanceTiles { get; init; } = 18;

    public int MaxDistanceTiles { get; init; } = 46;

    public int VerticalSearchRadiusTiles { get; init; } = 24;

    public int PlacementSearchRadiusTiles { get; init; } = 24;

    public bool EnablePopulationWarmStart { get; init; } = true;

    public int WarmStartTargetPopulation { get; init; } = 2;

    public int WarmStartAttemptCycles { get; init; } = 6;

    public float WarmStartIntervalSeconds { get; init; } = 0.5f;

    public bool PreferViewportIngress { get; init; } = true;

    public int ViewportIngressBandTiles { get; init; } = 8;

    public int ViewportIngressAttemptCycle { get; init; } = 4;

    public int ViewportIngressAttemptsPerCycle { get; init; } = 3;

    public float ViewportIngressSpeedTilesPerSecond { get; init; } = 8f;

    public float ViewportIngressMaxSeconds { get; init; } = 10f;

    public int SectorCount { get; init; } = 12;

    public int OnScreenHalfWidthTiles { get; init; } = 16;

    public int OnScreenHalfHeightTiles { get; init; } = 10;

    public int OnScreenExclusionPaddingTiles { get; init; } = 2;

    public int MaxTotalActiveEnemies { get; init; } = 32;

    public int DespawnDistanceTiles { get; init; } = 80;

    public static SpawnSchedulerOptions Default { get; } = new();

    internal static SpawnSchedulerOptions DirectPlacement { get; } = new()
    {
        MinDistanceTiles = 0,
        MaxDistanceTiles = int.MaxValue,
        VerticalSearchRadiusTiles = 0,
        PlacementSearchRadiusTiles = 0,
        OnScreenHalfWidthTiles = 0,
        OnScreenHalfHeightTiles = 0,
        OnScreenExclusionPaddingTiles = 0
    };
}
