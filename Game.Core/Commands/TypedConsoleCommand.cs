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

        return ValidateIntentContract(Execute(context, validation.Arguments!));
    }

    protected abstract CommandResult Execute(CommandContext context, CommandArguments arguments);

    private CommandResult ValidateIntentContract(CommandResult result)
    {
        if (result.Intent is null)
        {
            return result.Kind == CommandResultKind.Request
                ? CommandResult.Failure(
                    "missing_request_intent",
                    $"/{Name} returned a request without a typed intent.")
                : result;
        }

        if (result.Kind != CommandResultKind.Request)
        {
            return CommandResult.Failure(
                "invalid_intent_result",
                $"/{Name} returned {result.Intent.GetType().Name} without request result semantics.");
        }

        if (Specification.RequestIntentType is null)
        {
            return CommandResult.Failure(
                "undeclared_request_intent",
                $"/{Name} returned an undeclared typed intent ({result.Intent.GetType().Name}).");
        }

        return Specification.RequestIntentType.IsInstanceOfType(result.Intent)
            ? result
            : CommandResult.Failure(
                "request_intent_mismatch",
                $"/{Name} declared {Specification.RequestIntentType.Name} but returned {result.Intent.GetType().Name}.");
    }
}
