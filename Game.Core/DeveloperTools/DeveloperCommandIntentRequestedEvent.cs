using Game.Core.Events;

namespace Game.Core.DeveloperTools;

public sealed record DeveloperCommandIntentRequestedEvent(
    string CommandName,
    IDeveloperCommandIntent Intent) : IGameEvent;
