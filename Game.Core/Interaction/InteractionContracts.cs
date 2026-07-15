using Game.Core.World;
using System.Numerics;

namespace Game.Core.Interaction;

[Flags]
public enum InteractionKindMask
{
    None = 0,
    Entity = 1 << 0,
    Harvest = 1 << 1,
    Tile = 1 << 2,
    Placeable = 1 << 3,
    All = Entity | Harvest | Tile | Placeable
}

public enum InteractionTargetKind
{
    Entity,
    Harvest,
    Tile,
    Placeable
}

public enum InteractionFailure
{
    None,
    InvalidQuery,
    NoCandidate,
    OutOfBounds,
    ChunkNotLoaded,
    OutOfReach,
    Obstructed,
    Occupied,
    Unsupported,
    ActorUnavailable,
    TargetChanged,
    Released
}

public readonly record struct InteractionTargetIdentity(
    InteractionTargetKind Kind,
    int EntityId,
    TilePos TilePosition,
    string TargetId);

public readonly record struct InteractionCandidate(
    InteractionTargetIdentity Identity,
    RectI WorldBounds,
    int Priority,
    int RequiredHoldTicks,
    bool RequiresLineOfSight,
    bool RequiresLoadedChunk)
{
    public static InteractionCandidate AtTile(
        InteractionTargetKind kind,
        TilePos position,
        string targetId,
        int priority = 0,
        int requiredHoldTicks = 1,
        bool requiresLineOfSight = true,
        bool requiresLoadedChunk = true)
    {
        return new InteractionCandidate(
            new InteractionTargetIdentity(kind, 0, position, targetId),
            new RectI(
                SaturatingTileOrigin(position.X),
                SaturatingTileOrigin(position.Y),
                GameConstants.TileSize,
                GameConstants.TileSize),
            priority,
            Math.Max(1, requiredHoldTicks),
            requiresLineOfSight,
            requiresLoadedChunk);
    }

    public static InteractionCandidate ForEntity(
        int entityId,
        string targetId,
        RectI worldBounds,
        int priority = 0,
        int requiredHoldTicks = 1,
        bool requiresLineOfSight = true)
    {
        if (entityId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(entityId));
        }

        return new InteractionCandidate(
            new InteractionTargetIdentity(
                InteractionTargetKind.Entity,
                entityId,
                CoordinateUtils.WorldToTile(worldBounds.X, worldBounds.Y),
                targetId),
            worldBounds,
            priority,
            Math.Max(1, requiredHoldTicks),
            requiresLineOfSight,
            false);
    }

    private static int SaturatingTileOrigin(int tileCoordinate)
    {
        return (int)Math.Clamp(
            (long)tileCoordinate * GameConstants.TileSize,
            int.MinValue,
            (long)int.MaxValue - GameConstants.TileSize);
    }
}

public readonly record struct InteractionQuery(
    Vector2 ActorCenterWorld,
    RectI ActorBoundsWorld,
    Vector2 AimWorldPosition,
    float ReachPixels,
    InteractionKindMask AllowedKinds,
    bool RequireLineOfSight,
    float AimAssistRadiusPixels = 24f)
{
    public bool IsValid =>
        float.IsFinite(ActorCenterWorld.X) && float.IsFinite(ActorCenterWorld.Y) &&
        float.IsFinite(AimWorldPosition.X) && float.IsFinite(AimWorldPosition.Y) &&
        float.IsFinite(ReachPixels) && ReachPixels > 0f &&
        float.IsFinite(AimAssistRadiusPixels) && AimAssistRadiusPixels >= 0f &&
        AllowedKinds != InteractionKindMask.None && !ActorBoundsWorld.IsEmpty;
}

public readonly record struct InteractionResult(
    bool Success,
    InteractionCandidate Candidate,
    InteractionFailure Failure,
    float DistancePixels,
    float AimDistancePixels)
{
    public static InteractionResult Miss(InteractionFailure failure)
    {
        return new InteractionResult(false, default, failure, float.PositiveInfinity, float.PositiveInfinity);
    }

    public static InteractionResult Hit(
        in InteractionCandidate candidate,
        float distancePixels,
        float aimDistancePixels)
    {
        return new InteractionResult(true, candidate, InteractionFailure.None, distancePixels, aimDistancePixels);
    }
}
