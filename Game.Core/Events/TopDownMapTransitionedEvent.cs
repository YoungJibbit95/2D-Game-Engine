using Game.Core.Maps;

namespace Game.Core.Events;

public sealed record TopDownMapTransitionedEvent(TopDownMapTransitionResult Result) : IGameEvent;
