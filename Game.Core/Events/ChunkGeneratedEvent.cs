using Game.Core.World;

namespace Game.Core.Events;

public sealed record ChunkGeneratedEvent(ChunkPos Position) : IGameEvent;
