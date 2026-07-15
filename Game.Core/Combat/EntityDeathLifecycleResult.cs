namespace Game.Core.Combat;

public readonly record struct EntityDeathLifecycleResult(
    bool Processed,
    int VictimEntityId,
    int DroppedStacks)
{
    public static EntityDeathLifecycleResult None { get; } = new(false, 0, 0);
}
