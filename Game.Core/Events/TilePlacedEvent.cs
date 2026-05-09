using Game.Core.World;

namespace Game.Core.Events;

public sealed record TilePlacedEvent(TilePos Position, ushort TileId, string ItemId) : IGameEvent;
