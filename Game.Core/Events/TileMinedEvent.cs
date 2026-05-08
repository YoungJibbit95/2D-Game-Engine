using Game.Core.Inventory;
using Game.Core.World;

namespace Game.Core.Events;

public sealed record TileMinedEvent(TilePos Position, ushort TileId, ItemStack DroppedItem) : IGameEvent;
