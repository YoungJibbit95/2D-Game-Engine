namespace Game.Core.Events;

public sealed record ProjectileHitEvent(int ProjectileEntityId, int TargetEntityId, int Damage) : IGameEvent;
