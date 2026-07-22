using Game.Core.Combat;
using Game.Core.Entities;
using Game.Core.Physics;
using Game.Core.World;
using System.Numerics;
using GameWorld = Game.Core.World.World;

namespace Game.Core.Projectiles;

public sealed class ProjectileEntity : Entity, IEntityPhysicsParticipant, IContinuousCollisionPhysicsParticipant
{
    private static readonly TileCollisionSettings DefaultCollisionSettings = new TileCollisionResolver().Settings;
    private IReadOnlyList<ProjectileHomingTarget>? _homingTargetsForNextUpdate;
    private ProjectileTileCollisionResult? _latestTileCollision;
    private Vector2 _latestTileCollisionIncomingVelocity;

    public ProjectileEntity(
        string projectileId,
        Vector2 position,
        Vector2 velocity,
        int damage,
        float gravity,
        int pierce,
        float lifetime,
        int? ownerEntityId = null,
        float age = 0)
        : this(
            projectileId,
            position,
            velocity,
            damage,
            DamageType.Ranged,
            gravity,
            pierce,
            lifetime,
            ownerEntityId,
            age)
    {
    }

    public ProjectileEntity(
        string projectileId,
        Vector2 position,
        Vector2 velocity,
        int damage,
        DamageType damageType,
        float gravity,
        int pierce,
        float lifetime,
        int? ownerEntityId = null,
        float age = 0)
        : this(
            CreateCompatibilityDefinition(
                projectileId,
                velocity,
                damage,
                damageType,
                gravity,
                pierce,
                lifetime),
            position,
            velocity,
            ownerEntityId,
            EntityFaction.Friendly,
            age: age,
            remainingPierces: pierce)
    {
    }

    public ProjectileEntity(
        ProjectileDefinition definition,
        Vector2 position,
        Vector2 velocity,
        int? ownerEntityId = null,
        EntityFaction ownerFaction = EntityFaction.Friendly,
        ulong instanceId = 0,
        float age = 0,
        int? remainingPierces = null,
        int? remainingBounces = null)
    {
        RuntimeState = new ProjectileRuntimeState(
            instanceId,
            definition,
            position,
            velocity,
            ownerEntityId,
            ownerFaction,
            age,
            remainingPierces,
            remainingBounces);
        Body = new PhysicsBody
        {
            Position = position,
            Size = CreateBodySize(definition),
            BodyType = PhysicsBodyType.Dynamic,
            CollidesWithTiles = false,
            CollisionLayer = PhysicsCollisionLayer.Projectile,
            CollisionMask = PhysicsCollisionLayer.World,
            Material = new PhysicsMaterial(0f, definition.BounceRestitution),
            GravityScale = 0f
        };
        CollisionSettings = DefaultCollisionSettings;
        Position = position;
        IsActive = RuntimeState.IsActive;
        SynchronizeRuntimeState();
    }

    public ProjectileRuntimeState RuntimeState { get; }

    public ProjectileDefinition Definition => RuntimeState.Definition;

    public string ProjectileId => Definition.Id;

    public PhysicsBody Body { get; }

    public Vector2 Velocity
    {
        get => RuntimeState.Velocity;
        set => RuntimeState.SetVelocity(value);
    }

    public int Damage => Definition.Damage;

    public DamageType DamageType => Definition.DamageType;

    public float Gravity => Definition.Gravity;

    public int Pierce => RuntimeState.RemainingPierces;

    public int RemainingBounces => RuntimeState.RemainingBounces;

    public float Lifetime => Definition.Lifetime;

    public int? OwnerEntityId => RuntimeState.OwnerEntityId;

    public EntityFaction OwnerFaction => RuntimeState.OwnerFaction;

    public float Age => RuntimeState.AgeSeconds;

    public DamageInfo DamageInfo => new(
        Damage,
        DamageType,
        OwnerEntityId,
        Vector2.Normalize(Velocity == Vector2.Zero ? Vector2.UnitX : Velocity),
        Definition.Knockback);

