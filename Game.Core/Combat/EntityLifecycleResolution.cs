namespace Game.Core.Combat;

public readonly record struct EntityLifecycleResolution(int DeathsProcessed, int DroppedStacks)
{
    public static EntityLifecycleResolution None { get; } = new(0, 0);
}
