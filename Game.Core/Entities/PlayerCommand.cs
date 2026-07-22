using System.Numerics;

namespace Game.Core.Entities;

public readonly record struct PlayerCommand(
    float MoveAxis,
    bool WantsJump,
    bool WantsFly = false,
    bool WantsGlide = false,
    bool WantsGuard = false,
    Vector2 GuardFacing = default)
{
    /// <summary>
    /// Preserves the established positional guard-command contract after flight
    /// and glide intents were added ahead of the guard fields.
    /// </summary>
    public PlayerCommand(float moveAxis, bool wantsJump, bool wantsGuard, Vector2 guardFacing)
        : this(moveAxis, wantsJump, false, false, wantsGuard, guardFacing)
    {
    }

    public static PlayerCommand None { get; } = new(0, false, false, false, false, Vector2.Zero);
}
