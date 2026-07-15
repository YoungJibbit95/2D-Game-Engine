namespace Game.Core.Combat;

public readonly record struct ContactDamageResult(
    int ContactHits,
    int DamageApplied,
    bool PlayerDied,
    CombatHitOutcome Outcome = CombatHitOutcome.NoDamage,
    float GuardStaminaSpent = 0,
    int DamagePrevented = 0)
{
    public static ContactDamageResult None { get; } = new(0, 0, false);

    public CombatHitResult? Resolution { get; init; }

    public bool Blocked => Outcome == CombatHitOutcome.Blocked;

    public bool Parried => Outcome == CombatHitOutcome.Parried;

    public bool GuardBroken => Outcome == CombatHitOutcome.GuardBroken;
}
