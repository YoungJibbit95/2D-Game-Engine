using Game.Core.Commands;
using Game.Core.DeveloperTools;

namespace Game.Client.DeveloperTools;

public sealed class DeveloperConsoleAdapter
{
    private readonly CommandRegistry _registry;
    private readonly CommandHistory _history;
    private readonly CommandDispatcher _dispatcher;
    private readonly CommandSuggestionService _suggestions;
    private readonly CommandHelpService _help;

    public DeveloperConsoleAdapter(CommandRegistry? registry = null, int historyCapacity = 100)
    {
        _registry = registry ?? CommandRegistry.CreateDefault();
        _history = new CommandHistory(historyCapacity);
        _dispatcher = new CommandDispatcher(_registry, history: _history);
        _suggestions = new CommandSuggestionService(_registry);
        _help = new CommandHelpService(_registry);
    }

    public IReadOnlyList<CommandHistoryEntry> History => _history.Entries;

    public CommandResult Execute(string input, CommandContext context)
    {
        return _dispatcher.Execute(input, context);
    }

    public IReadOnlyList<CommandSuggestion> Suggest(string input, CommandContext context, int maximum = 6)
    {
        return _suggestions.GetSuggestions(input, context, maximum);
    }

    public string ApplySuggestion(string input, CommandSuggestion suggestion)
    {
        ArgumentNullException.ThrowIfNull(input);
        var start = Math.Clamp(suggestion.ReplacementStart, 0, input.Length);
        var length = Math.Clamp(suggestion.ReplacementLength, 0, input.Length - start);
        var prefix = input[..start];
        if (suggestion.Kind is CommandSuggestionKind.Command or CommandSuggestionKind.Alias && prefix.Length == 0)
        {
            prefix = "/";
        }

        var suffix = input[(start + length)..];
        return prefix + suggestion.ReplacementText + suffix +
            (suffix.Length == 0 && suggestion.Kind is CommandSuggestionKind.Command or CommandSuggestionKind.Alias ? " " : string.Empty);
    }

    public string? PreviousHistory() => _history.Previous()?.Input;

    public string? NextHistory() => _history.Next()?.Input;

    public string Help(string? command = null)
    {
        return string.IsNullOrWhiteSpace(command)
            ? _help.BuildOverview()
            : _help.TryBuildCommandHelp(command.TrimStart('/'), out var help)
                ? help
                : $"No help found for '{command}'.";
    }

    public ConsoleCommandDetails? Describe(string input, CommandSuggestion? selectedSuggestion = null)
    {
        ArgumentNullException.ThrowIfNull(input);
        var commandName = ExtractCommandName(input);
        IConsoleCommand? command = null;
        if (commandName.Length > 0)
        {
            _registry.TryGet(commandName, out command);
        }

        if (command is null &&
            selectedSuggestion is { Kind: CommandSuggestionKind.Command or CommandSuggestionKind.Alias })
        {
            commandName = selectedSuggestion.ReplacementText;
            if (!_registry.TryGet(commandName, out command))
            {
                return null;
            }
        }

        if (command is null)
        {
            return null;
        }

        var specification = _registry.GetSpecification(command);
        _help.TryBuildCommandHelp(commandName, out var help);
        return new ConsoleCommandDetails(specification.Usage, specification.Description, help);
    }

    private static string ExtractCommandName(string input)
    {
        var start = input.StartsWith("/", StringComparison.Ordinal) ? 1 : 0;
        while (start < input.Length && char.IsWhiteSpace(input[start]))
        {
            start++;
        }

        var end = start;
        while (end < input.Length && !char.IsWhiteSpace(input[end]))
        {
            end++;
        }

        return end == start ? string.Empty : input[start..end];
    }
}

public sealed record ConsoleCommandDetails(string Signature, string Description, string HelpText);
