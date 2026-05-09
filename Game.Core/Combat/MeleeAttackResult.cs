namespace Game.Core.Combat;

public readonly record struct MeleeAttackResult(
    bool Attacked,
    int Hits,
    int EnemyDeaths,
    int DroppedItems,
    int StatusEffectsApplied = 0)
{
    public static MeleeAttackResult None { get; } = new(false, 0, 0, 0);
}
