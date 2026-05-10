using Game.Core.World;

namespace Game.Core.Events;

public sealed record ChunkUnloadSkippedEvent(ChunkPos Position, string Reason) : IGameEvent;
