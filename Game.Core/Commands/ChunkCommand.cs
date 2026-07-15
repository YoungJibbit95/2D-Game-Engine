using Game.Core.DeveloperTools;
using Game.Core.World;

namespace Game.Core.Commands;

public sealed class ChunkCommand : TypedConsoleCommand
{
    public ChunkCommand()
        : base(new CommandSpecification(
            "chunk",
            "Shows chunk information or requests a streaming reload.",
            new[]
            {
                new CommandArgumentSpecification(
                    "action",
                    CommandArgumentType.Choice,
                    choices: new[] { "info", "reload" },
                    description: "Chunk action."),
                new CommandArgumentSpecification("x", CommandArgumentType.Integer, false, "Chunk X coordinate."),
                new CommandArgumentSpecification("y", CommandArgumentType.Integer, false, "Chunk Y coordinate."),
                new CommandArgumentSpecification("force", CommandArgumentType.Boolean, false, "Force reload of dirty chunks.")
            },
            aliases: new[] { "chunks" },
            examples: new[] { "/chunk info", "/chunk info -1 0", "/chunk reload 2 0 on" }))
    {
    }

    protected override CommandResult Execute(CommandContext context, CommandArguments arguments)
    {
        var positionResult = ResolvePosition(context, arguments);
        if (!positionResult.IsSuccess)
        {
            return CommandResult.Failure("invalid_chunk_position", positionResult.Error);
        }

        return arguments.GetString("action").Equals("info", StringComparison.OrdinalIgnoreCase)
            ? GetInfo(context, positionResult.Position)
            : RequestReload(arguments, positionResult.Position);
    }

    private static CommandResult GetInfo(CommandContext context, ChunkPos? position)
    {
        if (context.World is null)
        {
            return CommandResult.Failure("missing_world", "World is required for /chunk info.");
        }

        if (position is null)
        {
            var dirtyCount = context.World.Chunks.Values.Count(chunk => chunk.IsDirty);
            return CommandResult.Success(
                "chunk_summary",
                $"Loaded chunks: {context.World.Chunks.Count}; dirty: {dirtyCount}.");
        }

        if (!context.World.TryGetChunk(position.Value, out var chunk) || chunk is null)
        {
            return CommandResult.Failure("chunk_not_loaded", $"Chunk {position.Value.X}, {position.Value.Y} is not loaded.");
        }

        return CommandResult.Success(
            "chunk_info",
            $"Chunk {chunk.Position.X}, {chunk.Position.Y}: dirty={chunk.IsDirty}, mesh={chunk.NeedsMeshRebuild}, " +
            $"light={chunk.NeedsLightUpdate}, liquids={chunk.Metadata.ActiveLiquidTiles}, " +
            $"lights={chunk.Metadata.ActiveLightTiles}, entities={chunk.Metadata.TileEntityCount}, " +
            $"savedTick={chunk.Metadata.LastSavedTick}.");
    }

    private static CommandResult RequestReload(CommandArguments arguments, ChunkPos? position)
    {
        if (position is null)
        {
            return CommandResult.Failure(
                "missing_chunk_position",
                "Chunk coordinates or a current player position are required for /chunk reload.");
        }

        var force = arguments.Has("force") &&
                    DeveloperCommandParsing.ParseToggle(arguments.GetString("force")) == DeveloperToggle.On;
        return CommandResult.Request(
            "chunk_reload_requested",
            $"Requested reload of chunk {position.Value.X}, {position.Value.Y}{(force ? " (forced)" : string.Empty)}.",
            new ReloadChunkIntent(position.Value, force));
    }

    private static ChunkPositionResult ResolvePosition(CommandContext context, CommandArguments arguments)
    {
        if (arguments.Has("x") != arguments.Has("y"))
        {
            return ChunkPositionResult.Failure("Chunk X and Y must be provided together.");
        }

        if (arguments.Has("x"))
        {
            return ChunkPositionResult.Success(new ChunkPos(arguments.GetInt32("x"), arguments.GetInt32("y")));
        }

        return context.PlayerPosition is { } playerPosition
            ? ChunkPositionResult.Success(CoordinateUtils.TileToChunk(CoordinateUtils.WorldToTile(playerPosition)))
            : ChunkPositionResult.Empty;
    }

    private readonly record struct ChunkPositionResult(bool IsSuccess, ChunkPos? Position, string Error)
    {
        public static ChunkPositionResult Empty { get; } = new(true, null, string.Empty);

        public static ChunkPositionResult Success(ChunkPos position)
        {
            return new ChunkPositionResult(true, position, string.Empty);
        }

        public static ChunkPositionResult Failure(string error)
        {
            return new ChunkPositionResult(false, null, error);
        }
    }
}
