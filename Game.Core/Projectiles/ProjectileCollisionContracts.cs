using Game.Core.Combat;
using Game.Core.Entities;
using System.Numerics;

namespace Game.Core.Projectiles;

public enum ProjectileTileCollisionBehavior
{
    Destroy,
    Bounce,
    Ignore
}

public enum ProjectileEntityCollisionBehavior
{
    Damage,
    Ignore
}

public enum ProjectileTerminationReason
{
    None,
    LifetimeExpired,
    TileCollision,
    EntityHit,
    ExplicitlyTerminated
}

public enum ProjectileTileCollisionDecision
{
    IgnoredInactive,
    IgnoredByDefinition,
    Bounced,
    Destroyed
}

public enum ProjectileEntityCollisionDecision
{
    IgnoredInactive,
    IgnoredByDefinition,
    IgnoredOwner,
    IgnoredFriendly,
    IgnoredAlreadyHit,
    Hit,
    HitAndStopped
}

public readonly record struct ProjectileHomingTarget(
    int EntityId,
    EntityFaction Faction,
    Vector2 Position,
    bool IsActive = true);

public readonly record struct ProjectileMotionResult(
    Vector2 PreviousPosition,
    Vector2 Position,
    Vector2 Velocity,
    float AgeSeconds,
    int? HomingTargetEntityId,
    bool Expired,
    ProjectileTerminationReason TerminationReason);

public readonly record struct ProjectileTileCollision(
    Vector2 ContactPoint,
    Vector2 SurfaceNormal,
    int? TileX = null,
    int? TileY = null,
    ushort? TileId = null);

public readonly record struct ProjectileTileCollisionResult(
    ProjectileTileCollisionDecision Decision,
    Vector2 Position,
    Vector2 Velocity,
    int RemainingBounces,
    bool IsActive,
    ProjectileTerminationReason TerminationReason,
    int? TileX = null,
    int? TileY = null,
    ushort? TileId = null);

public readonly record struct ProjectileEntityCollision(
    int TargetEntityId,
    EntityFaction TargetFaction);

public readonly record struct ProjectileEntityCollisionResult(
    ProjectileEntityCollisionDecision Decision,
    CombatDamageRequest? DamageRequest,
    int RemainingPierces,
    bool IsActive,
    ProjectileTerminationReason TerminationReason)
{
    public bool Accepted => Decision is
        ProjectileEntityCollisionDecision.Hit or
        ProjectileEntityCollisionDecision.HitAndStopped;
}
