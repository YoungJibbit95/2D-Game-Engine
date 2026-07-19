using System.Numerics;
using Game.Core.World;

namespace Game.Core.Physics;

public sealed class PhysicsBody
{
    private Vector2 _accumulatedForce;

    public Vector2 Position { get; set; }

    public Vector2 Velocity { get; set; }

    public Vector2 Size { get; init; } = new(12, 28);

    public bool OnGround { get; set; }

    public bool CollidesWithTiles { get; init; } = true;

    public PhysicsBodyType BodyType { get; init; } = PhysicsBodyType.Dynamic;

    public PhysicsCollisionLayer CollisionLayer { get; init; } = PhysicsCollisionLayer.Default;

    public PhysicsCollisionLayer CollisionMask { get; init; } = PhysicsCollisionLayer.All;

    public PhysicsMaterial Material { get; init; } = PhysicsMaterial.Legacy;

    /// <summary>
    /// Stable authoritative ordering key used only to break otherwise identical
    /// broadphase/narrowphase ties. Entity-owned bodies bind this to their entity ID.
    /// Standalone callers may leave the default value when input order is already stable.
    /// </summary>
    public long DeterministicOrder { get; set; }

    public float Mass { get; init; } = 1f;

    public float GravityScale { get; set; } = 1f;

    public float LinearDamping { get; set; }

    public Vector2 MaximumAbsoluteVelocity { get; init; } = new(
        float.PositiveInfinity,
        float.PositiveInfinity);

    public float InverseMass => BodyType == PhysicsBodyType.Dynamic && Mass > 0f
        ? 1f / Mass
        : 0f;

    public Vector2 AccumulatedForce => _accumulatedForce;

    public Vector2 Center => Position + Size * 0.5f;

    public RectI Bounds => new(
        (int)MathF.Floor(Position.X),
        (int)MathF.Floor(Position.Y),
        (int)MathF.Ceiling(Size.X),
        (int)MathF.Ceiling(Size.Y));

    public void AddForce(Vector2 force)
    {
        if (BodyType == PhysicsBodyType.Dynamic && IsFinite(force))
        {
            _accumulatedForce += force;
        }
    }

    public void ApplyImpulse(Vector2 impulse)
    {
        if (BodyType == PhysicsBodyType.Dynamic && IsFinite(impulse))
        {
            Velocity += impulse * InverseMass;
        }
    }

    public void ClearForces()
    {
        _accumulatedForce = Vector2.Zero;
    }

    private static bool IsFinite(Vector2 value)
    {
        return float.IsFinite(value.X) && float.IsFinite(value.Y);
    }
}
