using Game.Core.Saving;

namespace Game.Core.Events;

public sealed record GameSavedEvent(GameSaveResult Result) : IGameEvent;
