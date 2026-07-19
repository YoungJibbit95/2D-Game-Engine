using System.Numerics;

namespace Game.Core.Physics;

public enum PhysicsBodyType : byte
{
    Static,
    Kinematic,
    Dynamic
}

[Flags]
public enum PhysicsCollisionLayer : uint
{
    None = 0,
    Default = 1 << 0,
    Player = 1 << 1,
    Enemy = 1 << 2,
    Item = 1 << 3,
    Projectile = 1 << 4,
    World = 1 << 5,
    Sensor = 1 << 6,
    All = uint.MaxValue
}

[Flags]
public enum PhysicsContactFlags : byte
{
    None = 0,
    Ground = 1 << 0,
    Ceiling = 1 << 1,
    LeftWall = 1 << 2,
    RightWall = 1 << 3,
    WorkBudgetExhausted = 1 << 4,
    InitialOverlapRecovered = 1 << 5,
    InitialOverlapUnresolved = 1 << 6
}

public readonly record struct PhysicsMaterial(
    float Friction,
    float Restitution)
{
    public static PhysicsMaterial Legacy { get; } = new(0f, 0f);

    public static PhysicsMaterial Default { get; } = new(0.55f, 0f);

    public PhysicsMaterial Normalized()
    {
        return new PhysicsMaterial(
            float.IsFinite(Friction) ? Math.Clamp(Friction, 0f, 1f) : 0f,
            float.IsFinite(Restitution) ? Math.Clamp(Restitution, 0f, 1f) : 0f);
    }
}

public readonly record struct PhysicsContact(
    int TileX,
    int TileY,
    Vector2 Point,
    Vector2 Normal,
    float TravelFraction,
    PhysicsContactFlags Flags);

public readonly record struct PhysicsMoveResult(
    Vector2 StartPosition,
    Vector2 RequestedDisplacement,
    Vector2 ActualDisplacement,
    Vector2 FinalVelocity,
    PhysicsContactFlags ContactFlags,
    int ContactsFound,
    int ContactsWritten,
    int TilesTested,
    int Substeps)
{
    public bool Collided => (ContactFlags & ~PhysicsContactFlags.WorkBudgetExhausted) != 0;

    public bool WorkBudgetExhausted =>
        (ContactFlags & PhysicsContactFlags.WorkBudgetExhausted) != PhysicsContactFlags.None;
}

public readonly record struct TileCollisionSettings(
    float MaxSubstepDistance,
    int MaxSubsteps,
    int MaxTileTests)
{
    public static TileCollisionSettings Default { get; } = new(
        GameConstants.TileSize,
        32,
        16_384);

    public TileCollisionSettings Validate()
    {
        if (!float.IsFinite(MaxSubstepDistance) || MaxSubstepDistance <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxSubstepDistance));
        }

        if (MaxSubsteps <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxSubsteps));
        }

        if (MaxTileTests <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxTileTests));
        }

        return this;
    }
}

public readonly record struct PhysicsBodyPair(int BodyAIndex, int BodyBIndex);

public readonly record struct PhysicsBodyContact(
    int BodyAIndex,
    int BodyBIndex,
    Vector2 Point,
    Vector2 Normal,
    float Penetration,
    float NormalImpulse,
    float TangentImpulse);

public readonly record struct PhysicsBroadphaseSettings(
    int MaximumBodies,
    int MaximumPairTests,
    bool IncludeStaticStaticPairs)
{
    public static PhysicsBroadphaseSettings Default { get; } = new(
        4_096,
        262_144,
        false);

    public PhysicsBroadphaseSettings Validate()
    {
        if (MaximumBodies <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaximumBodies));
        }

        if (MaximumPairTests <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaximumPairTests));
        }

        return this;
    }
}

public readonly record struct PhysicsBroadphaseTelemetry(
    int BodiesRequested,
    int BodiesIndexed,
    int BodiesDeferred,
    int PairTests,
    int PairsFound,
    int PairsWritten,
    bool PairBudgetExhausted);
