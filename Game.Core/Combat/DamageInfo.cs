using System.Numerics;

namespace Game.Core.Combat;

public readonly record struct DamageInfo(
    int Amount,
    DamageType Type,
    int? SourceEntityId,
    Vector2 KnockbackDirection,
    float KnockbackForce);
