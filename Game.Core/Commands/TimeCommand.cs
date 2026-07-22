using System.Globalization;

namespace Game.Core.Commands;

public sealed class TimeCommand : TypedConsoleCommand
{
    public TimeCommand()
        : base(new CommandSpecification(
            "time",
            "Reads or changes the world time.",
            new[]
            {
                new CommandArgumentSpecification(
                    "action",
                    CommandArgumentType.Choice,
                    false,
                    "Time action.",
                    new[] { "status", "day", "noon", "night", "midnight", "set" }),
                new CommandArgumentSpecification(
                    "normalizedTime",
                    CommandArgumentType.Number,
                    false,
                    "Normalized day time.",
                    minimum: 0,
                    maximum: 1)
            },
            examples: new[] { "/time", "/time day", "/time set 0.5" },
            category: CommandCategory.Environment,
            searchTerms: new[] { "day", "night", "clock", "sun" }))
    {
    }

    protected override CommandResult Execute(CommandContext context, CommandArguments typedArguments)
    {
        var arguments = typedArguments.Raw;
        if (context.WorldTime is null)
        {
            return CommandResult.Failure("missing_world_time", "World time is required for /time.");
        }

        if (arguments.Count >= 2 && !arguments[0].Equals("set", StringComparison.OrdinalIgnoreCase))
        {
            return CommandResult.Failure("too_many_arguments", $"/time {arguments[0]} does not accept normalizedTime.");
        }

        if (arguments.Count == 0)
        {
            return CommandResult.Success(
                "time_status",
                $"Day {context.WorldTime.Day}, time {context.WorldTime.NormalizedTimeOfDay:0.###} ({(context.WorldTime.IsNight ? "night" : "day")}).");
        }

        switch (arguments[0].ToLowerInvariant())
        {
            case "day":
            case "noon":
                context.WorldTime.SetDay();
                return CommandResult.Success("time_set", "Time set to day.");
            case "night":
            case "midnight":
                context.WorldTime.SetNight();
                return CommandResult.Success("time_set", "Time set to night.");
            case "status":
                return CommandResult.Success(
                    "time_status",
                    $"Day {context.WorldTime.Day}, time {context.WorldTime.NormalizedTimeOfDay:0.###} ({(context.WorldTime.IsNight ? "night" : "day")}).");
            case "set":
                return SetTime(context, arguments);
            default:
                return CommandResult.Failure("invalid_time_action", "Usage: /time <day|night|set> [normalizedTime]");
        }
    }

    private static CommandResult SetTime(CommandContext context, IReadOnlyList<string> arguments)
    {
        if (arguments.Count < 2)
        {
            return CommandResult.Failure("missing_argument", "Usage: /time set <normalizedTime>");
        }

        if (!double.TryParse(arguments[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var normalized))
        {
            return CommandResult.Failure("invalid_argument", "Normalized time must be a finite number from 0 to 1.");
        }

        context.WorldTime!.SetTimeNormalized(normalized);
        return CommandResult.Success("time_set", $"Time set to {context.WorldTime.NormalizedTimeOfDay:0.###}.");
    }
}
