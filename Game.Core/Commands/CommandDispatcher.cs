using Game.Core.Events;
using Game.Core.DeveloperTools;

namespace Game.Core.Commands;

public sealed class CommandDispatcher
{
    private readonly CommandRegistry _registry;
    private readonly CommandParser _parser;
    private readonly CommandHistory? _history;
    private readonly DeveloperCommandCatalog _catalog;

    public CommandDispatcher(
        CommandRegistry registry,
        CommandParser? parser = null,
        CommandHistory? history = null)
    {
        ArgumentNullException.ThrowIfNull(registry);
        _registry = registry;
        _parser = parser ?? new CommandParser();
        _history = history;
        _catalog = new DeveloperCommandCatalog(registry);
    }

    public CommandResult Execute(string input, CommandContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!_parser.TryParse(input, out var parsed, out var parseError))
        {
            var emptyResult = parseError is null
                ? CommandResult.Failure(
                    "empty_command",
                    "No command entered. Type /help to list commands.")
                : CommandResult.Failure(
                    "invalid_command_syntax",
                    $"{parseError} Usage supports quoted arguments such as /give \"item id\".");
            emptyResult = PublishExecuted(context, string.Empty, emptyResult);
            _history?.Record(input, emptyResult);
            return emptyResult;
        }

        if (!_registry.TryGet(parsed.Name, out var command))
        {
            var unknownResult = CommandResult.Failure(
                "unknown_command",
                BuildUnknownCommandMessage(parsed.Name));
            unknownResult = PublishExecuted(context, parsed.Name, unknownResult);
            _history?.Record(input, unknownResult);
            return unknownResult;
        }

        CommandResult result;
        try
        {
            result = command.Execute(context, parsed.Arguments);
        }
        catch (Exception ex)
        {
            result = CommandResult.Failure(
                "command_exception",
                $"/{command.Name} failed with {ex.GetType().Name}: {ex.Message}");
        }

        if (result.Intent is { } intent && context.Events is { } intentEvents)
        {
            try
            {
                intentEvents.Publish(new DeveloperCommandIntentRequestedEvent(command.Name, intent));
            }
            catch (Exception ex)
            {
                result = CommandResult.Failure(
                    "intent_dispatch_failed",
                    $"/{command.Name} request could not be delivered: {ex.GetType().Name}: {ex.Message}");
            }
        }

        result = PublishExecuted(context, command.Name, result);
        _history?.Record(input, result);
        return result;
    }

    private static CommandResult PublishExecuted(
        CommandContext context,
        string commandName,
        CommandResult result)
    {
        if (context.Events is not { } events)
        {
            return result;
        }

        try
        {
            events.Publish(new CommandExecutedEvent(commandName, result.IsSuccess, result.Message));
            return result;
        }
        catch (Exception ex)
        {
            return CommandResult.Failure(
                "command_event_delivery_failed",
                $"/{commandName} completed, but command-event delivery failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private string BuildUnknownCommandMessage(string commandName)
    {
        Span<int> matches = stackalloc int[3];
        var count = _catalog.Search(commandName, CommandCategory.All, matches);
        if (count == 0)
        {
            return $"Unknown command '{commandName}'. Type /help to list commands.";
        }

        var suggestions = new string[count];
        for (var index = 0; index < count; index++)
        {
            suggestions[index] = $"/{_catalog.Entries[matches[index]].Name}";
        }

        return $"Unknown command '{commandName}'. Did you mean {string.Join(", ", suggestions)}? " +
               "Type /help to list commands.";
    }
}
