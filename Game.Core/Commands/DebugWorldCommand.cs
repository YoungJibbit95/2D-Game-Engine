using Game.Core.Diagnostics;

namespace Game.Core.Commands;

public sealed class DebugWorldCommand : IConsoleCommand
{
    private readonly EngineDebugSnapshotBuilder _snapshots = new();

    public string Name => "debug";

    public string Description => "Prints engine debug information.";

    public IReadOnlyList<string> Aliases { get; } = new[] { "dbg" };

    public CommandResult Execute(CommandContext context, IReadOnlyList<string> arguments)
    {
        if (arguments.Count == 0 || !string.Equals(arguments[0], "world", StringComparison.OrdinalIgnoreCase))
        {
            return CommandResult.Failure("Usage: /debug world");
        }

        if (context.World is null)
        {
            return CommandResult.Failure("World is required for /debug world.");
        }

        if (context.EntityManager is null)
        {
            return CommandResult.Failure("Entity manager is required for /debug world.");
        }

        var snapshot = _snapshots.Build(context.World, context.EntityManager, context.WorldTime);
        return CommandResult.Success(
            $"world={snapshot.WorldName} seed={snapshot.Seed} size={snapshot.WidthTiles}x{snapshot.HeightTiles} " +
            $"chunks={snapshot.LoadedChunkCount} dirty={snapshot.DirtyChunkCount} entities={snapshot.ActiveEntityCount} " +
            $"liquid={snapshot.LiquidTileCount} surface={snapshot.MinSurfaceY}-{snapshot.MaxSurfaceY}");
    }
}