    public override RectI Bounds
    {
        get
        {
            var size = Math.Max(1, (int)MathF.Ceiling(Definition.CollisionRadius * 2));
            return new RectI(
                (int)MathF.Floor(Position.X),
                (int)MathF.Floor(Position.Y),
                size,
                size);
        }
    }

    public override void Update(GameWorld world, float deltaSeconds)
    {
        ArgumentNullException.ThrowIfNull(world);
        var homingTargets = _homingTargetsForNextUpdate;
        _homingTargetsForNextUpdate = null;
        var motion = AdvanceRuntime(deltaSeconds, homingTargets);
        if (!IsActive ||
            !TryFindSolidTileCollision(world, motion.PreviousPosition, motion.Position, out var collision))
        {
            ClearLatestTileCollision();
            return;
        }

        _ = ResolveTileCollision(collision);
    }

    internal bool PreparePhysicsUpdate(float deltaSeconds)
    {
        var homingTargets = _homingTargetsForNextUpdate;
        _homingTargetsForNextUpdate = null;
        RuntimeState.Advance(deltaSeconds, homingTargets, applyTranslation: false);
        SynchronizeRuntimeState();
        return IsActive;
    }

    public void SetHomingTargetsForNextUpdate(IReadOnlyList<ProjectileHomingTarget> homingTargets)
    {
        ArgumentNullException.ThrowIfNull(homingTargets);
        _homingTargetsForNextUpdate = homingTargets;
    }

    public ProjectileMotionResult AdvanceRuntime(
        float deltaSeconds,
        IReadOnlyList<ProjectileHomingTarget>? homingTargets = null)
    {
        EnsureRuntimeIdentity();
        var result = RuntimeState.Advance(deltaSeconds, homingTargets);
        SynchronizeRuntimeState();
        return result;
    }

    public ProjectileTileCollisionResult ResolveTileCollision(ProjectileTileCollision collision)
    {
        var incomingVelocity = RuntimeState.Velocity;
        var result = RuntimeState.ResolveTileCollision(collision);
        _latestTileCollision = result;
        _latestTileCollisionIncomingVelocity = incomingVelocity;
        SynchronizeRuntimeState();
        return result;
    }

    public ProjectileEntityCollisionResult ResolveEntityCollision(ProjectileEntityCollision collision)
    {
        EnsureRuntimeIdentity();
        var result = RuntimeState.ResolveEntityCollision(collision);
        SynchronizeRuntimeState();
        return result;
    }

    internal ProjectileEntityCollisionResult ResolveEntityCollisionBeforeTile(
        ProjectileEntityCollision collision,
        Vector2 incomingVelocity)
    {
        EnsureRuntimeIdentity();
        var result = RuntimeState.ResolveEntityCollisionBeforeTile(collision, incomingVelocity);
        SynchronizeRuntimeState();
        return result;
    }

    internal ProjectileTileCollisionResult? SynchronizePhysicsState(
        GameWorld world,
        in PhysicsMoveResult physicsResult,
        ReadOnlySpan<PhysicsContact> tileContacts)
    {
        RuntimeState.SynchronizeWithPhysics(Body.Position, Body.Velocity);
        if (TryBuildProjectileTileCollision(tileContacts, physicsResult, out var collision))
        {
            return ResolveTileCollision(collision);
        }

        if (TryFindSolidTileCollision(
                world,
                RuntimeState.PreviousPosition,
                RuntimeState.Position,
                out collision))
        {
            return ResolveTileCollision(collision);
        }

        ClearLatestTileCollision();
        SynchronizeRuntimeState();
        return null;
    }

    internal bool HasPendingTileCollisionResult => _latestTileCollision is not null;

    internal bool TryConsumeLatestTileCollisionResult(out ProjectileTileCollisionResult collisionResult)
    {
        return TryConsumeLatestTileCollisionResult(
            out collisionResult,
            out _);
    }

