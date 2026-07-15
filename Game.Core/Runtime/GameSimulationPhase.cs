namespace Game.Core.Runtime;

public enum GameSimulationPhase
{
    PlayerCommand,
    WorldTimeAndFarming,
    LivingWorld,
    Player,
    PlayerItemUse,
    Entities,
    EntityAttacks,
    Combat,
    EntityDeaths,
    PickupMagnetism,
    Pickups,
    Spawning,
    Respawn,
    WorldSimulation,
    Lighting,
    FrameSnapshot
}
