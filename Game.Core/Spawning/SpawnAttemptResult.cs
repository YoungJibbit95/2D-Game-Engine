using Game.Core.Entities;

namespace Game.Core.Spawning;

public readonly record struct SpawnAttemptResult(bool Spawned, string? RuleId, EnemyEntity? Entity)
{
    public static SpawnAttemptResult None { get; } = new(false, null, null);
}
