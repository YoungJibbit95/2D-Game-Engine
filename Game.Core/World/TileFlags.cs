namespace Game.Core.World;

[Flags]
public enum TileFlags : ushort
{
    None = 0,
    Solid = 1 << 0,
    Platform = 1 << 1,
    Slope = 1 << 2,
    HalfBlock = 1 << 3,
    HasWall = 1 << 4,
    HasLiquid = 1 << 5,
    IsActuated = 1 << 6,
    IsNatural = 1 << 7,
    /// <summary>Upward-facing slope whose surface rises toward positive X.</summary>
    SlopeAscendingRight = 1 << 8,
    /// <summary>Upward-facing slope whose surface rises toward negative X.</summary>
    SlopeAscendingLeft = 1 << 9
}
