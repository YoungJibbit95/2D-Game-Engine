using System.Globalization;

namespace Game.Core.Commands;

public sealed class TimeCommand : IConsoleCommand
{
    public string Name => "time";

    public string Description => "Changes the world time.";

    public IReadOnlyList<string> Aliases { get; } = Array.Empty<string>();

    public CommandResult Execute(CommandContext context, IReadOnlyList<string> arguments)
    {
        if (context.WorldTime is null)
        {
            return CommandResult.Failure("World time is required for /time.");
        }

        if (arguments.Count == 0)
        {
            return CommandResult.Failure("Usage: /time <day|night|set> [normalizedTime]");
        }

        switch (arguments[0].ToLowerInvariant())
        {
            case "day":
            case "noon":
                context.WorldTime.SetDay();
                return CommandResult.Success("Time set to day.");
            case "night":
            case "midnight":
                context.WorldTime.SetNight();
                return CommandResult.Success("Time set to night.");
            case "set":
                return SetTime(context, arguments);
            default:
                return CommandResult.Failure("Usage: /time <day|night|set> [normalizedTime]");
        }
    }

    private static CommandResult SetTime(CommandContext context, IReadOnlyList<string> arguments)
    {
        if (arguments.Count < 2)
        {
            return CommandResult.Failure("Usage: /time set <normalizedTime>");
        }

        if (!double.TryParse(arguments[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var normalized))
        {
            return CommandResult.Failure("Normalized time must be a number.");
        }

        context.WorldTime!.SetTimeNormalized(normalized);
        return CommandResult.Success($"Time set to {context.WorldTime.NormalizedTimeOfDay:0.###}.");
    }
}
