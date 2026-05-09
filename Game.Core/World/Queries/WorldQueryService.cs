using System.Numerics;
using GameWorld = Game.Core.World.World;

namespace Game.Core.World.Queries;

public sealed class WorldQueryService
{
    public TileRaycastHit RaycastTiles(
        GameWorld world,
        Vector2 startWorld,
        Vector2 endWorld,
        Func<TileInstance, bool>? predicate = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        predicate ??= static tile => tile.IsSolid;

        var direction = endWorld - startWorld;
        var maxDistance = direction.Length();
        if (maxDistance <= 0.0001f)
        {
            var startTile = CoordinateUtils.WorldToTile(startWorld.X, startWorld.Y);
            return TryHitTile(world, startTile, startWorld, 0, predicate, out var hit)
                ? hit
                : TileRaycastHit.None;
        }

        direction /= maxDistance;
        var current = CoordinateUtils.WorldToTile(startWorld.X, startWorld.Y);
        if (TryHitTile(world, current, startWorld, 0, predicate, out var startHit))
        {
            return startHit;
        }

        var stepX = Math.Sign(direction.X);
        var stepY = Math.Sign(direction.Y);
        var tMaxX = InitialBoundaryDistance(startWorld.X, current.X, direction.X, stepX);
        var tMaxY = InitialBoundaryDistance(startWorld.Y, current.Y, direction.Y, stepY);
        var tDeltaX = stepX == 0 ? float.PositiveInfinity : GameConstants.TileSize / Math.Abs(direction.X);
        var tDeltaY = stepY == 0 ? float.PositiveInfinity : GameConstants.TileSize / Math.Abs(direction.Y);
        var traveled = 0f;

        while (traveled <= maxDistance)
        {
            if (tMaxX < tMaxY)
            {
                current = new TilePos(current.X + stepX, current.Y);
                traveled = tMaxX;
                tMaxX += tDeltaX;
            }
            else
            {
                current = new TilePos(current.X, current.Y + stepY);
                traveled = tMaxY;
                tMaxY += tDeltaY;
            }

            if (traveled > maxDistance)
            {
                break;
            }

            var hitPosition = startWorld + direction * traveled;
            if (TryHitTile(world, current, hitPosition, traveled, predicate, out var hit))
            {
                return hit;
            }
        }

        return TileRaycastHit.None;
    }

    public bool HasLineOfSight(GameWorld world, Vector2 startWorld, Vector2 endWorld)
    {
        return !RaycastTiles(world, startWorld, endWorld, static tile => tile.IsSolid).Hit;
    }

    public IReadOnlyList<TileQueryResult> QueryTiles(
        GameWorld world,
        RectI tileRegion,
        Func<TileInstance, bool>? predicate = null)
    {
        ArgumentNullException.ThrowIfNull(world);

        if (tileRegion.IsEmpty)
        {
            return Array.Empty<TileQueryResult>();
        }

        predicate ??= static _ => true;
        var results = new List<TileQueryResult>();
        var minX = Math.Max(0, tileRegion.Left);
        var maxX = Math.Min(world.WidthTiles - 1, tileRegion.Right - 1);
        var minY = Math.Max(0, tileRegion.Top);
        var maxY = Math.Min(world.HeightTiles - 1, tileRegion.Bottom - 1);

        for (var y = minY; y <= maxY; y++)
        {
            for (var x = minX; x <= maxX; x++)
            {
                var tile = world.GetTile(x, y);
                if (predicate(tile))
                {
                    results.Add(new TileQueryResult(new TilePos(x, y), tile));
                }
            }
        }

        return results;
    }

    private static bool TryHitTile(
        GameWorld world,
        TilePos position,
        Vector2 worldPosition,
        float distance,
        Func<TileInstance, bool> predicate,
        out TileRaycastHit hit)
    {
        if (!world.IsInBounds(position.X, position.Y))
        {
            hit = TileRaycastHit.None;
            return false;
        }

        var tile = world.GetTile(position.X, position.Y);
        if (!predicate(tile))
        {
            hit = TileRaycastHit.None;
            return false;
        }

        hit = new TileRaycastHit(true, position, tile, worldPosition, distance);
        return true;
    }

    private static float InitialBoundaryDistance(float start, int tileCoordinate, float direction, int step)
    {
        if (step == 0)
        {
            return float.PositiveInfinity;
        }

        var boundary = step > 0
            ? (tileCoordinate + 1) * GameConstants.TileSize
            : tileCoordinate * GameConstants.TileSize;

        return Math.Max(0, (boundary - start) / direction);
    }
}
