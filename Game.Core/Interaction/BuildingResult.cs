using Game.Core.Events;
using Game.Core.World;

namespace Game.Core.Interaction;

public readonly record struct BuildingResult(
    bool Success,
    TilePos Position,
    string? ItemId,
    ushort TileId,
    GameplayActionFailureReason FailureReason)
{
    public bool Blocked => !Success && FailureReason != GameplayActionFailureReason.None;

    public static BuildingResult Placed(TilePos position, string itemId, ushort tileId)
    {
        return new BuildingResult(true, position, itemId, tileId, GameplayActionFailureReason.None);
    }

    public static BuildingResult BlockedResult(
        TilePos position,
        string? itemId,
        GameplayActionFailureReason reason)
    {
        return new BuildingResult(false, position, itemId, 0, reason);
    }
}
