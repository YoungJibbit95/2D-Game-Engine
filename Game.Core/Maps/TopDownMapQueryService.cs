using Game.Core.World;

namespace Game.Core.Maps;

public sealed class TopDownMapQueryService
{
    public bool IsBlocked(MapDefinition map, TilePos position)
    {
        ArgumentNullException.ThrowIfNull(map);

        if (!map.IsInBounds(position))
        {
            return true;
        }

        return map.Layers.Any(layer => layer.BlocksAt(position.X, position.Y)) ||
               map.Objects.Any(item => item.BlocksMovement && item.Bounds.Contains(position));
    }

    public IReadOnlyList<MapObjectDefinition> QueryObjects(MapDefinition map, RectI region, bool interactableOnly = false)
    {
        ArgumentNullException.ThrowIfNull(map);

        if (region.IsEmpty)
        {
            return Array.Empty<MapObjectDefinition>();
        }

        return map.Objects
            .Where(item => (!interactableOnly || item.IsInteractable) && item.Bounds.Intersects(region))
            .OrderBy(item => item.TileY)
            .ThenBy(item => item.TileX)
            .ThenBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public IReadOnlyList<MapObjectDefinition> FindObjectsAt(MapDefinition map, TilePos position, bool interactableOnly = false)
    {
        return QueryObjects(map, new RectI(position.X, position.Y, 1, 1), interactableOnly);
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
        ArgumentNullException.ThrowIfNull(map);

        var source = map.Objects.FirstOrDefault(item =>
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
}
