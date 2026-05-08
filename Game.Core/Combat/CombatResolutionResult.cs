namespace Game.Core.Combat;

public readonly record struct CombatResolutionResult(int ProjectileHits, int EnemyDeaths, int DroppedItems)
{
    public static CombatResolutionResult None { get; } = new(0, 0, 0);
}