    internal bool TryConsumeLatestTileCollisionResult(
        out ProjectileTileCollisionResult collisionResult,
        out Vector2 incomingVelocity)
    {
        if (_latestTileCollision is null)
        {
            collisionResult = default;
            incomingVelocity = Vector2.Zero;
            return false;
        }

        collisionResult = _latestTileCollision.Value;
        incomingVelocity = _latestTileCollisionIncomingVelocity;
        ClearLatestTileCollision();
        return true;
    }

    private void ClearLatestTileCollision()
    {
        _latestTileCollision = null;
        _latestTileCollisionIncomingVelocity = Vector2.Zero;
    }

    public void RegisterHit()
    {
        RuntimeState.RegisterUntrackedHit();
        SynchronizeRuntimeState();
    }

    private void EnsureRuntimeIdentity()
    {
        if (RuntimeState.InstanceId == 0 && Id > 0)
        {
            RuntimeState.BindInstanceId((ulong)Id);
        }
    }

    private void SynchronizeRuntimeState()
    {
        Position = RuntimeState.Position;
        Body.Position = RuntimeState.Position;
        Body.Velocity = RuntimeState.Velocity;
        IsActive = RuntimeState.IsActive;
    }

    private bool TryFindSolidTileCollision(
        GameWorld world,
        Vector2 start,
        Vector2 end,
        out ProjectileTileCollision collision)
    {
        var movement = end - start;
        var size = CreateBodySize(Definition);
        var halfSize = size * 0.5f;
        var startCenter = start + halfSize;
        var endCenter = end + halfSize;
        var startCell = CoordinateUtils.WorldToTile(startCenter.X, startCenter.Y);
        var endCell = CoordinateUtils.WorldToTile(endCenter.X, endCenter.Y);
        var paddingX = Math.Max(1, (int)MathF.Ceiling(halfSize.X / GameConstants.TileSize));
        var paddingY = Math.Max(1, (int)MathF.Ceiling(halfSize.Y / GameConstants.TileSize));
        var stepX = Math.Sign(movement.X);
        var stepY = Math.Sign(movement.Y);
        var currentX = startCell.X;
        var currentY = startCell.Y;
        var nextBoundaryTimeX = ResolveNextCellBoundaryTime(currentX, stepX, startCenter.X, movement.X);
        var nextBoundaryTimeY = ResolveNextCellBoundaryTime(currentY, stepY, startCenter.Y, movement.Y);
        var cellTravelTimeX = stepX == 0
            ? double.PositiveInfinity
            : GameConstants.TileSize / Math.Abs((double)movement.X);
        var cellTravelTimeY = stepY == 0
            ? double.PositiveInfinity
            : GameConstants.TileSize / Math.Abs((double)movement.Y);
        var found = false;
        var bestFraction = double.PositiveInfinity;
        var bestTileX = 0;
        var bestTileY = 0;
        ushort bestTileId = 0;
        var bestNormal = Vector2.Zero;

        while (true)
        {
            for (var offsetY = -paddingY; offsetY <= paddingY; offsetY++)
            {
                var candidateY = (long)currentY + offsetY;
                if (candidateY is < int.MinValue or > int.MaxValue)
                {
                    continue;
                }

                for (var offsetX = -paddingX; offsetX <= paddingX; offsetX++)
                {
                    var candidateX = (long)currentX + offsetX;
                    if (candidateX is < int.MinValue or > int.MaxValue ||
                        !world.TryGetTile((int)candidateX, (int)candidateY, out var tile) ||
                        !tile.IsSolid ||
                        !TrySweepAabbAgainstTile(
                            start,
                            size,
                            movement,
                            (int)candidateX,
                            (int)candidateY,
                            out var fraction,
                            out var normal) ||
                        !IsEarlierTileHit(
                            fraction,
                            (int)candidateX,
                            (int)candidateY,
                            bestFraction,
                            bestTileX,
                            bestTileY,
                            found))
                    {
                        continue;
                    }

                    found = true;
                    bestFraction = fraction;
                    bestTileX = (int)candidateX;
                    bestTileY = (int)candidateY;
                    bestTileId = tile.TileId;
                    bestNormal = normal;
                }
            }

            if (currentX == endCell.X && currentY == endCell.Y)
            {
                break;
            }

            if (nextBoundaryTimeX < nextBoundaryTimeY)
            {
                if (!TryAdvanceCell(ref currentX, stepX))
                {
                    break;
                }

                nextBoundaryTimeX += cellTravelTimeX;
            }
            else if (nextBoundaryTimeY < nextBoundaryTimeX)
            {
                if (!TryAdvanceCell(ref currentY, stepY))
                {
                    break;
                }

                nextBoundaryTimeY += cellTravelTimeY;
            }
            else
            {
                if (!TryAdvanceCell(ref currentX, stepX) ||
                    !TryAdvanceCell(ref currentY, stepY))
                {
                    break;
                }

                nextBoundaryTimeX += cellTravelTimeX;
                nextBoundaryTimeY += cellTravelTimeY;
            }
        }

        if (!found)
        {
            collision = default;
            return false;
        }

        collision = new ProjectileTileCollision(
            start + movement * (float)Math.Clamp(bestFraction, 0d, 1d),
            bestNormal,
            bestTileX,
            bestTileY,
            bestTileId);
        return true;
    }

