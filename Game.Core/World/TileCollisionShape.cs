namespace Game.Core.World;

/// <summary>
/// Renderer-neutral collision geometry encoded by a tile instance. The current
/// physics contract supports full blocks and upward-facing floor surfaces; it
/// intentionally does not imply arbitrary polygon collision.
/// </summary>
public enum TileCollisionShape : byte
{
    Empty,
    FullBlock,
    OneWayPlatform,
    HalfBlock,
    SlopeAscendingRight,
    SlopeAscendingLeft
}
