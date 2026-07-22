using Game.Core.Entities;

namespace Game.Core.Commands;

public sealed class DespawnCommand : TypedConsoleCommand
{
    public DespawnCommand()
        : base(new CommandSpecification(
            "despawn",
            "Despawns loaded entities by instance id, definition id, or all.",
            new[]
            {
                new CommandArgumentSpecification(
                    "target",
                    CommandArgumentType.Identifier,
                    description: "#instanceId, entity definition id, or all.",
                    choices: new[] { "all" },
                    suggestionSource: CommandSuggestionSource.LoadedEntities),
                new CommandArgumentSpecification(
                    "count",
                    CommandArgumentType.Integer,
                    false,
                    "Maximum number to despawn.",
                    minimum: 1)
            },
            examples: new[] { "/despawn #12", "/despawn slime 5", "/despawn all" },
            category: CommandCategory.Entities,
            searchTerms: new[] { "entity", "mob", "enemy", "remove" }))
    {
    }

    protected override CommandResult Execute(CommandContext context, CommandArguments arguments)
    {
        if (context.EntityManager is null)
        {
            return CommandResult.Failure("missing_entities", "Entity manager is required for /despawn.");
        }

        var target = arguments.GetString("target");
        var maximum = arguments.Has("count") ? arguments.GetInt32("count") : int.MaxValue;
        var matches = FindMatches(context.EntityManager, target, maximum);
        foreach (var entity in matches)
        {
            context.EntityManager.Remove(entity);
        }

        return matches.Count == 0
            ? CommandResult.Failure("entity_not_found", $"No loaded entity matched '{target}'.")
            : CommandResult.Success("entities_despawned", $"Despawned {matches.Count} entity instance(s).");
    }

    private static IReadOnlyList<Entity> FindMatches(EntityManager manager, string target, int maximum)
    {
        if (target.StartsWith('#') && int.TryParse(target.AsSpan(1), out var instanceId))
        {
            var instance = manager.Entities.FirstOrDefault(entity => entity.Id == instanceId);
            return instance is null ? Array.Empty<Entity>() : new[] { instance };
        }

        var all = target.Equals("all", StringComparison.OrdinalIgnoreCase);
        return manager.Entities
            .Where(entity => all || entity is EnemyEntity enemy &&
                enemy.DefinitionId.Equals(target, StringComparison.OrdinalIgnoreCase))
            .Take(maximum)
            .ToArray();
    }
}
