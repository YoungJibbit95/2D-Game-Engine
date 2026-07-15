using Game.Core.World;
using System.Numerics;

namespace Game.Core.Runtime;

public readonly record struct PlayerItemUseRequest(
    bool IsActive,
    TilePos TargetTile,
    Vector2 TargetWorldPosition)
{
    public static PlayerItemUseRequest Inactive { get; } = new(false, default, Vector2.Zero);
}
