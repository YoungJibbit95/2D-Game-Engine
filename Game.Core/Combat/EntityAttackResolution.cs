namespace Game.Core.Combat;

public readonly record struct EntityAttackResolution(
    int IntentsConsumed,
    int HitsApplied,
    int DamageApplied,
    int Deaths)
{
    public static EntityAttackResolution None { get; } = new(0, 0, 0, 0);
}
