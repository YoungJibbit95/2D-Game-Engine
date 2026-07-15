namespace Game.Core.Commands;

public sealed record CommandValidationIssue(string Code, string ArgumentName, string Message);
