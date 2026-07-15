using Game.Core.Actions;
using Game.Core.Combat;
using Game.Core.Entities;
using Game.Core.Farming;
using Game.Core.Spawning;
using Game.Core.World.Simulation;

namespace Game.Core.Runtime;

public sealed record GameSimulationTickResult(
    bool DidTick,
    long TickNumber,
    ImmutableSnapshotList<GameSimulationPhase> ObservedPhases,
    EntityAttackResolution EntityAttacks,
    CombatResolutionResult Combat,
    ContactDamageResult ContactDamage,
    EntityLifecycleResolution EntityLifecycle,
    int PickedUpItems,
    SpawnSchedulerResult Spawning,
    PlayerRespawnResult Respawn,
    WorldSimulationTickResult WorldSimulation,
    int DaysAdvanced,
    FarmDailyTickResult Farming,
    PlayerItemUseResult ItemUse,
    GameFrameSnapshot Snapshot);
