using Game.Core.World;

namespace Game.Core.Events;

public sealed record MiningProgressEvent(
    TilePos Position,
    ushort TileId,
    float PreviousProgress,
    float Progress) : IGameEvent;
