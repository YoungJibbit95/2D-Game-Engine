namespace Game.Core.Runtime;

public sealed record GameSimulationOptions
{
    public static GameSimulationOptions Default { get; } = new();

    public bool AutoPickupItems { get; init; } = true;

    public int PickupRadiusPixels { get; init; } = 28;

    public float ItemMagnetRadiusPixels { get; init; } = 92f;

    public float ItemMagnetStrength { get; init; } = 480f;

    public bool EnablePhaseTelemetry { get; init; }

    public int MaxLightingChunksPerTick { get; init; } = 1;

    public int MaxActiveEnemies { get; init; } = -1;

    public float EnemySpawnRateMultiplier { get; init; } = 1f;

    public int SpawnMinimumDistanceTiles { get; init; } = 18;

    public int SpawnMaximumDistanceTiles { get; init; } = 46;

    public int SpawnVisibleHalfWidthTiles { get; init; } = 16;

    public int SpawnVisibleHalfHeightTiles { get; init; } = 10;

    public bool SpawnOutsideViewportOnly { get; init; } = true;

    public float WorldTimeRateMultiplier { get; init; } = 1f;

    public float PlayerMovementSpeedMultiplier { get; init; } = 1f;

    public float PlayerMiningSpeedMultiplier { get; init; } = 1f;

    public float PlayerManaCostMultiplier { get; init; } = 1f;

    public bool PlayerInvulnerable { get; init; }

    public bool PlayerNoClip { get; init; }

    public bool PlayerFreeFlight { get; init; }

    public bool FriendlyFireEnabled { get; init; }
}
