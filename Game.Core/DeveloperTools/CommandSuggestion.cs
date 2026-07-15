namespace Game.Core.DeveloperTools;

public enum CommandSuggestionKind
{
    Command,
    Alias,
    Argument
}

public sealed record CommandSuggestion(
    string ReplacementText,
    string DisplayText,
    string Description,
    int ReplacementStart,
    int ReplacementLength,
    CommandSuggestionKind Kind);
