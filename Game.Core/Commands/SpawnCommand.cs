using System.Globalization;
using System.Numerics;

namespace Game.Core.Commands;

public sealed class SpawnCommand : IConsoleCommand
{
    public string Name => "spawn";

    public string Description => "Spawns an entity definition at a world position.";

    public IReadOnlyList<string> Aliases { get; } = Array.Empty<string>();

    public CommandResult Execute(CommandContext context, IReadOnlyList<string> arguments)
    {
        if (context.Content is null)
        {
            return CommandResult.Failure("Content database is required for /spawn.");
        }

        if (context.EntityManager is null || context.EntityFactory is null)
        {
            return CommandResult.Failure("Entity manager and factory are required for /spawn.");
        }

        if (arguments.Count == 0)
        {
            return CommandResult.Failure("Usage: /spawn <entityId> [x] [y]");
        }

        var entityId = arguments[0];
        if (!context.Content.Entities.TryGetById(entityId, out var definition))
        {
            return CommandResult.Failure($"Unknown entity '{entityId}'.");
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
