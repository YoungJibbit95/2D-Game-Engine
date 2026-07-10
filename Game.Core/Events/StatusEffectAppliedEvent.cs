namespace Game.Core.Events;

public sealed record StatusEffectAppliedEvent(
    int TargetEntityId,
    string EffectId,
    StatusEffectSourceKind SourceKind,
    string SourceId,
    bool Refreshed,
    float DurationSeconds) : IGameEvent;
