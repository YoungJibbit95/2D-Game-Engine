namespace Game.Core.Entities;

public readonly record struct PlayerCommand(float MoveAxis, bool WantsJump)
{
    public static PlayerCommand None { get; } = new(0, false);
}