    private static bool TrySweepAabbAgainstTile(
        Vector2 start,
        Vector2 size,
        Vector2 movement,
        int tileX,
        int tileY,
        out double fraction,
        out Vector2 normal)
    {
        var tileLeft = (double)tileX * GameConstants.TileSize;
        var tileTop = (double)tileY * GameConstants.TileSize;
        var tileRight = tileLeft + GameConstants.TileSize;
        var tileBottom = tileTop + GameConstants.TileSize;
        var startsOverlapping = start.X < tileRight &&
                                start.X + size.X > tileLeft &&
                                start.Y < tileBottom &&
                                start.Y + size.Y > tileTop;
        if (startsOverlapping)
        {
            fraction = 0d;
            normal = ResolveSurfaceNormal(movement);
            return normal != Vector2.Zero;
        }

        if (!TryResolveSweepAxis(
                start.X,
                size.X,
                movement.X,
                tileLeft,
                tileRight,
                out var entryX,
                out var exitX) ||
            !TryResolveSweepAxis(
                start.Y,
                size.Y,
                movement.Y,
                tileTop,
                tileBottom,
                out var entryY,
                out var exitY))
        {
            fraction = 0d;
            normal = Vector2.Zero;
            return false;
        }

        var entry = Math.Max(0d, Math.Max(entryX, entryY));
        var exit = Math.Min(1d, Math.Min(exitX, exitY));
        if (entry > exit || exit < 0d || entry > 1d ||
            entry <= 0.0000001d && exit <= 0.0000001d)
        {
            fraction = 0d;
            normal = Vector2.Zero;
            return false;
        }

        const double axisTieEpsilon = 0.0000001d;
        var horizontal = entryX > entryY + axisTieEpsilon ||
                         Math.Abs(entryX - entryY) <= axisTieEpsilon &&
                         Math.Abs(movement.X) >= Math.Abs(movement.Y);
        normal = horizontal
            ? new Vector2(movement.X > 0f ? -1f : 1f, 0f)
            : new Vector2(0f, movement.Y > 0f ? -1f : 1f);
        fraction = entry;
        return normal != Vector2.Zero;
    }

    private static bool TryResolveSweepAxis(
        double origin,
        double extent,
        double movement,
        double obstacleMinimum,
        double obstacleMaximum,
        out double entry,
        out double exit)
    {
        var expandedMinimum = obstacleMinimum - extent;
        if (Math.Abs(movement) <= double.Epsilon)
        {
            entry = double.NegativeInfinity;
            exit = double.PositiveInfinity;
            return origin >= expandedMinimum && origin <= obstacleMaximum;
        }

        var first = (expandedMinimum - origin) / movement;
        var second = (obstacleMaximum - origin) / movement;
        entry = Math.Min(first, second);
        exit = Math.Max(first, second);
        return true;
    }

