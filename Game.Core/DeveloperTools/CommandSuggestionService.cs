using Game.Core.Commands;

namespace Game.Core.DeveloperTools;

public sealed class CommandSuggestionService
{
    private readonly CommandRegistry _registry;
    private readonly CommandParser _parser;

    public CommandSuggestionService(CommandRegistry registry, CommandParser? parser = null)
    {
        ArgumentNullException.ThrowIfNull(registry);
        _registry = registry;
        _parser = parser ?? new CommandParser();
    }

    public IReadOnlyList<CommandSuggestion> GetSuggestions(
        string? input,
        CommandContext context,
        int maximum = 20)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (maximum <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximum), "Maximum suggestions must be greater than zero.");
        }

        var text = input ?? string.Empty;
        var commandTextStart = text.StartsWith("/", StringComparison.Ordinal) ? 1 : 0;
        var commandText = text[commandTextStart..];
        var hasArgumentBoundary = commandText.Any(char.IsWhiteSpace);

        if (!hasArgumentBoundary)
        {
            return SuggestCommands(commandText, commandTextStart, maximum);
        }

        if (!_parser.TryParse(text, out var parsed) || !_registry.TryGet(parsed.Name, out var command))
        {
            return Array.Empty<CommandSuggestion>();
        }

        var specification = _registry.GetSpecification(command);
        var endsWithWhitespace = text.Length > 0 && char.IsWhiteSpace(text[^1]);
        var argumentIndex = endsWithWhitespace ? parsed.Arguments.Count : parsed.Arguments.Count - 1;
        if (argumentIndex < 0 || argumentIndex >= specification.Arguments.Count)
        {
            return Array.Empty<CommandSuggestion>();
        }

        var prefix = endsWithWhitespace ? string.Empty : parsed.Arguments[^1];
        var replacementStart = endsWithWhitespace ? text.Length : FindTokenStart(text);
        var values = GetArgumentValues(specification.Arguments[argumentIndex], context);
        return values
            .Where(value => value.Value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .OrderBy(value => value.Value, StringComparer.OrdinalIgnoreCase)
            .Take(maximum)
            .Select(value => new CommandSuggestion(
                value.Value,
                value.Value,
                value.Description,
                replacementStart,
                text.Length - replacementStart,
                CommandSuggestionKind.Argument))
            .ToArray();
    }

    private IReadOnlyList<CommandSuggestion> SuggestCommands(string prefix, int start, int maximum)
    {
        var suggestions = new List<CommandSuggestion>();
        foreach (var command in _registry.Commands)
        {
            if (command.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                suggestions.Add(new CommandSuggestion(
                    command.Name,
                    command.Name,
                    command.Description,
                    start,
                    prefix.Length,
                    CommandSuggestionKind.Command));
            }

            foreach (var alias in command.Aliases)
            {
                if (alias.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    suggestions.Add(new CommandSuggestion(
                        alias,
                        alias,
                        $"Alias for /{command.Name}",
                        start,
                        prefix.Length,
                        CommandSuggestionKind.Alias));
                }
            }
        }

        return suggestions
            .OrderBy(suggestion => suggestion.DisplayText, StringComparer.OrdinalIgnoreCase)
            .Take(maximum)
            .ToArray();
    }

    private static IEnumerable<SuggestionValue> GetArgumentValues(
        CommandArgumentSpecification argument,
        CommandContext context)
    {
        foreach (var choice in argument.Choices)
        {
            yield return new SuggestionValue(choice, argument.Description);
        }

        if (argument.Type == CommandArgumentType.Boolean)
        {
            yield return new SuggestionValue("on", "Enable the setting.");
            yield return new SuggestionValue("off", "Disable the setting.");
            yield return new SuggestionValue("toggle", "Toggle the setting.");
        }

        if (argument.SuggestionSource == CommandSuggestionSource.Items && context.Content is not null)
        {
            foreach (var item in context.Content.Items.Definitions)
            {
                yield return new SuggestionValue(item.Id, item.DisplayName);
            }
        }
        else if (argument.SuggestionSource == CommandSuggestionSource.Entities && context.Content is not null)
        {
            foreach (var entity in context.Content.Entities.Definitions)
            {
                yield return new SuggestionValue(entity.Id, entity.DisplayName);
            }
        }
        else if (argument.SuggestionSource == CommandSuggestionSource.LoadedEntities && context.EntityManager is not null)
        {
            foreach (var entity in context.EntityManager.Entities)
            {
                yield return new SuggestionValue($"#{entity.Id}", entity.GetType().Name);
            }
        }
    }

    private static int FindTokenStart(string input)
    {
        for (var index = input.Length - 1; index >= 0; index--)
        {
            if (char.IsWhiteSpace(input[index]))
            {
                return index + 1;
            }
        }

        return input.StartsWith("/", StringComparison.Ordinal) ? 1 : 0;
    }

    private readonly record struct SuggestionValue(string Value, string Description);
}
