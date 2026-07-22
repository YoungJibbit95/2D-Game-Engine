using Game.Core.Diagnostics;

namespace Game.Core.Commands;

public sealed class DebugWorldCommand : TypedConsoleCommand
{
    private readonly EngineDebugSnapshotBuilder _snapshots = new();

    public DebugWorldCommand()
        : base(new CommandSpecification(
            "debug",
            "Prints world information or requests a debug view change.",
            new[]
            {
                new CommandArgumentSpecification(
                    "view",
                    CommandArgumentType.Choice,
                    choices: new[] { "world", "overlay", "collisions", "ai", "streaming", "lighting", "shadows", "reflections", "particles", "background", "combat", "spawns" },
                    description: "Debug view or world summary."),
                new CommandArgumentSpecification(
                    "value",
                    CommandArgumentType.Boolean,
                    false,
                    "Optional on, off, or toggle state.")
            },
            aliases: new[] { "dbg" },
            examples: new[] { "/debug world", "/debug collisions on" },
            category: CommandCategory.Diagnostics,
            searchTerms: new[] { "overlay", "visualize", "world", "collision", "ai" },
            requestIntentType: typeof(Game.Core.DeveloperTools.SetDebugViewIntent)))
    {
    }

    protected override CommandResult Execute(CommandContext context, CommandArguments typedArguments)
    {
        var view = typedArguments.GetString("view");
        if (!string.Equals(view, "world", StringComparison.OrdinalIgnoreCase))
        {
            var debugView = Enum.Parse<Game.Core.DeveloperTools.DebugView>(view, ignoreCase: true);
            var toggle = DeveloperCommandParsing.ParseToggle(typedArguments.GetOptionalString("value"));
            return CommandResult.Request(
                "debug_view_requested",
                $"Requested {debugView} debug view: {toggle}.",
                new Game.Core.DeveloperTools.SetDebugViewIntent(debugView, toggle));
        }


        if (typedArguments.Has("value"))
        {
            return CommandResult.Failure("too_many_arguments", "/debug world does not accept a toggle value.");
        }

        if (context.World is null)
        {
            return CommandResult.Failure("missing_world", "World is required for /debug world.");
        }

        if (context.EntityManager is null)
        {
            return CommandResult.Failure("missing_entities", "Entity manager is required for /debug world.");
        }

        var snapshot = _snapshots.Build(context.World, context.EntityManager, context.WorldTime);
        return CommandResult.Success(
            $"world={snapshot.WorldName} seed={snapshot.Seed} size={snapshot.WidthTiles}x{snapshot.HeightTiles} " +
            $"chunks={snapshot.LoadedChunkCount} dirty={snapshot.DirtyChunkCount} entities={snapshot.ActiveEntityCount} " +
            $"liquid={snapshot.LiquidTileCount} surface={snapshot.MinSurfaceY}-{snapshot.MaxSurfaceY}");
    }
}
