using System.Numerics;

namespace Game.Core.Maps;

public sealed record TopDownMapTransitionResult(
    bool Success,
    string SourceMapId,
    string? TargetMapId,
    string? TargetSpawnId,
    string? WarpObjectId,
    Vector2 PreviousPosition,
    Vector2 NewPosition,
    string? FailureReason)
{
    public static TopDownMapTransitionResult Applied(
        string sourceMapId,
        MapWarpTarget warp,
        Vector2 previousPosition,
        Vector2 newPosition)
    {
        return new TopDownMapTransitionResult(
            true,
            sourceMapId,
            warp.TargetMapId,
            warp.TargetSpawnId,
            warp.ObjectId,
            previousPosition,
            newPosition,
            null);
    }

    public static TopDownMapTransitionResult Failed(string sourceMapId, Vector2 position, string reason)
    {
        return new TopDownMapTransitionResult(false, sourceMapId, null, null, null, position, position, reason);
    }
}
