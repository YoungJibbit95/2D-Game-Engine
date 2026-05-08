namespace Game.Core.Spawning;

public readonly record struct SpawnSchedulerResult(int Attempts, int Spawned, int Despawned)
{
    public static SpawnSchedulerResult None { get; } = new(0, 0, 0);
}