    private static bool IsEarlierTileHit(
        double fraction,
        int tileX,
        int tileY,
        double bestFraction,
        int bestTileX,
        int bestTileY,
        bool found)
    {
        const double tieEpsilon = 0.0000001d;
        if (!found || fraction < bestFraction - tieEpsilon)
        {
            return true;
        }

        return Math.Abs(fraction - bestFraction) <= tieEpsilon &&
               (tileY < bestTileY || tileY == bestTileY && tileX < bestTileX);
    }

    private static double ResolveNextCellBoundaryTime(
        int cell,
        int step,
        float center,
        float movement)
    {
        if (step == 0)
        {
            return double.PositiveInfinity;
        }

        var boundaryCell = step > 0 ? (long)cell + 1L : cell;
        var boundary = boundaryCell * GameConstants.TileSize;
        return (boundary - center) / movement;
    }

    private static bool TryAdvanceCell(ref int cell, int step)
    {
        if (step == 0 ||
            step > 0 && cell == int.MaxValue ||
            step < 0 && cell == int.MinValue)
        {
            return false;
        }

        cell += step;
        return true;
    }

    private static Vector2 ResolveSurfaceNormal(Vector2 movement)
    {
        if (MathF.Abs(movement.X) >= MathF.Abs(movement.Y))
        {
            return movement.X >= 0 ? -Vector2.UnitX : Vector2.UnitX;
        }

        return movement.Y >= 0 ? -Vector2.UnitY : Vector2.UnitY;
    }

    private static Vector2 CreateBodySize(ProjectileDefinition definition)
    {
        var size = Math.Max(1, (int)MathF.Ceiling(definition.CollisionRadius * 2));
        return new Vector2(size, size);
    }

    private static bool TryBuildProjectileTileCollision(
        ReadOnlySpan<PhysicsContact> tileContacts,
        in PhysicsMoveResult physicsResult,
        out ProjectileTileCollision collision)
    {
        collision = default;
        if (tileContacts.Length == 0)
        {
            return false;
        }

        var bestTravelFraction = float.PositiveInfinity;
        var bestContact = default(PhysicsContact);
        var foundContact = false;
        for (var index = 0; index < tileContacts.Length; index++)
        {
            var contact = tileContacts[index];
            if (contact.TravelFraction >= bestTravelFraction)
            {
                continue;
            }

            bestTravelFraction = contact.TravelFraction;
            bestContact = contact;
            foundContact = true;
        }

        if (!foundContact)
        {
            return false;
        }

        var normal = bestContact.Normal;
        if (normal == Vector2.Zero)
        {
            normal = ResolveSurfaceNormal(physicsResult.RequestedDisplacement);
            if (normal == Vector2.Zero)
            {
                normal = ResolveSurfaceNormal(physicsResult.ActualDisplacement);
            }
        }

        if (normal == Vector2.Zero)
        {
            return false;
        }

        collision = new ProjectileTileCollision(
            bestContact.Point,
            normal,
            bestContact.TileX,
            bestContact.TileY,
            null);
        return true;
    }

    void IEntityPhysicsParticipant.SynchronizePhysicsState(
        GameWorld world,
        in PhysicsMoveResult moveResult,
        ReadOnlySpan<PhysicsContact> tileContacts)
    {
        _ = SynchronizePhysicsState(world, moveResult, tileContacts);
    }

    private static ProjectileDefinition CreateCompatibilityDefinition(
        string projectileId,
        Vector2 velocity,
        int damage,
        DamageType damageType,
        float gravity,
        int pierce,
        float lifetime)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectileId);
        return new ProjectileDefinition
        {
            Id = projectileId,
            TexturePath = projectileId,
            Speed = velocity.Length(),
            Damage = damage,
            DamageType = damageType,
            Gravity = gravity,
            Pierce = pierce,
            Lifetime = lifetime
        };
    }

    PhysicsBody IEntityPhysicsParticipant.Body => Body;

    internal TileCollisionSettings CollisionSettings { get; }

    TileCollisionSettings IEntityPhysicsParticipant.CollisionSettings => CollisionSettings;

    bool IContinuousCollisionPhysicsParticipant.UseContinuousCollision => true;
}
