namespace Game.Core.Commands;

public sealed record ParsedCommand(string Name, IReadOnlyList<string> Arguments);
