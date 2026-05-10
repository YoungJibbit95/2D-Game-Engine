using Game.Core.World;

namespace Game.Core.Events;

public sealed record ChunkLoadedEvent(ChunkPos Position, bool LoadedFromSave) : IGameEvent;
