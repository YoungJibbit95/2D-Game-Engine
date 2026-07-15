using Game.Core.WorldEvents;

namespace Game.Core.Events;

public sealed record WorldEventPlayerActionEvaluatedEvent(
    long Sequence,
    WorldEventPlayerActionKind Action,
    bool Activated,
    string? EventId) : IGameEvent;

public sealed record WorldEventActivatedEvent(
    string EventId,
    WorldEventActivationSource Source,
    WorldEventPlayerActionKind? TriggerAction,
    long TriggerSequence) : IGameEvent;
