using Game.Core.World;

namespace Game.Core.Events;

public sealed record ChunkSavedEvent(ChunkPos Position, bool SavedBeforeUnload) : IGameEvent;
