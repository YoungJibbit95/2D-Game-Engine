using System.Numerics;

namespace Game.Core.Physics;

public sealed class TopDownMovementController
{
    private readonly TileCollisionResolver _collisionResolver;

    public TopDownMovementController(TileCollisionResolver? collisionResolver = null)
    {
        _collisionResolver = collisionResolver ?? new TileCollisionResolver();
    }

    public void Move(World.World world, PhysicsBody body, Vector2 direction, float deltaSeconds, TopDownMovementOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(body);

        options ??= new TopDownMovementOptions();

        if (deltaSeconds <= 0)
        {
            body.Velocity = Vector2.Zero;
            return;
        }

        var movement = ResolveDirection(direction, options);
        body.Velocity = movement * options.MoveSpeedPixelsPerSecond;
        _collisionResolver.Move(world, body, deltaSeconds);
        body.OnGround = false;
    }

    private static Vector2 ResolveDirection(Vector2 direction, TopDownMovementOptions options)
    {
        if (direction.LengthSquared() <= float.Epsilon)
        {
            return Vector2.Zero;
        }

        if (!options.AllowDiagonalMovement)
        {
            direction = MathF.Abs(direction.X) >= MathF.Abs(direction.Y)
                ? new Vector2(MathF.Sign(direction.X), 0)
                : new Vector2(0, MathF.Sign(direction.Y));
        }

        if (options.NormalizeDiagonalSpeed && direction.LengthSquared() > 1f)
        {
            direction = Vector2.Normalize(direction);
        }

        return direction;
    }
}
