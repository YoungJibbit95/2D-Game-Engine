using Game.Core.Combat;
using Game.Core.Entities;
using Game.Core.Spawning;
using Game.Core.World.Simulation;

namespace Game.Core.Runtime;

public readonly record struct GameSimulationTickResult(
    CombatResolutionResult Combat,
    ContactDamageResult ContactDamage,
    int PickedUpItems,
    SpawnSchedulerResult Spawning,
    PlayerRespawnResult Respawn,
    WorldSimulationTickResult WorldSimulation);
