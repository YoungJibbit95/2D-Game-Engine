using Game.Core.DeveloperTools;

namespace Game.Core.Commands;

public sealed class DeveloperMovementModeCommand : TypedConsoleCommand
{
    private readonly DeveloperMovementMode _mode;

    public DeveloperMovementModeCommand(string name, DeveloperMovementMode mode, params string[] aliases)
        : base(new CommandSpecification(
            name,
            $"Requests a {name} state change.",
            new[]
            {
                new CommandArgumentSpecification(
                    "value",
                    CommandArgumentType.Boolean,
                    false,
                    "Optional on, off, or toggle state.")
            },
            aliases: aliases,
            examples: new[] { $"/{name}", $"/{name} on" },
            category: CommandCategory.Movement,
            searchTerms: new[] { "player", "movement", "cheat" },
            requestIntentType: typeof(SetDeveloperMovementModeIntent)))
    {
        _mode = mode;
    }

    protected override CommandResult Execute(CommandContext context, CommandArguments arguments)
    {
        var toggle = DeveloperCommandParsing.ParseToggle(arguments.GetOptionalString("value"));
        return CommandResult.Request(
            "movement_mode_requested",
            $"Requested {_mode}: {toggle}.",
            new SetDeveloperMovementModeIntent(_mode, toggle));
    }
}

public sealed class SpeedCommand : TypedConsoleCommand
{
    public SpeedCommand()
        : base(new CommandSpecification(
            "speed",
            "Requests a developer movement speed multiplier.",
            new[]
            {
                new CommandArgumentSpecification(
                    "multiplier",
                    CommandArgumentType.Text,
                    description: "Multiplier from 0.1 to 20, or reset.",
                    choices: new[] { "reset" })
            },
            examples: new[] { "/speed 2", "/speed reset" },
            category: CommandCategory.Movement,
            searchTerms: new[] { "player", "movement", "multiplier" },
            requestIntentType: typeof(SetDeveloperSpeedIntent)))
    {
    }

    protected override CommandResult Execute(CommandContext context, CommandArguments arguments)
    {
        var value = arguments.GetString("multiplier");
        if (value.Equals("reset", StringComparison.OrdinalIgnoreCase))
        {
            return CommandResult.Request(
                "speed_requested",
                "Requested movement speed reset.",
                new SetDeveloperSpeedIntent(1f, Reset: true));
        }

        if (!float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var multiplier) ||
            !float.IsFinite(multiplier) || multiplier is < 0.1f or > 20f)
        {
            return CommandResult.Failure("invalid_argument", "Speed multiplier must be between 0.1 and 20, or reset.");
        }

        return CommandResult.Request(
            "speed_requested",
            $"Requested movement speed multiplier {multiplier:0.##}.",
            new SetDeveloperSpeedIntent(multiplier, Reset: false));
    }
}
