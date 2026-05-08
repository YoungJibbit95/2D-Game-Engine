using Game.Core.Events;

namespace Game.Core.Commands;

public sealed class CommandDispatcher
{
    private readonly CommandRegistry _registry;
    private readonly CommandParser _parser;

    public CommandDispatcher(CommandRegistry registry, CommandParser? parser = null)
    {
        _registry = registry;
        _parser = parser ?? new CommandParser();
    }

    public CommandResult Execute(string input, CommandContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!_parser.TryParse(input, out var parsed))
        {
            var emptyResult = CommandResult.Failure("No command entered.");
            context.Events?.Publish(new CommandExecutedEvent(string.Empty, false, emptyResult.Message));
            return emptyResult;
        }

        if (!_registry.TryGet(parsed.Name, out var command))
        {
            var unknownResult = CommandResult.Failure($"Unknown command '{parsed.Name}'.");
            context.Events?.Publish(new CommandExecutedEvent(parsed.Name, false, unknownResult.Message));
            return unknownResult;
        }

        CommandResult result;
        try
        {
            result = command.Execute(context, parsed.Arguments);
        }
        catch (Exception ex)
        {
            result = CommandResult.Failure($"{command.Name} failed: {ex.Message}");
        }

        context.Events?.Publish(new CommandExecutedEvent(command.Name, result.IsSuccess, result.Message));
        return result;
    }
}
