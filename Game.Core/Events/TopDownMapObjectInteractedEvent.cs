using Game.Core.Maps;

namespace Game.Core.Events;

public sealed record TopDownMapObjectInteractedEvent(
    string MapId,
    string ObjectId,
    MapObjectKind ObjectKind,
    TopDownMapObjectActionKind ActionKind,
    string? InteractionId,
    string? PayloadId) : IGameEvent;
