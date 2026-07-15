using Game.Core.Entities;
using Game.Core.Runtime;
using Game.Core.World;

namespace Game.Core.Diagnostics.Replay;

public static class ReplayLimits
{
    public const int DefaultFrameCapacity = 8_192;
    public const int MaximumFrameCapacity = 65_536;
    public const int MaximumSerializedBytes = 64 * 1024 * 1024;
}

public readonly record struct ReplayVector2(float X, float Y)
{
    public bool IsFinite => float.IsFinite(X) && float.IsFinite(Y);
}

public readonly record struct ReplayPlayerCommand(
    float MoveAxis,
    bool WantsJump,
    bool WantsGuard,
    ReplayVector2 GuardFacing)
{
    public static ReplayPlayerCommand FromRuntime(in PlayerCommand command)
    {
        return new ReplayPlayerCommand(
            command.MoveAxis,
            command.WantsJump,
            command.WantsGuard,
            new ReplayVector2(command.GuardFacing.X, command.GuardFacing.Y));
    }

    public PlayerCommand ToRuntime()
    {
        Validate();
        return new PlayerCommand(
            MoveAxis,
            WantsJump,
            WantsGuard,
            new System.Numerics.Vector2(GuardFacing.X, GuardFacing.Y));
    }

    public void Validate()
    {
        if (!float.IsFinite(MoveAxis) || MoveAxis is < -1f or > 1f || !GuardFacing.IsFinite)
        {
            throw new InvalidDataException("Replay player command contains invalid numeric input.");
        }
    }
}

public readonly record struct ReplayPlayerItemUseRequest(
    bool IsActive,
    int TargetTileX,
    int TargetTileY,
    ReplayVector2 TargetWorldPosition)
{
    public static ReplayPlayerItemUseRequest? FromRuntime(in PlayerItemUseRequest request)
    {
        return request.IsActive
            ? new ReplayPlayerItemUseRequest(
                true,
                request.TargetTile.X,
                request.TargetTile.Y,
                new ReplayVector2(request.TargetWorldPosition.X, request.TargetWorldPosition.Y))
            : null;
    }

    public PlayerItemUseRequest ToRuntime()
    {
        Validate();
        return new PlayerItemUseRequest(
            true,
            new TilePos(TargetTileX, TargetTileY),
            new System.Numerics.Vector2(TargetWorldPosition.X, TargetWorldPosition.Y));
    }

    public void Validate()
    {
        if (!IsActive || !TargetWorldPosition.IsFinite)
        {
            throw new InvalidDataException("Replay item-use request is inactive or contains invalid coordinates.");
        }
    }
}

public readonly record struct ReplayInputFrame
{
    public const int CurrentFormatVersion = 1;

    public int FormatVersion { get; init; }

    public long Tick { get; init; }

    public long Sequence { get; init; }

    public float DeltaSeconds { get; init; }

    public ReplayPlayerCommand Command { get; init; }

    public ReplayPlayerItemUseRequest? ItemUseRequest { get; init; }

    public ulong? CheckpointStateHash { get; init; }

    public static ReplayInputFrame Create(
        long tick,
        long sequence,
        in PlayerCommand command,
        in PlayerItemUseRequest itemUseRequest,
        ulong? checkpointStateHash = null,
        float deltaSeconds = 1f / 60f)
    {
        var frame = new ReplayInputFrame
        {
            FormatVersion = CurrentFormatVersion,
            Tick = tick,
            Sequence = sequence,
            DeltaSeconds = deltaSeconds,
            Command = ReplayPlayerCommand.FromRuntime(command),
            ItemUseRequest = ReplayPlayerItemUseRequest.FromRuntime(itemUseRequest),
            CheckpointStateHash = checkpointStateHash
        };
        Validate(frame);
        return frame;
    }

    public static void Validate(in ReplayInputFrame frame)
    {
        if (frame.FormatVersion != CurrentFormatVersion)
        {
            throw new InvalidDataException(
                $"Replay input frame version {frame.FormatVersion} is unsupported.");
        }

        if (frame.Tick < 0 || frame.Sequence < 0)
        {
            throw new InvalidDataException("Replay input frame tick and sequence must be non-negative.");
        }

        if (!float.IsFinite(frame.DeltaSeconds) || frame.DeltaSeconds <= 0f || frame.DeltaSeconds > 1f)
        {
            throw new InvalidDataException("Replay frame delta time must be finite and between 0 and 1 second.");
        }

        frame.Command.Validate();
        frame.ItemUseRequest?.Validate();
    }
}
