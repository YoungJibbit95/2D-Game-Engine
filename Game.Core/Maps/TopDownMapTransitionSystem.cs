using Game.Core.World;

namespace Game.Core.Maps;

public sealed class TopDownMapTransitionSystem
{
    private readonly TopDownMapQueryService _queries;

    public TopDownMapTransitionSystem(TopDownMapQueryService? queries = null)
    {
        _queries = queries ?? new TopDownMapQueryService();
    }

    public TopDownMapTransitionResult TryApplyCurrentWarp(MapRegistry maps, TopDownMapSession session)
    {
        ArgumentNullException.ThrowIfNull(maps);
        ArgumentNullException.ThrowIfNull(session);

        if (!maps.TryGetById(session.CurrentMapId, out var currentMap))
        {
            return TopDownMapTransitionResult.Failed(session.CurrentMapId, session.Body.Position, "current_map_missing");
        }

        return TryApplyWarpAt(maps, session, currentMap, session.Body.CenterTile(currentMap.TileSize));
    }

    public TopDownMapTransitionResult TryApplyWarpAt(
        MapRegistry maps,
        TopDownMapSession session,
        MapDefinition currentMap,
        TilePos sourceTile)
    {
        ArgumentNullException.ThrowIfNull(maps);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(currentMap);

        var previousPosition = session.Body.Position;
        if (!_queries.TryResolveWarp(currentMap, sourceTile, out var warp))
        {
            return TopDownMapTransitionResult.Failed(currentMap.Id, previousPosition, "no_warp_at_source_tile");
        }

        if (!maps.TryGetById(warp.TargetMapId, out var targetMap))
        {
            return TopDownMapTransitionResult.Failed(currentMap.Id, previousPosition, "target_map_missing");
        }

        if (!targetMap.TryGetSpawn(warp.TargetSpawnId, out var targetSpawn))
        {
            return TopDownMapTransitionResult.Failed(currentMap.Id, previousPosition, "target_spawn_missing");
        }

        session.MoveToSpawn(targetMap, targetSpawn);
        return TopDownMapTransitionResult.Applied(currentMap.Id, warp, previousPosition, session.Body.Position);
    }
}
