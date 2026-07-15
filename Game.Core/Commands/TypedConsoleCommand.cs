namespace Game.Core.Commands;

public abstract class TypedConsoleCommand : IConsoleCommand
{
    private readonly CommandArgumentValidator _validator = new();

    protected TypedConsoleCommand(CommandSpecification specification)
    {
        ArgumentNullException.ThrowIfNull(specification);
        Specification = specification;
    }

    public CommandSpecification Specification { get; }

    public string Name => Specification.Name;

    public string Description => Specification.Description;

    public IReadOnlyList<string> Aliases => Specification.Aliases;

    public CommandResult Execute(CommandContext context, IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(context);
        var validation = _validator.Validate(Specification, arguments);
        if (!validation.IsValid)
        {
            var issue = validation.Issues[0];
            return CommandResult.Failure(issue.Code, issue.Message);
        }

        return Execute(context, validation.Arguments!);
    }

    protected abstract CommandResult Execute(CommandContext context, CommandArguments arguments);
}
