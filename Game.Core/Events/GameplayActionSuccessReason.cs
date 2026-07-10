namespace Game.Core.Events;

public enum GameplayActionSuccessReason
{
    None,
    ActionStarted,
    ActionProgressed,
    TileMined,
    TilePlaced,
    AttackPerformed,
    ProjectileSpawned,
    ConsumableApplied,
    FarmingCompleted
}
