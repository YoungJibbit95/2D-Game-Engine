using System.Numerics;
using Game.Core.Physics;
using GameWorld = Game.Core.World.World;

namespace Game.Core.Movement;

/// <summary>
/// Converts top-down control intent into velocity and delegates collision integration to physics.
/// </summary>
public sealed class TopDownMovementController
{
    private readonly TileCollisionResolver _collisionResolver;

    public TopDownMovementController(TileCollisionResolver? collisionResolver = null)
    {
        _collisionResolver = collisionResolver ?? new TileCollisionResolver();
    }

    public void Move(
        GameWorld world,
        PhysicsBody body,
        Vector2 direction,
        float deltaSeconds,
        TopDownMovementOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(body);

        options ??= TopDownMovementOptions.Default;

        if (!float.IsFinite(deltaSeconds) || deltaSeconds <= 0)
        {
            body.Velocity = Vector2.Zero;
            return;
        }

        var movement = TopDownMovementMath.ResolveDirection(direction, options);
        body.Velocity = movement * TopDownMovementMath.ResolveSpeed(options);
        _collisionResolver.Move(world, body, deltaSeconds);
        body.OnGround = false;
    }
}
