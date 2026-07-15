namespace Game.Core.Entities;

public enum EntityDespawnMode
{
    Distance,
    WhenIdle,
    Never
}

public sealed record EntityDespawnPolicyDefinition
{
    public EntityDespawnMode Mode { get; init; } = EntityDespawnMode.Distance;

    public float SpawnProtectionSeconds { get; init; }

    public float DamageProtectionSeconds { get; init; } = 10f;
}
