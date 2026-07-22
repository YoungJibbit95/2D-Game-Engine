using System.Numerics;

namespace Game.Core.Particles;

[Flags]
public enum ParticleSimulationFlags : byte
{
    None = 0,
    CollideWithTiles = 1 << 0,
    AllowSleep = 1 << 1,
    KillOnCollision = 1 << 2
}

[Flags]
public enum ParticleStateFlags : byte
{
    None = 0,
    Sleeping = 1 << 0
}

[Flags]
public enum ParticleStepFlags : byte
{
    None = 0,
    UpdateBudgetExhausted = 1 << 0,
    CollisionBudgetExhausted = 1 << 1,
    CollisionIterationLimitReached = 1 << 2,
    EventCapacityExhausted = 1 << 3,
    InputSanitized = 1 << 4
}

public enum ParticlePhysicsEventKind : byte
{
    Collision,
    Expired,
    Slept,
    KilledOnCollision
}

public readonly record struct ParticleHandle(int Slot, uint Generation)
{
    public static ParticleHandle Invalid { get; } = new(-1, 0);

    public bool IsValid => Slot >= 0 && Generation != 0;
}

public readonly record struct ParticleForces(Vector2 Gravity, Vector2 Wind)
{
    public static ParticleForces None { get; } = new(Vector2.Zero, Vector2.Zero);
}

public readonly record struct ParticleStepBudget(
    int MaximumParticleUpdates,
    int MaximumTileTests,
    int MaximumCollisionsPerParticle)
{
    public static ParticleStepBudget Default { get; } = new(16_384, 131_072, 4);
}

public readonly record struct ParticleTileCollider(
    bool IsSolid,
    float Restitution,
    float Friction)
{
    public static ParticleTileCollider Solid { get; } = new(true, 0f, 0.65f);
}

/// <summary>
/// Supplies full-tile axis-aligned colliders to the particle solver. The solver owns
/// candidate enumeration and guarantees that calls never exceed the step tile-test budget.
/// Implementations must be deterministic and allocation-free for steady-state simulation.
/// </summary>
public interface IParticleTileCollisionAdapter
{
    float TileSize { get; }

    bool TryGetCollider(int tileX, int tileY, out ParticleTileCollider collider);
}

public readonly record struct ParticleSpawnCommand
{
    public Vector2 Position { get; init; }

    public Vector2 Velocity { get; init; }

    public Vector2 PositionVariance { get; init; }

    public Vector2 VelocityVariance { get; init; }

    public float LifetimeSeconds { get; init; }

    public float LifetimeVarianceSeconds { get; init; }

    public float Radius { get; init; }

    public float GravityScale { get; init; }

    public float LinearDrag { get; init; }

    public float Restitution { get; init; }

    public float Friction { get; init; }

    public float SleepSpeed { get; init; }

    public float SleepDelaySeconds { get; init; }

    public ulong Seed { get; init; }

    public ulong Sequence { get; init; }

    public int UserData { get; init; }

    public ParticleSimulationFlags Flags { get; init; }

    public static ParticleSpawnCommand Create(
        Vector2 position,
        Vector2 velocity,
        float lifetimeSeconds,
        ulong seed = 0)
    {
        return new ParticleSpawnCommand
        {
            Position = position,
            Velocity = velocity,
            LifetimeSeconds = lifetimeSeconds,
            Radius = 1f,
            GravityScale = 1f,
            LinearDrag = 0f,
            Restitution = 0.25f,
            Friction = 0.35f,
            SleepSpeed = 0.5f,
            SleepDelaySeconds = 0.2f,
            Seed = seed,
            Flags = ParticleSimulationFlags.CollideWithTiles
        };
    }
}

public readonly record struct ParticleSnapshot(
    ParticleHandle Handle,
    Vector2 Position,
    Vector2 Velocity,
    float AgeSeconds,
    float LifetimeSeconds,
    float Radius,
    int UserData,
    ParticleSimulationFlags SimulationFlags,
    ParticleStateFlags StateFlags);

public readonly record struct ParticlePhysicsEvent(
    ParticlePhysicsEventKind Kind,
    ParticleHandle Handle,
    int UserData,
    Vector2 Position,
    Vector2 Normal,
    Vector2 IncomingVelocity,
    Vector2 OutgoingVelocity,
    int TileX,
    int TileY);

public readonly record struct ParticleStepResult(
    int ActiveParticles,
    int UpdatedParticles,
    int TileTests,
    int Collisions,
    int ExpiredParticles,
    int SleepingParticles,
    int KilledParticles,
    int EventsWritten,
    int EventsDropped,
    ParticleStepFlags Flags)
{
    public bool UpdateBudgetExhausted =>
        (Flags & ParticleStepFlags.UpdateBudgetExhausted) != ParticleStepFlags.None;

    public bool CollisionBudgetExhausted =>
        (Flags & ParticleStepFlags.CollisionBudgetExhausted) != ParticleStepFlags.None;

    public bool InputWasSanitized =>
        (Flags & ParticleStepFlags.InputSanitized) != ParticleStepFlags.None;
}
