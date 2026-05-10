using Game.Core.Saving;

namespace Game.Core.Events;

public sealed record GameLoadedEvent(GameLoadResult Result) : IGameEvent;
