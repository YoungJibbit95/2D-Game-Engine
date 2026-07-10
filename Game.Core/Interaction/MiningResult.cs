using Game.Core.Events;
using Game.Core.Inventory;
using Game.Core.World;

namespace Game.Core.Interaction;

public readonly record struct MiningResult(bool Completed, TilePos TilePosition, ItemStack DroppedItem)
{
    public MiningActionStatus Status { get; init; }

    public GameplayActionFailureReason FailureReason { get; init; }

    public ushort TargetTileId { get; init; }

    public float Progress { get; init; }

    public float PreviousProgress { get; init; }

    public float ProgressDelta => Math.Max(0, Progress - PreviousProgress);

    public bool Started => Status == MiningActionStatus.Started;

    public bool InProgress => Status is MiningActionStatus.Started or MiningActionStatus.InProgress;

    public bool Blocked => Status == MiningActionStatus.Blocked;

    public static MiningResult None { get; } = new(false, TilePos.Zero, ItemStack.Empty)
    {
        Status = MiningActionStatus.None
    };

    public static MiningResult Progressed(
        TilePos target,
        ushort targetTileId,
        float previousProgress,
        float progress,
        bool started)
    {
        return new MiningResult(false, target, ItemStack.Empty)
        {
            Status = started ? MiningActionStatus.Started : MiningActionStatus.InProgress,
            TargetTileId = targetTileId,
            PreviousProgress = Math.Clamp(previousProgress, 0f, 1f),
            Progress = Math.Clamp(progress, 0f, 1f)
        };
    }

    public static MiningResult CompletedResult(TilePos target, ushort targetTileId, ItemStack drop, float previousProgress)
    {
        return new MiningResult(true, target, drop)
        {
            Status = MiningActionStatus.Completed,
            TargetTileId = targetTileId,
            PreviousProgress = Math.Clamp(previousProgress, 0f, 1f),
            Progress = 1f
        };
    }

    public static MiningResult BlockedResult(
        TilePos target,
        ushort targetTileId,
        GameplayActionFailureReason reason)
    {
        return new MiningResult(false, target, ItemStack.Empty)
        {
            Status = MiningActionStatus.Blocked,
            FailureReason = reason,
            TargetTileId = targetTileId
        };
    }
}
