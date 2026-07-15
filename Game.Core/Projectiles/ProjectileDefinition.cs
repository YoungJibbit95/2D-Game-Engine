using Game.Core.Effects;
using Game.Core.Combat;

namespace Game.Core.Projectiles;

public sealed record ProjectileDefinition
{
    public required string Id { get; init; }

    public required string TexturePath { get; init; }

    public required float Speed { get; init; }

    public required int Damage { get; init; }

    public DamageType DamageType { get; init; } = DamageType.Ranged;

    public float Gravity { get; init; }

    public float DragPerSecond { get; init; }

    public float HomingTurnRateRadiansPerSecond { get; init; }

    public float HomingRange { get; init; }

    public int Pierce { get; init; }

    public int BounceCount { get; init; }

    public float BounceRestitution { get; init; } = 1;

    public float CollisionRadius { get; init; } = 2;

    public ProjectileTileCollisionBehavior TileCollisionBehavior { get; init; } =
        ProjectileTileCollisionBehavior.Destroy;

    public ProjectileEntityCollisionBehavior EntityCollisionBehavior { get; init; } =
        ProjectileEntityCollisionBehavior.Damage;

    public bool FriendlyFire { get; init; }

    public bool HitOncePerTarget { get; init; } = true;

    public float Knockback { get; init; } = 1;

    public float CriticalChance { get; init; }

    public float CriticalMultiplier { get; init; } = 2;

    public required float Lifetime { get; init; }

    public IReadOnlyList<StatusEffectApplication> OnHitEffects { get; init; } = Array.Empty<StatusEffectApplication>();
}
