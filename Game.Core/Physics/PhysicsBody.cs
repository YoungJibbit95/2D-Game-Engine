using System.Numerics;
using Game.Core.World;

namespace Game.Core.Physics;

public sealed class PhysicsBody
{
    public Vector2 Position { get; set; }

    public Vector2 Velocity { get; set; }

    public Vector2 Size { get; init; } = new(12, 28);

    public bool OnGround { get; set; }

    public bool CollidesWithTiles { get; init; } = true;

    public Vector2 Center => Position + Size * 0.5f;

    public RectI Bounds => new(
        (int)MathF.Floor(Position.X),
        (int)MathF.Floor(Position.Y),
        (int)MathF.Ceiling(Size.X),
        (int)MathF.Ceiling(Size.Y));
}
