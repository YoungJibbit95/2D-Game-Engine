using Game.Core.World;

namespace Game.Core.Events;

public sealed record MiningBlockedEvent(
    TilePos Position,
    ushort TileId,
    GameplayActionFailureReason Reason) : IGameEvent;
