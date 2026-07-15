using Game.Core.World;
using Game.Core.World.Queries;
using System.Numerics;
using GameWorld = Game.Core.World.World;

namespace Game.Core.Interaction;

public sealed class InteractionQueryService
{
    private readonly WorldQueryService _worldQueries;

    public InteractionQueryService(WorldQueryService? worldQueries = null)
    {
        _worldQueries = worldQueries ?? new WorldQueryService();
    }

    public InteractionResult Resolve(
        GameWorld world,
        in InteractionQuery query,
        IReadOnlyList<InteractionCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(candidates);
        if (!query.IsValid)
        {
            return InteractionResult.Miss(InteractionFailure.InvalidQuery);
        }

        var selected = default(InteractionCandidate);
        var selectedDistance = float.PositiveInfinity;
        var selectedAimDistance = float.PositiveInfinity;
        var selectedPriority = int.MinValue;
        var hasSelected = false;
        var bestFailure = InteractionFailure.NoCandidate;

        for (var index = 0; index < candidates.Count; index++)
        {
            var candidate = candidates[index];
            if (!IsAllowed(query.AllowedKinds, candidate.Identity.Kind) || candidate.WorldBounds.IsEmpty ||
                string.IsNullOrWhiteSpace(candidate.Identity.TargetId))
            {
                continue;
            }

            var target = ClosestPoint(candidate.WorldBounds, query.ActorCenterWorld);
            var targetTile = candidate.Identity.TilePosition;
            if (!world.IsInBounds(targetTile.X, targetTile.Y))
            {
                bestFailure = Prefer(bestFailure, InteractionFailure.OutOfBounds);
                continue;
            }

            if (candidate.RequiresLoadedChunk &&
                !world.TryGetChunk(CoordinateUtils.TileToChunk(targetTile), out _))
            {
                bestFailure = Prefer(bestFailure, InteractionFailure.ChunkNotLoaded);
                continue;
            }

            var distance = Vector2.Distance(query.ActorCenterWorld, target);
            if (distance > query.ReachPixels)
            {
                bestFailure = Prefer(bestFailure, InteractionFailure.OutOfReach);
                continue;
            }

            var aimDistance = DistanceToBounds(candidate.WorldBounds, query.AimWorldPosition);
            if (aimDistance > query.AimAssistRadiusPixels)
            {
                continue;
            }

            if (query.RequireLineOfSight && candidate.RequiresLineOfSight &&
                !HasLineOfSight(world, query.ActorCenterWorld, target, targetTile))
            {
                bestFailure = Prefer(bestFailure, InteractionFailure.Obstructed);
                continue;
            }

            var priority = candidate.Priority + GetKindPriority(candidate.Identity.Kind);
            if (!hasSelected || IsBetter(
                    candidate,
                    distance,
                    aimDistance,
                    priority,
                    selected,
                    selectedDistance,
                    selectedAimDistance,
                    selectedPriority))
            {
                selected = candidate;
                selectedDistance = distance;
                selectedAimDistance = aimDistance;
                selectedPriority = priority;
                hasSelected = true;
            }
        }

        return hasSelected
            ? InteractionResult.Hit(selected, selectedDistance, selectedAimDistance)
            : InteractionResult.Miss(bestFailure);
    }

    public InteractionCandidate? CreateTileCandidate(
        GameWorld world,
        Vector2 aimWorldPosition,
        int requiredHoldTicks = 1)
    {
        ArgumentNullException.ThrowIfNull(world);
        var position = CoordinateUtils.WorldToTile(aimWorldPosition);
        if (!world.TryGetTile(position.X, position.Y, out var tile) || tile.IsAir)
        {
            return null;
        }

        return InteractionCandidate.AtTile(
            InteractionTargetKind.Tile,
            position,
            $"tile:{tile.TileId}",
            requiredHoldTicks: requiredHoldTicks);
    }

    private bool HasLineOfSight(
        GameWorld world,
        Vector2 start,
        Vector2 end,
        TilePos targetTile)
    {
        var hit = _worldQueries.RaycastTiles(world, start, end, static tile => tile.IsSolid);
        return !hit.Hit || hit.TilePosition == targetTile;
    }

    private static bool IsAllowed(InteractionKindMask mask, InteractionTargetKind kind)
    {
        var flag = kind switch
        {
            InteractionTargetKind.Entity => InteractionKindMask.Entity,
            InteractionTargetKind.Harvest => InteractionKindMask.Harvest,
            InteractionTargetKind.Tile => InteractionKindMask.Tile,
            InteractionTargetKind.Placeable => InteractionKindMask.Placeable,
            _ => InteractionKindMask.None
        };
        return (mask & flag) != 0;
    }

    private static int GetKindPriority(InteractionTargetKind kind)
    {
        return kind switch
        {
            InteractionTargetKind.Entity => 4_000,
            InteractionTargetKind.Harvest => 3_000,
            InteractionTargetKind.Tile => 2_000,
            InteractionTargetKind.Placeable => 1_000,
            _ => 0
        };
    }

    private static bool IsBetter(
        in InteractionCandidate candidate,
        float distance,
        float aimDistance,
        int priority,
        in InteractionCandidate selected,
        float selectedDistance,
        float selectedAimDistance,
        int selectedPriority)
    {
        if (priority != selectedPriority)
        {
            return priority > selectedPriority;
        }

        var aimComparison = aimDistance.CompareTo(selectedAimDistance);
        if (aimComparison != 0)
        {
            return aimComparison < 0;
        }

        var distanceComparison = distance.CompareTo(selectedDistance);
        if (distanceComparison != 0)
        {
            return distanceComparison < 0;
        }

        var entityComparison = candidate.Identity.EntityId.CompareTo(selected.Identity.EntityId);
        if (entityComparison != 0)
        {
            return entityComparison < 0;
        }

        var xComparison = candidate.Identity.TilePosition.X.CompareTo(selected.Identity.TilePosition.X);
        return xComparison != 0
            ? xComparison < 0
            : candidate.Identity.TilePosition.Y < selected.Identity.TilePosition.Y;
    }

    private static Vector2 ClosestPoint(RectI bounds, Vector2 point)
    {
        return new Vector2(
            Math.Clamp(point.X, bounds.Left, Math.Max(bounds.Left, bounds.Right - 1)),
            Math.Clamp(point.Y, bounds.Top, Math.Max(bounds.Top, bounds.Bottom - 1)));
    }

    private static float DistanceToBounds(RectI bounds, Vector2 point)
    {
        return Vector2.Distance(point, ClosestPoint(bounds, point));
    }

    private static InteractionFailure Prefer(InteractionFailure current, InteractionFailure candidate)
    {
        return FailurePriority(candidate) > FailurePriority(current) ? candidate : current;
    }

    private static int FailurePriority(InteractionFailure failure)
    {
        return failure switch
        {
            InteractionFailure.OutOfBounds => 5,
            InteractionFailure.ChunkNotLoaded => 4,
            InteractionFailure.OutOfReach => 3,
            InteractionFailure.Obstructed => 2,
            InteractionFailure.NoCandidate => 1,
            _ => 0
        };
    }
}
