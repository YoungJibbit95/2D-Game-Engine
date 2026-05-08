namespace Game.Core.Commands;

public interface IConsoleCommand
{
    string Name { get; }

    string Description { get; }

    IReadOnlyList<string> Aliases { get; }

    CommandResult Execute(CommandContext context, IReadOnlyList<string> arguments);
}
