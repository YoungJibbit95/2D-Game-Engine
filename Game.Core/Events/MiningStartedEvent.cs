using Game.Core.World;

namespace Game.Core.Events;

public sealed record MiningStartedEvent(TilePos Position, ushort TileId) : IGameEvent;
