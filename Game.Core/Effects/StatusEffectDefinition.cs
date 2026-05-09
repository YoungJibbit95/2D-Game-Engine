namespace Game.Core.Effects;

public sealed record StatusEffectDefinition
{
    public required string Id { get; init; }

    public required string DisplayName { get; init; }

    public StatusEffectKind Kind { get; init; }

    public float DurationSeconds { get; init; }

    public float TickIntervalSeconds { get; init; } = 1f;

    public int DamagePerTick { get; init; }

    public int HealPerTick { get; init; }

    public int MaxHealthBonus { get; init; }

    public int DefenseDelta { get; init; }

    public float MovementSpeedBonus { get; init; }

    public float MeleeDamageBonus { get; init; }

    public float RangedDamageBonus { get; init; }

    public float MiningSpeedBonus { get; init; }
}
