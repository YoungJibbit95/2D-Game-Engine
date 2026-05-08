namespace Game.Core.Events;

public sealed record PlayerDamagedEvent(int Damage, int CurrentHealth, int MaxHealth, int? SourceEntityId) : IGameEvent;
