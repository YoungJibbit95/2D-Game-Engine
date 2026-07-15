using Game.Core.Biomes;
using Game.Core.World;

namespace Game.Core.Spawning;

internal readonly record struct SpawnAttemptContext(
    TilePos TargetTile,
    BiomeMap? BiomeMap,
    string? ExplicitBiomeId,
    IReadOnlyList<SpawnActivitySource>? ActivitySources,
    SpawnActivitySource SingleSource,
    int SourceIndex,
    int SourceCount,
    bool UseImplicitVisibleBounds,
    SpawnSchedulerOptions Options)
{
    public static SpawnAttemptContext Direct(
        TilePos tile,
        BiomeMap? biomeMap,
        string? biomeId)
    {
        return new SpawnAttemptContext(
            tile,
            biomeMap,
            biomeId,
            null,
            default,
            0,
            0,
            false,
            SpawnSchedulerOptions.DirectPlacement);
    }

    public SpawnActivitySource GetSource(int index)
    {
        return ActivitySources is null ? SingleSource : ActivitySources[index];
    }
}
