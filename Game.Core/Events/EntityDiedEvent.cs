namespace Game.Core.Events;

public sealed record EntityDiedEvent(int EntityId, string DefinitionId) : IGameEvent;
