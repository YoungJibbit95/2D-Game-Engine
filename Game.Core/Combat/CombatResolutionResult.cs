namespace Game.Core.Combat;

public readonly record struct CombatResolutionResult(
    int ProjectileHits,
    int EnemyDeaths,
    int DroppedItems,
    int StatusEffectsApplied = 0)
{
    public static CombatResolutionResult None { get; } = new(0, 0, 0);
}
