using Game.Core.Combat;
using Game.Core.Entities;
using System.Numerics;

namespace Game.Core.Projectiles;

public sealed class ProjectileRuntimeState
{
    public const float GravityPixelsPerSecondSquared = 980;

    private readonly HashSet<int> _hitEntityIds = new();

    public ProjectileRuntimeState(
        ulong instanceId,
        ProjectileDefinition definition,
        Vector2 position,
        Vector2 velocity,
        int? ownerEntityId = null,
        EntityFaction ownerFaction = EntityFaction.Friendly,
        float ageSeconds = 0,
        int? remainingPierces = null,
        int? remainingBounces = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ValidateDefinition(definition);
        ValidateFinite(position, nameof(position));
        ValidateFinite(velocity, nameof(velocity));
        if (!float.IsFinite(ageSeconds) || ageSeconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ageSeconds));
        }

        if (ownerEntityId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ownerEntityId));
        }

        if (remainingPierces < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(remainingPierces));
        }

        if (remainingBounces < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(remainingBounces));
        }

        InstanceId = instanceId;
        Definition = definition;
        Position = position;
        PreviousPosition = position;
        Velocity = velocity;
        OwnerEntityId = ownerEntityId;
        OwnerFaction = ownerFaction;
        AgeSeconds = Math.Min(ageSeconds, definition.Lifetime);
        RemainingPierces = remainingPierces ?? definition.Pierce;
        RemainingBounces = remainingBounces ?? definition.BounceCount;
        IsActive = ageSeconds < definition.Lifetime;
        TerminationReason = IsActive
            ? ProjectileTerminationReason.None
            : ProjectileTerminationReason.LifetimeExpired;
    }

    public ulong InstanceId { get; private set; }

    public ProjectileDefinition Definition { get; }

    public Vector2 PreviousPosition { get; private set; }

    public Vector2 Position { get; private set; }

    public Vector2 Velocity { get; private set; }

    public int? OwnerEntityId { get; }

    public EntityFaction OwnerFaction { get; }

    public float AgeSeconds { get; private set; }

    public int RemainingPierces { get; private set; }

    public int RemainingBounces { get; private set; }

    public bool IsActive { get; private set; }

    public ProjectileTerminationReason TerminationReason { get; private set; }

    public ProjectileMotionResult Advance(
        float deltaSeconds,
        IReadOnlyList<ProjectileHomingTarget>? homingTargets = null)
    {
        if (!float.IsFinite(deltaSeconds) || deltaSeconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
        }

        PreviousPosition = Position;
        if (!IsActive || deltaSeconds == 0)
        {
            return CreateMotionResult(null, expired: false);
        }

        var lifetimeRemaining = Definition.Lifetime - AgeSeconds;
        var simulatedSeconds = Math.Min(deltaSeconds, lifetimeRemaining);
        var homingTarget = SelectHomingTarget(homingTargets);
        if (homingTarget is { } selected)
        {
            ApplyHoming(selected.Position, simulatedSeconds);
        }

        Velocity += new Vector2(
            0,
            Definition.Gravity * GravityPixelsPerSecondSquared * simulatedSeconds);
        if (Definition.DragPerSecond > 0)
        {
            Velocity *= MathF.Exp(-Definition.DragPerSecond * simulatedSeconds);
        }

        Position += Velocity * simulatedSeconds;
        AgeSeconds += simulatedSeconds;
        var expired = deltaSeconds >= lifetimeRemaining;
        if (expired)
        {
            Terminate(ProjectileTerminationReason.LifetimeExpired);
        }

        return CreateMotionResult(homingTarget?.EntityId, expired);
    }

    public void BindInstanceId(ulong instanceId)
    {
        if (instanceId == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(instanceId));
        }

        if (InstanceId != 0 && InstanceId != instanceId)
        {
            throw new InvalidOperationException($"Projectile runtime is already bound to instance {InstanceId}.");
        }

        InstanceId = instanceId;
    }

    public void SetVelocity(Vector2 velocity)
    {
        ValidateFinite(velocity, nameof(velocity));
        Velocity = velocity;
    }

    public ProjectileTileCollisionResult ResolveTileCollision(ProjectileTileCollision collision)
    {
        ValidateFinite(collision.ContactPoint, nameof(collision));
        ValidateFinite(collision.SurfaceNormal, nameof(collision));
        if (!IsActive)
        {
            return CreateTileResult(ProjectileTileCollisionDecision.IgnoredInactive);
        }

        if (Definition.TileCollisionBehavior == ProjectileTileCollisionBehavior.Ignore)
        {
            return CreateTileResult(ProjectileTileCollisionDecision.IgnoredByDefinition);
        }

        if (Definition.TileCollisionBehavior == ProjectileTileCollisionBehavior.Bounce && RemainingBounces > 0)
        {
            if (collision.SurfaceNormal.LengthSquared() <= float.Epsilon)
            {
                throw new ArgumentException("A bounce collision requires a non-zero surface normal.", nameof(collision));
            }

            var normal = Vector2.Normalize(collision.SurfaceNormal);
            Position = collision.ContactPoint + normal * (Definition.CollisionRadius + 0.001f);
            Velocity = Vector2.Reflect(Velocity, normal) * Definition.BounceRestitution;
            RemainingBounces--;
            return CreateTileResult(ProjectileTileCollisionDecision.Bounced);
        }

        Position = collision.ContactPoint;
        Terminate(ProjectileTerminationReason.TileCollision);
        return CreateTileResult(ProjectileTileCollisionDecision.Destroyed);
    }

    public ProjectileEntityCollisionResult ResolveEntityCollision(ProjectileEntityCollision collision)
    {
        if (collision.TargetEntityId < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(collision));
        }

        if (!IsActive)
        {
            return CreateEntityResult(ProjectileEntityCollisionDecision.IgnoredInactive, null);
        }

        if (Definition.EntityCollisionBehavior == ProjectileEntityCollisionBehavior.Ignore)
        {
            return CreateEntityResult(ProjectileEntityCollisionDecision.IgnoredByDefinition, null);
        }

        if (OwnerEntityId == collision.TargetEntityId)
        {
            return CreateEntityResult(ProjectileEntityCollisionDecision.IgnoredOwner, null);
        }

        if (!Definition.FriendlyFire && collision.TargetFaction == OwnerFaction)
        {
            return CreateEntityResult(ProjectileEntityCollisionDecision.IgnoredFriendly, null);
        }

        if (Definition.HitOncePerTarget && !_hitEntityIds.Add(collision.TargetEntityId))
        {
            return CreateEntityResult(ProjectileEntityCollisionDecision.IgnoredAlreadyHit, null);
        }

        var damageRequest = new CombatDamageRequest
        {
            AttackInstanceId = InstanceId,
            SourceEntityId = OwnerEntityId,
            TargetEntityId = collision.TargetEntityId,
            SourceFaction = OwnerFaction,
            TargetFaction = collision.TargetFaction,
            BaseDamage = Definition.Damage,
            DamageType = Definition.DamageType,
            ImpactDirection = Velocity,
            KnockbackForce = Definition.Knockback,
            CriticalChance = Definition.CriticalChance,
            CriticalMultiplier = Definition.CriticalMultiplier,
            StatusEffects = Definition.OnHitEffects
        };

        if (RemainingPierces <= 0)
        {
            Terminate(ProjectileTerminationReason.EntityHit);
            return CreateEntityResult(ProjectileEntityCollisionDecision.HitAndStopped, damageRequest);
        }

        RemainingPierces--;
        return CreateEntityResult(ProjectileEntityCollisionDecision.Hit, damageRequest);
    }

    public void Terminate()
    {
        Terminate(ProjectileTerminationReason.ExplicitlyTerminated);
    }

    public void RegisterUntrackedHit()
    {
        if (!IsActive)
        {
            return;
        }

        if (RemainingPierces <= 0)
        {
            Terminate(ProjectileTerminationReason.EntityHit);
            return;
        }

        RemainingPierces--;
    }

    private ProjectileHomingTarget? SelectHomingTarget(IReadOnlyList<ProjectileHomingTarget>? candidates)
    {
        if (candidates is null ||
            candidates.Count == 0 ||
            Definition.HomingTurnRateRadiansPerSecond <= 0 ||
            Definition.HomingRange <= 0)
        {
            return null;
        }

        ProjectileHomingTarget? selected = null;
        var selectedDistanceSquared = Definition.HomingRange * Definition.HomingRange;
        for (var index = 0; index < candidates.Count; index++)
        {
            var candidate = candidates[index];
            if (!candidate.IsActive ||
                candidate.EntityId < 0 ||
                candidate.EntityId == OwnerEntityId ||
                !float.IsFinite(candidate.Position.X) ||
                !float.IsFinite(candidate.Position.Y) ||
                !Definition.FriendlyFire && candidate.Faction == OwnerFaction)
            {
                continue;
            }

            var distanceSquared = Vector2.DistanceSquared(Position, candidate.Position);
            if (distanceSquared > selectedDistanceSquared ||
                selected is { } current &&
                distanceSquared == selectedDistanceSquared &&
                candidate.EntityId >= current.EntityId)
            {
                continue;
            }

            selected = candidate;
            selectedDistanceSquared = distanceSquared;
        }

        return selected;
    }

    private void ApplyHoming(Vector2 targetPosition, float deltaSeconds)
    {
        var toTarget = targetPosition - Position;
        if (toTarget.LengthSquared() <= float.Epsilon)
        {
            return;
        }

        var speed = Velocity.Length();
        if (speed <= float.Epsilon)
        {
            speed = Definition.Speed;
            Velocity = Vector2.Normalize(toTarget) * speed;
            return;
        }

        var currentAngle = MathF.Atan2(Velocity.Y, Velocity.X);
        var targetAngle = MathF.Atan2(toTarget.Y, toTarget.X);
        var difference = WrapAngle(targetAngle - currentAngle);
        var maximumTurn = Definition.HomingTurnRateRadiansPerSecond * deltaSeconds;
        var resolvedAngle = currentAngle + Math.Clamp(difference, -maximumTurn, maximumTurn);
        Velocity = new Vector2(MathF.Cos(resolvedAngle), MathF.Sin(resolvedAngle)) * speed;
    }

    private static float WrapAngle(float angle)
    {
        while (angle > MathF.PI)
        {
            angle -= MathF.Tau;
        }

        while (angle < -MathF.PI)
        {
            angle += MathF.Tau;
        }

        return angle;
    }

    private ProjectileMotionResult CreateMotionResult(int? homingTargetEntityId, bool expired)
    {
        return new ProjectileMotionResult(
            PreviousPosition,
            Position,
            Velocity,
            AgeSeconds,
            homingTargetEntityId,
            expired,
            TerminationReason);
    }

    private ProjectileTileCollisionResult CreateTileResult(ProjectileTileCollisionDecision decision)
    {
        return new ProjectileTileCollisionResult(
            decision,
            Position,
            Velocity,
            RemainingBounces,
            IsActive,
            TerminationReason);
    }

    private ProjectileEntityCollisionResult CreateEntityResult(
        ProjectileEntityCollisionDecision decision,
        CombatDamageRequest? damageRequest)
    {
        return new ProjectileEntityCollisionResult(
            decision,
            damageRequest,
            RemainingPierces,
            IsActive,
            TerminationReason);
    }

    private void Terminate(ProjectileTerminationReason reason)
    {
        IsActive = false;
        TerminationReason = reason;
    }

    private static void ValidateDefinition(ProjectileDefinition definition)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Id);
        if (!float.IsFinite(definition.Speed) || definition.Speed < 0 ||
            definition.Damage < 0 ||
            !float.IsFinite(definition.Gravity) ||
            !float.IsFinite(definition.DragPerSecond) || definition.DragPerSecond < 0 ||
            !float.IsFinite(definition.HomingTurnRateRadiansPerSecond) ||
            definition.HomingTurnRateRadiansPerSecond < 0 ||
            !float.IsFinite(definition.HomingRange) || definition.HomingRange < 0 ||
            definition.Pierce < 0 ||
            definition.BounceCount < 0 ||
            !float.IsFinite(definition.BounceRestitution) ||
            definition.BounceRestitution < 0 || definition.BounceRestitution > 1 ||
            !float.IsFinite(definition.CollisionRadius) || definition.CollisionRadius <= 0 ||
            !float.IsFinite(definition.Knockback) || definition.Knockback < 0 ||
            !float.IsFinite(definition.CriticalChance) ||
            definition.CriticalChance < 0 || definition.CriticalChance > 1 ||
            !float.IsFinite(definition.CriticalMultiplier) || definition.CriticalMultiplier < 1 ||
            !float.IsFinite(definition.Lifetime) || definition.Lifetime <= 0)
        {
            throw new ArgumentException("Projectile definition contains invalid runtime values.", nameof(definition));
        }
    }

    private static void ValidateFinite(Vector2 value, string name)
    {
        if (!float.IsFinite(value.X) || !float.IsFinite(value.Y))
        {
            throw new ArgumentOutOfRangeException(name);
        }
    }
}
