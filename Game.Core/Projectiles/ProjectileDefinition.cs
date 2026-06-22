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

    public int Pierce { get; init; }

    public required float Lifetime { get; init; }

    public IReadOnlyList<StatusEffectApplication> OnHitEffects { get; init; } = Array.Empty<StatusEffectApplication>();
}
