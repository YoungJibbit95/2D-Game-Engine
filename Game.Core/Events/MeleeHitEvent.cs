namespace Game.Core.Events;

public sealed record MeleeHitEvent(int SourceEntityId, int TargetEntityId, int Damage) : IGameEvent;
