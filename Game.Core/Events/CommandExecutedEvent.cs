namespace Game.Core.Events;

public sealed record CommandExecutedEvent(string CommandName, bool Success, string Message) : IGameEvent;
