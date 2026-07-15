using Game.Core.Combat;
using Game.Core.Entities;
using Game.Core.World;
using System.Numerics;
using GameWorld = Game.Core.World.World;

namespace Game.Core.Projectiles;

public sealed class ProjectileEntity : Entity
{
    private IReadOnlyList<ProjectileHomingTarget>? _homingTargetsForNextUpdate;

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
        Position = position;
        IsActive = RuntimeState.IsActive;
    }

    public ProjectileRuntimeState RuntimeState { get; }

    public ProjectileDefinition Definition => RuntimeState.Definition;

    public string ProjectileId => Definition.Id;

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
            return;
        }

        ResolveTileCollision(collision);
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
        var result = RuntimeState.ResolveTileCollision(collision);
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
        IsActive = RuntimeState.IsActive;
    }

    private bool TryFindSolidTileCollision(
        GameWorld world,
        Vector2 start,
        Vector2 end,
        out ProjectileTileCollision collision)
    {
        var movement = end - start;
        var distance = movement.Length();
        var stepLength = Math.Max(1, Definition.CollisionRadius);
        var steps = Math.Clamp((int)MathF.Ceiling(distance / stepLength), 1, 1024);
        var previous = start;
        for (var step = 1; step <= steps; step++)
        {
            var amount = step / (float)steps;
            var candidate = start + movement * amount;
            if (!OverlapsSolidTile(world, candidate))
            {
                previous = candidate;
                continue;
            }

            collision = new ProjectileTileCollision(previous, ResolveSurfaceNormal(movement));
            return true;
        }

        collision = default;
        return false;
    }

    private bool OverlapsSolidTile(GameWorld world, Vector2 position)
    {
        var size = Math.Max(1, (int)MathF.Ceiling(Definition.CollisionRadius * 2));
        var bounds = new RectI(
            (int)MathF.Floor(position.X),
            (int)MathF.Floor(position.Y),
            size,
            size);
        var min = CoordinateUtils.WorldToTile(bounds.Left, bounds.Top);
        var max = CoordinateUtils.WorldToTile(bounds.Right - 1, bounds.Bottom - 1);

        for (var y = min.Y; y <= max.Y; y++)
        {
            for (var x = min.X; x <= max.X; x++)
            {
                if (world.IsSolid(x, y))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static Vector2 ResolveSurfaceNormal(Vector2 movement)
    {
        if (MathF.Abs(movement.X) >= MathF.Abs(movement.Y))
        {
            return movement.X >= 0 ? -Vector2.UnitX : Vector2.UnitX;
        }

        return movement.Y >= 0 ? -Vector2.UnitY : Vector2.UnitY;
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
}
