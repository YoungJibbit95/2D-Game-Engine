using System.Numerics;

namespace Game.Core.Entities;

public readonly record struct PlayerCommand(
    float MoveAxis,
    bool WantsJump,
    bool WantsGuard = false,
    Vector2 GuardFacing = default)
{
    public static PlayerCommand None { get; } = new(0, false, false, Vector2.Zero);
}
