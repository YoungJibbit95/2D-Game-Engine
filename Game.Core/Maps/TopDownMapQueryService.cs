using Game.Core.World;

namespace Game.Core.Maps;

public sealed class TopDownMapQueryService
{
    public bool IsBlocked(MapDefinition map, TilePos position)
    {
        return IsBlocked(map, position, runtimeState: null);
    }

    public bool IsBlocked(MapDefinition map, TilePos position, TopDownMapRuntimeState? runtimeState)
    {
        ArgumentNullException.ThrowIfNull(map);

        if (!map.IsInBounds(position))
        {
            return true;
        }

        return map.Layers.Any(layer => layer.BlocksAt(position.X, position.Y)) ||
               map.Objects.Any(item => IsObjectBlocking(item, runtimeState) && item.Bounds.Contains(position));
    }

    public IReadOnlyList<MapObjectDefinition> QueryObjects(
        MapDefinition map,
        RectI region,
        bool interactableOnly = false,
        TopDownMapRuntimeState? runtimeState = null)
    {
        ArgumentNullException.ThrowIfNull(map);

        if (region.IsEmpty)
        {
            return Array.Empty<MapObjectDefinition>();
        }

        return map.Objects
            .Where(item => IsObjectEnabled(item, runtimeState) &&
                           (!interactableOnly || item.IsInteractable) &&
                           item.Bounds.Intersects(region))
            .OrderBy(item => item.TileY)
            .ThenBy(item => item.TileX)
            .ThenBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public IReadOnlyList<MapObjectDefinition> FindObjectsAt(
        MapDefinition map,
        TilePos position,
        bool interactableOnly = false,
        TopDownMapRuntimeState? runtimeState = null)
    {
        return QueryObjects(map, new RectI(position.X, position.Y, 1, 1), interactableOnly, runtimeState);
    }

    public bool TryGetSpawn(MapDefinition map, string spawnId, out TilePos position)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentException.ThrowIfNullOrWhiteSpace(spawnId);

        if (map.TryGetSpawn(spawnId, out var spawn))
        {
            position = new TilePos(spawn.TileX, spawn.TileY);
            return true;
        }

        position = TilePos.Zero;
        return false;
    }

    public bool TryResolveWarp(MapDefinition map, TilePos sourceTile, out MapWarpTarget warp)
    {
        return TryResolveWarp(map, sourceTile, runtimeState: null, out warp);
    }

    public bool TryResolveWarp(
        MapDefinition map,
        TilePos sourceTile,
        TopDownMapRuntimeState? runtimeState,
        out MapWarpTarget warp)
    {
        ArgumentNullException.ThrowIfNull(map);

        var source = map.Objects.FirstOrDefault(item =>
            IsObjectEnabled(item, runtimeState) &&
            item.Kind == MapObjectKind.Warp &&
            item.Bounds.Contains(sourceTile) &&
            !string.IsNullOrWhiteSpace(item.TargetMapId) &&
            !string.IsNullOrWhiteSpace(item.TargetSpawnId));

        if (source is null)
        {
            warp = null!;
            return false;
        }

        warp = new MapWarpTarget(source.TargetMapId!, source.TargetSpawnId!, sourceTile, source.Id);
        return true;
    }

    private static bool IsObjectEnabled(MapObjectDefinition mapObject, TopDownMapRuntimeState? runtimeState)
    {
        return runtimeState?.IsObjectEnabled(mapObject) ?? true;
    }

    private static bool IsObjectBlocking(MapObjectDefinition mapObject, TopDownMapRuntimeState? runtimeState)
    {
        return runtimeState?.IsObjectBlocking(mapObject) ?? mapObject.BlocksMovement;
    }
}
