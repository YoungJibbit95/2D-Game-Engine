using System.Globalization;

namespace Game.Core.Commands;

public sealed class CommandArgumentValidator
{
    public CommandArgumentValidationResult Validate(
        CommandSpecification specification,
        IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(specification);
        ArgumentNullException.ThrowIfNull(arguments);

        var issues = new List<CommandValidationIssue>();
        var requiredCount = specification.Arguments.Count(argument => argument.IsRequired);
        if (arguments.Count < requiredCount)
        {
            var missing = specification.Arguments[arguments.Count];
            issues.Add(new CommandValidationIssue(
                "missing_argument",
                missing.Name,
                $"Missing required argument '{missing.Name}'. Usage: {specification.Usage}"));
        }

        if (arguments.Count > specification.Arguments.Count)
        {
            issues.Add(new CommandValidationIssue(
                "too_many_arguments",
                string.Empty,
                $"Too many arguments. Usage: {specification.Usage}"));
        }

        var count = Math.Min(arguments.Count, specification.Arguments.Count);
        for (var index = 0; index < count; index++)
        {
            ValidateValue(specification.Arguments[index], arguments[index], issues);
        }

        return issues.Count == 0
            ? CommandArgumentValidationResult.Valid(new CommandArguments(specification, arguments))
            : CommandArgumentValidationResult.Invalid(issues);
    }

    private static void ValidateValue(
        CommandArgumentSpecification specification,
        string value,
        ICollection<CommandValidationIssue> issues)
    {
        switch (specification.Type)
        {
            case CommandArgumentType.Text:
            case CommandArgumentType.Identifier:
                if (string.IsNullOrWhiteSpace(value))
                {
                    AddInvalid(specification, "Value must not be empty.", issues);
                }

                break;
            case CommandArgumentType.Integer:
                if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer))
                {
                    AddInvalid(specification, "Value must be an integer.", issues);
                }
                else
                {
                    ValidateRange(specification, integer, issues);
                }

                break;
            case CommandArgumentType.Number:
                if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ||
                    !double.IsFinite(number))
                {
                    AddInvalid(specification, "Value must be a finite number.", issues);
                }
                else
                {
                    ValidateRange(specification, number, issues);
                }

                break;
            case CommandArgumentType.Boolean:
                if (!IsBoolean(value))
                {
                    AddInvalid(specification, "Value must be on, off, true, false, 1, 0, or toggle.", issues);
                }

                break;
            case CommandArgumentType.Choice:
                if (!specification.Choices.Contains(value, StringComparer.OrdinalIgnoreCase))
                {
                    AddInvalid(specification, $"Value must be one of: {string.Join(", ", specification.Choices)}.", issues);
                }

                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(specification), specification.Type, "Unknown argument type.");
        }
    }

    private static bool IsBoolean(string value)
    {
        return value.Equals("on", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("off", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("false", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("1", StringComparison.Ordinal) ||
               value.Equals("0", StringComparison.Ordinal) ||
               value.Equals("toggle", StringComparison.OrdinalIgnoreCase);
    }

    private static void ValidateRange(
        CommandArgumentSpecification specification,
        double value,
        ICollection<CommandValidationIssue> issues)
    {
        if (specification.Minimum is { } minimum && value < minimum)
        {
            AddInvalid(specification, $"Value must be at least {minimum.ToString(CultureInfo.InvariantCulture)}.", issues);
        }

        if (specification.Maximum is { } maximum && value > maximum)
        {
            AddInvalid(specification, $"Value must be at most {maximum.ToString(CultureInfo.InvariantCulture)}.", issues);
        }
    }

    private static void AddInvalid(
        CommandArgumentSpecification specification,
        string message,
        ICollection<CommandValidationIssue> issues)
    {
        issues.Add(new CommandValidationIssue("invalid_argument", specification.Name, $"Invalid {specification.Name}: {message}"));
    }
}

public sealed record CommandArgumentValidationResult(
    bool IsValid,
    CommandArguments? Arguments,
    IReadOnlyList<CommandValidationIssue> Issues)
{
    public static CommandArgumentValidationResult Valid(CommandArguments arguments)
    {
        return new CommandArgumentValidationResult(true, arguments, Array.Empty<CommandValidationIssue>());
    }

    public static CommandArgumentValidationResult Invalid(IReadOnlyList<CommandValidationIssue> issues)
    {
        return new CommandArgumentValidationResult(false, null, issues);
    }
}
