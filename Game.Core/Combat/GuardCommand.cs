using System.Numerics;

namespace Game.Core.Combat;

public enum GuardCommandKind
{
    Begin,
    End,
    Toggle,
    UpdateFacing
}

public readonly record struct GuardCommand(
    GuardCommandKind Kind,
    Vector2 Facing)
{
    public static GuardCommand Begin(Vector2 facing) => new(GuardCommandKind.Begin, facing);

    public static GuardCommand End { get; } = new(GuardCommandKind.End, Vector2.Zero);

    public static GuardCommand Toggle(Vector2 facing) => new(GuardCommandKind.Toggle, facing);

    public static GuardCommand UpdateFacing(Vector2 facing) => new(GuardCommandKind.UpdateFacing, facing);
}

public readonly record struct GuardCommandResult(
    bool Accepted,
    bool IsGuarding,
    bool IsGuardBroken,
    float Stamina);
