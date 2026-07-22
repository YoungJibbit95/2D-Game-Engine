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

    public bool CollidesWithTiles { get; set; } = true;

    public PhysicsBodyType BodyType { get; init; } = PhysicsBodyType.Dynamic;

    public PhysicsCollisionLayer CollisionLayer { get; init; } = PhysicsCollisionLayer.Default;

    public PhysicsCollisionLayer CollisionMask { get; set; } = PhysicsCollisionLayer.All;

    public PhysicsMaterial Material { get; init; } = PhysicsMaterial.Legacy;

    /// <summary>
    /// Stable authoritative ordering key used only to break otherwise identical
    /// broadphase/narrowphase ties. Entity-owned bodies bind this to their entity ID.
    /// Standalone callers may leave the default value when input order is already stable.
    /// </summary>
    public long DeterministicOrder { get; set; }

    public float Mass { get; set; } = 1f;

    public float KnockbackResistance { get; set; }

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


    public Vector2 ApplyKnockback(
        Vector2 direction,
        float impulseMagnitude,
        float maximumVelocityChange = 720f)
    {
        if (BodyType != PhysicsBodyType.Dynamic ||
            !IsFinite(direction) ||
            direction.LengthSquared() <= float.Epsilon ||
            !float.IsFinite(impulseMagnitude) ||
            impulseMagnitude <= 0f ||
            !float.IsFinite(maximumVelocityChange) ||
            maximumVelocityChange <= 0f)
        {
            return Vector2.Zero;
        }

        var inverseMass = InverseMass;
        if (!float.IsFinite(inverseMass) || inverseMass <= 0f)
        {
            return Vector2.Zero;
        }

        var resistance = float.IsFinite(KnockbackResistance)
            ? Math.Clamp(KnockbackResistance, 0f, 1f)
            : 1f;
        var requestedVelocityChange = impulseMagnitude * inverseMass * (1f - resistance);
        if (!float.IsFinite(requestedVelocityChange) || requestedVelocityChange <= 0f)
        {
            return Vector2.Zero;
        }

        var appliedVelocityChange = Math.Min(requestedVelocityChange, maximumVelocityChange);
        var length = Math.Sqrt(
            (double)direction.X * direction.X +
            (double)direction.Y * direction.Y);
        var scale = appliedVelocityChange / length;
        var velocityDelta = new Vector2(
            (float)(direction.X * scale),
            (float)(direction.Y * scale));
        Velocity += velocityDelta;
        return velocityDelta;
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
