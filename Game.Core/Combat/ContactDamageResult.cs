namespace Game.Core.Combat;

public readonly record struct ContactDamageResult(int ContactHits, int DamageApplied, bool PlayerDied)
{
    public static ContactDamageResult None { get; } = new(0, 0, false);
}
