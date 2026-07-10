using Game.Core.Actions;
using Game.Core.World;
using System.Numerics;

namespace Game.Core.Events;

public sealed record PlayerItemUseCompletedEvent(
    int PlayerEntityId,
    string ItemId,
    PlayerItemUseKind Kind,
    GameplayActionSuccessReason Reason,
    TilePos TargetTile,
    Vector2 TargetWorldPosition,
    float CooldownDuration,
    int HealthRestored,
    int ManaRestored,
    int StatusEffectsApplied) : IGameEvent;
