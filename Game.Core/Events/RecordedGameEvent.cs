namespace Game.Core.Events;

public sealed record RecordedGameEvent(long Sequence, DateTimeOffset RecordedAtUtc, IGameEvent Event);
