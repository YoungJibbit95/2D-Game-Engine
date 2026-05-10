using System.Numerics;

namespace Game.Core.Maps;

public sealed record TopDownMapMovementResult(
    Vector2 PreviousPosition,
    Vector2 Position,
    Vector2 Velocity,
    TopDownFacing Facing,
    bool BlockedX,
    bool BlockedY,
    MapWarpTarget? Warp)
{
    public bool WasBlocked => BlockedX || BlockedY;

    public bool HasWarp => Warp is not null;
}
