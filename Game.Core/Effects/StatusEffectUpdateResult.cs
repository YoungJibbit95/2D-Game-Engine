namespace Game.Core.Effects;

public readonly record struct StatusEffectUpdateResult(
    int DamageApplied,
    int HealingApplied,
    int ExpiredCount)
{
    public static StatusEffectUpdateResult None { get; } = new(0, 0, 0);
}
