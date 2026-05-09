using Game.Core.Entities;
using Game.Core.Time;
using Game.Core.World.Generation;
using GameWorld = Game.Core.World.World;

namespace Game.Core.Diagnostics;

public sealed class EngineDebugSnapshotBuilder
{
    private readonly WorldAnalyzer _worldAnalyzer;

    public EngineDebugSnapshotBuilder(WorldAnalyzer? worldAnalyzer = null)
    {
        _worldAnalyzer = worldAnalyzer ?? new WorldAnalyzer();
    }

    public EngineDebugSnapshot Build(GameWorld world, EntityManager entities, WorldTime? time = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(entities);

        var analysis = _worldAnalyzer.Analyze(world);
        var chunks = world.Chunks.Values.ToArray();
        var entityCounts = entities.Entities
            .GroupBy(entity => entity.GetType().Name)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);

        return new EngineDebugSnapshot(
            world.Metadata.Name,
            world.Metadata.Seed,
            world.WidthTiles,
            world.HeightTiles,
            chunks.Length,
            chunks.Count(chunk => chunk.IsDirty),
            chunks.Count(chunk => chunk.NeedsMeshRebuild),
            chunks.Count(chunk => chunk.NeedsLightUpdate),
            entities.Entities.Count,
            entities.Entities.Count(entity => entity.IsActive),
            analysis.LiquidTileCount,
            analysis.SolidTileCount,
            analysis.AirTileCount,
            analysis.MinSurfaceY,
            analysis.MaxSurfaceY,
            analysis.AverageSurfaceY,
            time?.Day ?? 0,
            time?.NormalizedTimeOfDay ?? 0,
            time?.IsNight ?? false,
            entityCounts);
    }
}
