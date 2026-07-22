using Game.Core.DeveloperTools;
using System.Numerics;

namespace Game.Core.Commands;

public sealed class TeleportCommand : TypedConsoleCommand
{
    public TeleportCommand()
        : base(new CommandSpecification(
            "tp",
            "Requests an authoritative player teleport.",
            new[]
            {
                new CommandArgumentSpecification("x", CommandArgumentType.Number, description: "World X position."),
                new CommandArgumentSpecification("y", CommandArgumentType.Number, description: "World Y position.")
            },
            aliases: new[] { "teleport" },
            examples: new[] { "/tp 128 64" },
            category: CommandCategory.Player,
            searchTerms: new[] { "teleport", "move", "location" },
            requestIntentType: typeof(TeleportPlayerIntent)))
    {
    }

    protected override CommandResult Execute(CommandContext context, CommandArguments arguments)
    {
        var position = new Vector2(arguments.GetSingle("x"), arguments.GetSingle("y"));
        return CommandResult.Request(
            "teleport_requested",
            $"Requested teleport to {position.X:0.##}, {position.Y:0.##}.",
            new TeleportPlayerIntent(position));
    }
}

public sealed class PositionCommand : TypedConsoleCommand
{
    public PositionCommand()
        : base(new CommandSpecification(
            "position",
            "Prints the current player world position.",
            aliases: new[] { "pos" },
            category: CommandCategory.Player,
            searchTerms: new[] { "coordinates", "location" }))
    {
    }

    protected override CommandResult Execute(CommandContext context, CommandArguments arguments)
    {
        return context.PlayerPosition is { } position
            ? CommandResult.Success("player_position", $"Player position: {position.X:0.##}, {position.Y:0.##}.")
            : CommandResult.Failure("missing_player_position", "Player position is required for /position.");
    }
}
