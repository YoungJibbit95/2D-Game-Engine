namespace Game.Core.Events;

public sealed record ResourceRestoredEvent(
    int EntityId,
    string SourceItemId,
    int HealthRestored,
    int ManaRestored) : IGameEvent;
