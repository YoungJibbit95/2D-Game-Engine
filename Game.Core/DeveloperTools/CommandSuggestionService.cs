using Game.Core.Commands;

namespace Game.Core.DeveloperTools;

public sealed class CommandSuggestionService
{
    private const int InitialScratchCapacity = 32;
    private readonly CommandRegistry _registry;
    private readonly CommandParser _parser;
    private ScoredSuggestion[] _scratch = new ScoredSuggestion[InitialScratchCapacity];
    private int _scratchCount;

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

        EnsureScratchCapacity(maximum);
        _scratchCount = 0;

        var text = input ?? string.Empty;
        var token = FindCommandToken(text);
        var commandPrefix = text.AsSpan(token.Start, token.Length);
        if (!token.HasArgumentBoundary)
        {
            SuggestCommands(commandPrefix, token.Start, token.Length, maximum);
            return CopySuggestions();
        }

        var commandName = commandPrefix.ToString();
        if (!_registry.TryGet(commandName, out var command))
        {
            SuggestCommands(commandPrefix, token.Start, token.Length, maximum);
            return CopySuggestions();
        }

        if (!_parser.TryParse(text, out var parsed))
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

        var prefix = endsWithWhitespace ? ReadOnlySpan<char>.Empty : parsed.Arguments[^1].AsSpan();
        var replacementStart = endsWithWhitespace ? text.Length : FindTokenStart(text);
        var replacementLength = text.Length - replacementStart;
        SuggestArgument(
            specification.Arguments[argumentIndex],
            context,
            parsed.Arguments,
            argumentIndex,
            prefix,
            replacementStart,
            replacementLength,
            maximum);
        return CopySuggestions();
    }

    private void SuggestCommands(
        ReadOnlySpan<char> prefix,
        int replacementStart,
        int replacementLength,
        int maximum)
    {
        for (var commandIndex = 0; commandIndex < _registry.Commands.Count; commandIndex++)
        {
            var command = _registry.Commands[commandIndex];
            AddCandidate(
                command.Name,
                command.Name,
                command.Description,
                prefix,
                replacementStart,
                replacementLength,
                CommandSuggestionKind.Command,
                maximum,
                scoreBonus: 0);

            for (var aliasIndex = 0; aliasIndex < command.Aliases.Count; aliasIndex++)
            {
                var alias = command.Aliases[aliasIndex];
                AddCandidate(
                    alias,
                    alias,
                    $"Alias for /{command.Name}",
                    prefix,
                    replacementStart,
                    replacementLength,
                    CommandSuggestionKind.Alias,
                    maximum,
                    scoreBonus: 0);
            }
        }
    }

    private void SuggestArgument(
        CommandArgumentSpecification argument,
        CommandContext context,
        IReadOnlyList<string> parsedArguments,
        int argumentIndex,
        ReadOnlySpan<char> prefix,
        int replacementStart,
        int replacementLength,
        int maximum)
    {
        for (var index = 0; index < argument.Choices.Count; index++)
        {
            AddCandidate(
                argument.Choices[index],
                argument.Choices[index],
                argument.Description,
                prefix,
                replacementStart,
                replacementLength,
                CommandSuggestionKind.Argument,
                maximum);
        }

        if (argument.Type == CommandArgumentType.Boolean)
        {
            AddArgumentValue("on", "Enable the setting.", prefix, replacementStart, replacementLength, maximum);
            AddArgumentValue("off", "Disable the setting.", prefix, replacementStart, replacementLength, maximum);
            AddArgumentValue("toggle", "Toggle the setting.", prefix, replacementStart, replacementLength, maximum);
        }

        switch (argument.SuggestionSource)
        {
            case CommandSuggestionSource.Items when context.Content is not null:
                foreach (var item in context.Content.Items.Definitions)
                {
                    AddArgumentValue(item.Id, item.DisplayName, prefix, replacementStart, replacementLength, maximum);
                }

                break;
            case CommandSuggestionSource.Entities when context.Content is not null:
                foreach (var entity in context.Content.Entities.Definitions)
                {
                    AddArgumentValue(entity.Id, entity.DisplayName, prefix, replacementStart, replacementLength, maximum);
                }

                break;
            case CommandSuggestionSource.LoadedEntities when context.EntityManager is not null:
                foreach (var entity in context.EntityManager.Entities)
                {
                    AddArgumentValue(
                        $"#{entity.Id}",
                        entity.GetType().Name,
                        prefix,
                        replacementStart,
                        replacementLength,
                        maximum);
                }

                break;
            case CommandSuggestionSource.Biomes when context.Content is not null:
                foreach (var biome in context.Content.Biomes.Definitions)
                {
                    AddArgumentValue(biome.Id, biome.DisplayName, prefix, replacementStart, replacementLength, maximum);
                }

                break;
            case CommandSuggestionSource.Projectiles when context.Content is not null:
                foreach (var projectile in context.Content.Projectiles.Definitions)
                {
                    AddArgumentValue(
                        projectile.Id,
                        "Projectile definition",
                        prefix,
                        replacementStart,
                        replacementLength,
                        maximum);
                }

                break;
            case CommandSuggestionSource.WorldEvents when context.Content is not null:
                foreach (var worldEvent in context.Content.WorldEvents.Definitions)
                {
                    AddArgumentValue(
                        worldEvent.Id,
                        worldEvent.DisplayName,
                        prefix,
                        replacementStart,
                        replacementLength,
                        maximum);
                }

                break;
            case CommandSuggestionSource.Commands:
                for (var commandIndex = 0; commandIndex < _registry.Commands.Count; commandIndex++)
                {
                    var command = _registry.Commands[commandIndex];
                    AddArgumentValue(
                        command.Name,
                        command.Description,
                        prefix,
                        replacementStart,
                        replacementLength,
                        maximum);
                }

                break;
            case CommandSuggestionSource.GameRuleValues when argumentIndex > 0 && parsedArguments.Count > 0:
                var values = DeveloperCommandVocabulary.GetGameRuleValueSuggestions(parsedArguments[0]);
                for (var valueIndex = 0; valueIndex < values.Count; valueIndex++)
                {
                    AddArgumentValue(
                        values[valueIndex],
                        $"Value for {parsedArguments[0]}.",
                        prefix,
                        replacementStart,
                        replacementLength,
                        maximum);
                }

                break;
            case CommandSuggestionSource.GameEvents:
                for (var eventIndex = 0; eventIndex < DeveloperCommandVocabulary.EventTypeNames.Count; eventIndex++)
                {
                    AddArgumentValue(
                        DeveloperCommandVocabulary.EventTypeNames[eventIndex],
                        "Game event type.",
                        prefix,
                        replacementStart,
                        replacementLength,
                        maximum);
                }

                break;
        }
    }

    private void AddArgumentValue(
        string value,
        string description,
        ReadOnlySpan<char> prefix,
        int replacementStart,
        int replacementLength,
        int maximum)
    {
        AddCandidate(
            value,
            value,
            description,
            prefix,
            replacementStart,
            replacementLength,
            CommandSuggestionKind.Argument,
            maximum);
    }

    private void AddCandidate(
        string replacement,
        string display,
        string description,
        ReadOnlySpan<char> prefix,
        int replacementStart,
        int replacementLength,
        CommandSuggestionKind kind,
        int maximum,
        int scoreBonus = 0)
    {
        if (ContainsReplacement(replacement))
        {
            return;
        }

        var score = TolerantCommandMatcher.Score(prefix, replacement);
        if (score < 0)
        {
            var descriptionScore = TolerantCommandMatcher.Score(prefix, description);
            if (descriptionScore < 0)
            {
                return;
            }

            score = descriptionScore - 80;
        }

        var ranked = new ScoredSuggestion(
            score + scoreBonus,
            new CommandSuggestion(
                replacement,
                display,
                description,
                replacementStart,
                replacementLength,
                kind));

        var insertion = 0;
        while (insertion < _scratchCount && !IsBetter(ranked, _scratch[insertion]))
        {
            insertion++;
        }

        if (insertion >= maximum)
        {
            return;
        }

        var last = Math.Min(_scratchCount, maximum - 1);
        for (var index = last; index > insertion; index--)
        {
            _scratch[index] = _scratch[index - 1];
        }

        _scratch[insertion] = ranked;
        _scratchCount = Math.Min(_scratchCount + 1, maximum);
    }

    private bool ContainsReplacement(string replacement)
    {
        for (var index = 0; index < _scratchCount; index++)
        {
            if (_scratch[index].Suggestion.ReplacementText.Equals(
                    replacement,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private IReadOnlyList<CommandSuggestion> CopySuggestions()
    {
        if (_scratchCount == 0)
        {
            return Array.Empty<CommandSuggestion>();
        }

        var result = new CommandSuggestion[_scratchCount];
        for (var index = 0; index < _scratchCount; index++)
        {
            result[index] = _scratch[index].Suggestion;
        }

        return result;
    }

    private void EnsureScratchCapacity(int maximum)
    {
        if (_scratch.Length >= maximum)
        {
            return;
        }

        Array.Resize(ref _scratch, Math.Max(maximum, _scratch.Length * 2));
    }

    private static bool IsBetter(ScoredSuggestion candidate, ScoredSuggestion current)
    {
        if (candidate.Score != current.Score)
        {
            return candidate.Score > current.Score;
        }

        return string.Compare(
            candidate.Suggestion.DisplayText,
            current.Suggestion.DisplayText,
            StringComparison.OrdinalIgnoreCase) < 0;
    }

    private static CommandToken FindCommandToken(string input)
    {
        var start = 0;
        while (start < input.Length && char.IsWhiteSpace(input[start]))
        {
            start++;
        }

        if (start < input.Length && input[start] == '/')
        {
            start++;
        }

        while (start < input.Length && char.IsWhiteSpace(input[start]))
        {
            start++;
        }

        var end = start;
        while (end < input.Length && !char.IsWhiteSpace(input[end]))
        {
            end++;
        }

        return new CommandToken(start, end - start, end < input.Length);
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

    private readonly record struct CommandToken(int Start, int Length, bool HasArgumentBoundary);

    private readonly record struct ScoredSuggestion(int Score, CommandSuggestion Suggestion);
}
