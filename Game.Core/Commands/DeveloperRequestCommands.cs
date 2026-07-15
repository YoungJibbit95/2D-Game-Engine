using Game.Core.DeveloperTools;
using System.Globalization;

namespace Game.Core.Commands;

public sealed class SpawnRateCommand : TypedConsoleCommand
{
    public SpawnRateCommand()
        : base(new CommandSpecification(
            "spawnrate",
            "Requests an entity spawn-rate multiplier.",
            new[]
            {
                new CommandArgumentSpecification(
                    "multiplier",
                    CommandArgumentType.Text,
                    description: "Multiplier from 0 to 10, or reset.")
            },
            aliases: new[] { "spawn-rate" },
            examples: new[] { "/spawnrate 0", "/spawnrate 2", "/spawnrate reset" }))
    {
    }

    protected override CommandResult Execute(CommandContext context, CommandArguments arguments)
    {
        var value = arguments.GetString("multiplier");
        if (value.Equals("reset", StringComparison.OrdinalIgnoreCase))
        {
            return CommandResult.Request(
                "spawn_rate_requested",
                "Requested spawn-rate reset.",
                new SetSpawnRateIntent(1f, Reset: true));
        }

        if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var multiplier) ||
            !float.IsFinite(multiplier) || multiplier is < 0f or > 10f)
        {
            return CommandResult.Failure("invalid_argument", "Spawn-rate multiplier must be between 0 and 10, or reset.");
        }

        return CommandResult.Request(
            "spawn_rate_requested",
            $"Requested spawn-rate multiplier {multiplier:0.##}.",
            new SetSpawnRateIntent(multiplier, Reset: false));
    }
}

public sealed class PerformanceCommand : TypedConsoleCommand
{
    public PerformanceCommand()
        : base(new CommandSpecification(
            "performance",
            "Requests performance telemetry operations.",
            new[]
            {
                new CommandArgumentSpecification(
                    "action",
                    CommandArgumentType.Choice,
                    choices: new[] { "summary", "capture", "reset" },
                    description: "Telemetry operation.")
            },
            aliases: new[] { "perf" },
            examples: new[] { "/performance summary", "/perf capture" }))
    {
    }

    protected override CommandResult Execute(CommandContext context, CommandArguments arguments)
    {
        var kind = Enum.Parse<PerformanceRequestKind>(arguments.GetString("action"), ignoreCase: true);
        return CommandResult.Request(
            "performance_requested",
            $"Requested performance operation: {kind}.",
            new PerformanceRequestIntent(kind));
    }
}

public sealed class EventDiagnosticsCommand : TypedConsoleCommand
{
    public EventDiagnosticsCommand()
        : base(new CommandSpecification(
            "event",
            "Requests event-journal diagnostics operations.",
            new[]
            {
                new CommandArgumentSpecification(
                    "action",
                    CommandArgumentType.Choice,
                    choices: new[] { "list", "watch", "unwatch", "clear" },
                    description: "Event diagnostics operation."),
                new CommandArgumentSpecification(
                    "eventName",
                    CommandArgumentType.Identifier,
                    false,
                    "Event type name for watch or unwatch.")
            },
            aliases: new[] { "events" },
            examples: new[] { "/event list", "/event watch EntityDiedEvent", "/event clear" }))
    {
    }

    protected override CommandResult Execute(CommandContext context, CommandArguments arguments)
    {
        var kind = Enum.Parse<EventDiagnosticsRequestKind>(arguments.GetString("action"), ignoreCase: true);
        var eventName = arguments.GetOptionalString("eventName");
        if (kind is EventDiagnosticsRequestKind.Watch or EventDiagnosticsRequestKind.Unwatch && eventName is null)
        {
            return CommandResult.Failure(
                "missing_argument",
                $"An eventName is required for /event {kind.ToString().ToLowerInvariant()}.");
        }

        if (kind is EventDiagnosticsRequestKind.List or EventDiagnosticsRequestKind.Clear && eventName is not null)
        {
            return CommandResult.Failure(
                "too_many_arguments",
                $"/event {kind.ToString().ToLowerInvariant()} does not accept an eventName.");
        }

        return CommandResult.Request(
            "event_diagnostics_requested",
            $"Requested event diagnostics operation: {kind}{(eventName is null ? string.Empty : $" {eventName}")}.",
            new EventDiagnosticsRequestIntent(kind, eventName));
    }
}
