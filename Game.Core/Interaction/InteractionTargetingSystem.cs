using Game.Core.World;
using Game.Core.World.Queries;
using System.Numerics;
using GameWorld = Game.Core.World.World;

namespace Game.Core.Interaction;

public sealed class InteractionTargetingSystem
{
    private readonly WorldQueryService _queries;

    public InteractionTargetingSystem(WorldQueryService? queries = null)
    {
        _queries = queries ?? new WorldQueryService();
    }

    public InteractionTarget FindMiningTarget(
        GameWorld world,
        Vector2 actorCenterWorld,
        Vector2 aimWorldPosition,
        float reachPixels)
    {
        ArgumentNullException.ThrowIfNull(world);

        var end = ClampToReach(actorCenterWorld, aimWorldPosition, reachPixels);
        var hit = _queries.RaycastTiles(world, actorCenterWorld, end, static tile => !tile.IsAir || tile.WallId != 0);
        return hit.Hit
            ? new InteractionTarget(true, hit.TilePosition)
            : InteractionTarget.None;
    }

    public InteractionTarget FindPlacementTarget(
        GameWorld world,
        Vector2 actorCenterWorld,
        Vector2 aimWorldPosition,
        float reachPixels)
    {
        ArgumentNullException.ThrowIfNull(world);

        var end = ClampToReach(actorCenterWorld, aimWorldPosition, reachPixels);
        var target = CoordinateUtils.WorldToTile(end.X, end.Y);
        if (!world.IsInBounds(target.X, target.Y) || !world.GetTile(target.X, target.Y).IsAir)
        {
            return InteractionTarget.None;
        }

        var obstruction = _queries.RaycastTiles(world, actorCenterWorld, end, static tile => tile.IsSolid);
        return obstruction.Hit
            ? InteractionTarget.None
            : new InteractionTarget(true, target);
    }

    private static Vector2 ClampToReach(Vector2 origin, Vector2 target, float reachPixels)
    {
        if (reachPixels <= 0)
        {
            return origin;
        }

        var delta = target - origin;
        var distance = delta.Length();
        if (distance <= reachPixels || distance <= 0.0001f)
        {
            return target;
        }

        return origin + delta / distance * reachPixels;
    }
}
