using Game.Core.DeveloperTools;

namespace Game.Core.Commands;

public sealed class HelpCommand : TypedConsoleCommand
{
    private readonly CommandHelpService _help;

    public HelpCommand(CommandRegistry registry)
        : base(new CommandSpecification(
            "help",
            "Lists commands or shows detailed help for one command.",
            new[]
            {
                new CommandArgumentSpecification(
                    "command",
                    CommandArgumentType.Identifier,
                    isRequired: false,
                    description: "Command name or alias.")
            },
            aliases: new[] { "?" },
            examples: new[] { "/help", "/help give" }))
    {
        _help = new CommandHelpService(registry);
    }

    protected override CommandResult Execute(CommandContext context, CommandArguments arguments)
    {
        var commandName = arguments.GetOptionalString("command");
        if (commandName is null)
        {
            return CommandResult.Success("help_overview", _help.BuildOverview());
        }

        return _help.TryBuildCommandHelp(commandName, out var help)
            ? CommandResult.Success("help_command", help)
            : CommandResult.Failure("unknown_command", $"Unknown command '{commandName}'.");
    }
}
