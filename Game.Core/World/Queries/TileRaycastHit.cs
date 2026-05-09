using System.Numerics;

namespace Game.Core.World.Queries;

public readonly record struct TileRaycastHit(
    bool Hit,
    TilePos TilePosition,
    TileInstance Tile,
    Vector2 WorldPosition,
    float Distance)
{
    public static TileRaycastHit None { get; } = new(false, TilePos.Zero, TileInstance.Air, Vector2.Zero, 0);
}
