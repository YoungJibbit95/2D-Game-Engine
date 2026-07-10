using Game.Core.Actions;
using Game.Core.World;
using System.Numerics;

namespace Game.Core.Events;

public sealed record PlayerItemUseBlockedEvent(
    int PlayerEntityId,
    string? ItemId,
    PlayerItemUseKind AttemptedKind,
    GameplayActionFailureReason Reason,
    TilePos TargetTile,
    Vector2 TargetWorldPosition,
    float CooldownRemaining,
    float CooldownDuration,
    float ActionProgress) : IGameEvent;
