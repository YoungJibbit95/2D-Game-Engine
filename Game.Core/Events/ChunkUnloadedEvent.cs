using Game.Core.World;

namespace Game.Core.Events;

public sealed record ChunkUnloadedEvent(ChunkPos Position) : IGameEvent;
