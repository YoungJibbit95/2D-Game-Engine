using System.Globalization;
using System.Numerics;

namespace Game.Core.Commands;

public sealed class SpawnCommand : TypedConsoleCommand
{
    public SpawnCommand()
        : base(new CommandSpecification(
            "spawn",
            "Spawns an entity definition at a world position.",
            new[]
            {
                new CommandArgumentSpecification(
                    "entityId",
                    CommandArgumentType.Identifier,
                    description: "Registered entity id.",
                    suggestionSource: CommandSuggestionSource.Entities),
                new CommandArgumentSpecification("x", CommandArgumentType.Number, false, "World X position."),
                new CommandArgumentSpecification("y", CommandArgumentType.Number, false, "World Y position.")
            },
            examples: new[] { "/spawn slime", "/spawn slime 32 48" }))
    {
    }

    protected override CommandResult Execute(CommandContext context, CommandArguments typedArguments)
    {
        var arguments = typedArguments.Raw;
        if (context.Content is null)
        {
            return CommandResult.Failure("Content database is required for /spawn.");
        }

        if (context.EntityManager is null || context.EntityFactory is null)
        {
            return CommandResult.Failure("Entity manager and factory are required for /spawn.");
        }

        var entityId = typedArguments.GetString("entityId");
        if (!context.Content.Entities.TryGetById(entityId, out var definition))
        {
            return CommandResult.Failure($"Unknown entity '{entityId}'.");
        }

        if (arguments.Count == 2)
        {
            return CommandResult.Failure("invalid_position", "Spawn X and Y must be provided together.");
        }

        var positionResult = TryReadPosition(arguments, context.PlayerPosition);
        if (!positionResult.IsSuccess)
        {
            return CommandResult.Failure(positionResult.Error);
        }

        var entity = context.EntityFactory.CreateEnemy(definition, positionResult.Position);
        context.EntityManager.Add(entity);
        return CommandResult.Success($"Spawned {entityId} #{entity.Id} at {positionResult.Position.X:0.##}, {positionResult.Position.Y:0.##}.");
    }

    private static PositionParseResult TryReadPosition(IReadOnlyList<string> arguments, Vector2? fallbackPosition)
    {
        if (arguments.Count >= 3)
        {
            if (!float.TryParse(arguments[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var x) ||
                !float.TryParse(arguments[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
            {
                return PositionParseResult.Fail("Spawn position must be numeric.");
            }

            return PositionParseResult.Success(new Vector2(x, y));
        }

        if (fallbackPosition is { } position)
        {
            return PositionParseResult.Success(position);
        }

        return PositionParseResult.Fail("Usage: /spawn <entityId> [x] [y]");
    }

    private readonly record struct PositionParseResult(bool IsSuccess, Vector2 Position, string Error)
    {
        public static PositionParseResult Success(Vector2 position)
        {
            return new PositionParseResult(true, position, string.Empty);
        }

        public static PositionParseResult Fail(string error)
        {
            return new PositionParseResult(false, Vector2.Zero, error);
        }
    }
}
