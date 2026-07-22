using System.Numerics;

namespace Game.Core.Particles;

public sealed class ParticlePhysicsWorld
{
    private const float MaximumPosition = 100_000_000f;
    private const float MaximumVelocity = 1_000_000f;
    private const float MaximumForce = 1_000_000f;
    private const float MaximumLifetime = 86_400f;
    private const float MaximumRadius = 4_096f;
    private const float MaximumDrag = 10_000f;
    private const float MaximumDeltaTime = 0.25f;
    private const float SeparationEpsilon = 0.001f;
    private const float SweepEpsilon = 0.000001f;
    private const int MaximumCollisionIterations = 16;
    private readonly ParticleSlot[] _slots;
    private readonly int[] _freeSlots;
    private int _activeCount;
    private int _freeCount;
    private int _nextUpdateSlot;

    public ParticlePhysicsWorld(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        _slots = new ParticleSlot[capacity];
        _freeSlots = new int[capacity];
        _freeCount = capacity;
        for (var index = 0; index < capacity; index++)
        {
            _freeSlots[index] = capacity - index - 1;
        }
    }

    public int Capacity => _slots.Length;

    public int ActiveCount => _activeCount;

    public int AvailableCapacity => _freeCount;

    public bool TrySpawn(in ParticleSpawnCommand command, out ParticleHandle handle)
    {
        if (_freeCount == 0)
        {
            handle = ParticleHandle.Invalid;
            return false;
        }

        var slotIndex = _freeSlots[--_freeCount];
        ref var slot = ref _slots[slotIndex];
        var generation = slot.Generation == 0 ? 1U : slot.Generation;
        slot = CreateSlot(command, generation);
        handle = new ParticleHandle(slotIndex, generation);
        _activeCount++;
        return true;
    }

    public bool TryGetParticle(ParticleHandle handle, out ParticleSnapshot snapshot)
    {
        if (!TryResolve(handle, out var slotIndex))
        {
            snapshot = default;
            return false;
        }

        snapshot = CreateSnapshot(slotIndex, in _slots[slotIndex]);
        return true;
    }

    public int CopyActiveParticles(Span<ParticleSnapshot> destination)
    {
        var written = 0;
        for (var slotIndex = 0; slotIndex < _slots.Length && written < destination.Length; slotIndex++)
        {
            ref readonly var slot = ref _slots[slotIndex];
            if (!slot.Active)
            {
                continue;
            }

            destination[written++] = CreateSnapshot(slotIndex, in slot);
        }

        return written;
    }

    public bool TryKill(ParticleHandle handle)
    {
        if (!TryResolve(handle, out var slotIndex))
        {
            return false;
        }

        ReleaseSlot(slotIndex);
        return true;
    }

    public bool TryWake(ParticleHandle handle)
    {
        if (!TryResolve(handle, out var slotIndex))
        {
            return false;
        }

        ref var slot = ref _slots[slotIndex];
        slot.Sleeping = false;
        slot.SleepTimer = 0f;
        return true;
    }

    public void Clear()
    {
        _activeCount = 0;
        _freeCount = _slots.Length;
        _nextUpdateSlot = 0;
        for (var index = 0; index < _slots.Length; index++)
        {
            ref var slot = ref _slots[index];
            var generation = NextGeneration(slot.Generation);
            slot = default;
            slot.Generation = generation;
            _freeSlots[index] = _slots.Length - index - 1;
        }
    }

    public ParticleStepResult Step(
        float deltaTimeSeconds,
        in ParticleForces forces,
        in ParticleStepBudget budget,
        IParticleTileCollisionAdapter? collisionAdapter,
        Span<ParticlePhysicsEvent> events)
    {
        var stepFlags = ParticleStepFlags.None;
        var deltaTime = SanitizeDeltaTime(deltaTimeSeconds, ref stepFlags);
        var gravity = SanitizeVector(forces.Gravity, MaximumForce, ref stepFlags);
        var wind = SanitizeVector(forces.Wind, MaximumForce, ref stepFlags);
        var maximumUpdates = Math.Clamp(budget.MaximumParticleUpdates, 0, _slots.Length);
        var maximumTileTests = Math.Max(0, budget.MaximumTileTests);
        var maximumCollisions = Math.Clamp(
            budget.MaximumCollisionsPerParticle,
            0,
            MaximumCollisionIterations);
        var tileSize = 0f;
        if (collisionAdapter is not null)
        {
            tileSize = collisionAdapter.TileSize;
            if (!float.IsFinite(tileSize) || tileSize <= 0f)
            {
                collisionAdapter = null;
                stepFlags |= ParticleStepFlags.InputSanitized;
            }
        }

        var activeAtStepStart = _activeCount;
        var updateTarget = Math.Min(activeAtStepStart, maximumUpdates);
        var updated = 0;
        var scanned = 0;
        var tileTests = 0;
        var collisions = 0;
        var expired = 0;
        var slept = 0;
        var killed = 0;
        var eventsWritten = 0;
        var eventsDropped = 0;
        var slotIndex = _nextUpdateSlot;

        while (scanned < _slots.Length && updated < updateTarget)
        {
            var currentSlotIndex = slotIndex;
            slotIndex++;
            if (slotIndex == _slots.Length)
            {
                slotIndex = 0;
            }

            scanned++;
            ref var slot = ref _slots[currentSlotIndex];
            if (!slot.Active)
            {
                continue;
            }

            updated++;
            slot.AgeSeconds = MathF.Min(MaximumLifetime, slot.AgeSeconds + deltaTime);
            if (slot.AgeSeconds >= slot.LifetimeSeconds)
            {
                WriteEvent(
                    events,
                    ref eventsWritten,
                    ref eventsDropped,
                    ref stepFlags,
                    CreateLifecycleEvent(ParticlePhysicsEventKind.Expired, currentSlotIndex, in slot));
                expired++;
                ReleaseSlot(currentSlotIndex);
                continue;
            }

            if (slot.Sleeping || deltaTime <= 0f)
            {
                continue;
            }

            var acceleration = (gravity * slot.GravityScale) + wind;
            acceleration = SanitizeVector(acceleration, MaximumForce, ref stepFlags);
            slot.Velocity += acceleration * deltaTime;
            var damping = 1f / (1f + (slot.LinearDrag * deltaTime));
            slot.Velocity = SanitizeVector(slot.Velocity * damping, MaximumVelocity, ref stepFlags);

            var collidedThisStep = false;
            var killedThisStep = false;
            if ((slot.Flags & ParticleSimulationFlags.CollideWithTiles) != 0 &&
                collisionAdapter is not null)
            {
                MoveWithTileCollisions(
                    currentSlotIndex,
                    ref slot,
                    deltaTime,
                    collisionAdapter,
                    tileSize,
                    maximumTileTests,
                    maximumCollisions,
                    events,
                    ref tileTests,
                    ref collisions,
                    ref eventsWritten,
                    ref eventsDropped,
                    ref stepFlags,
                    out collidedThisStep,
                    out killedThisStep);
            }
            else
            {
                slot.Position = SanitizeVector(
                    slot.Position + (slot.Velocity * deltaTime),
                    MaximumPosition,
                    ref stepFlags);
            }

            if (killedThisStep)
            {
                killed++;
                ReleaseSlot(currentSlotIndex);
                continue;
            }

            if (UpdateSleep(ref slot, deltaTime, collidedThisStep))
            {
                slept++;
                WriteEvent(
                    events,
                    ref eventsWritten,
                    ref eventsDropped,
                    ref stepFlags,
                    CreateLifecycleEvent(ParticlePhysicsEventKind.Slept, currentSlotIndex, in slot));
            }
        }

        _nextUpdateSlot = slotIndex;
        if (updated < activeAtStepStart)
        {
            stepFlags |= ParticleStepFlags.UpdateBudgetExhausted;
        }

        return new ParticleStepResult(
            _activeCount,
            updated,
            tileTests,
            collisions,
            expired,
            slept,
            killed,
            eventsWritten,
            eventsDropped,
            stepFlags);
    }

    private static ParticleSlot CreateSlot(in ParticleSpawnCommand command, uint generation)
    {
        var sanitized = ParticleStepFlags.None;
        var position = SanitizeVector(command.Position, MaximumPosition, ref sanitized);
        var velocity = SanitizeVector(command.Velocity, MaximumVelocity, ref sanitized);
        var positionVariance = SanitizeVariance(command.PositionVariance, MaximumPosition);
        var velocityVariance = SanitizeVariance(command.VelocityVariance, MaximumVelocity);
        var lifetime = SanitizePositive(command.LifetimeSeconds, 1f, MaximumLifetime);
        var lifetimeVariance = SanitizeNonNegative(
            command.LifetimeVarianceSeconds,
            0f,
            MaximumLifetime);
        var randomState = ParticleDeterministicRandom.CreateState(command.Seed, command.Sequence);
        position.X += positionVariance.X * ParticleDeterministicRandom.NextSigned(ref randomState);
        position.Y += positionVariance.Y * ParticleDeterministicRandom.NextSigned(ref randomState);
        velocity.X += velocityVariance.X * ParticleDeterministicRandom.NextSigned(ref randomState);
        velocity.Y += velocityVariance.Y * ParticleDeterministicRandom.NextSigned(ref randomState);
        lifetime += lifetimeVariance * ParticleDeterministicRandom.NextSigned(ref randomState);

        return new ParticleSlot
        {
            Active = true,
            Generation = generation,
            Position = ClampVector(position, MaximumPosition),
            Velocity = ClampVector(velocity, MaximumVelocity),
            LifetimeSeconds = Math.Clamp(lifetime, 0.001f, MaximumLifetime),
            Radius = SanitizePositive(command.Radius, 1f, MaximumRadius),
            GravityScale = SanitizeScalar(command.GravityScale, 0f, -64f, 64f),
            LinearDrag = SanitizeNonNegative(command.LinearDrag, 0f, MaximumDrag),
            Restitution = SanitizeScalar(command.Restitution, 0f, 0f, 1f),
            Friction = SanitizeScalar(command.Friction, 0f, 0f, 1f),
            SleepSpeedSquared = Square(SanitizeNonNegative(command.SleepSpeed, 0.5f, MaximumVelocity)),
            SleepDelaySeconds = SanitizeNonNegative(command.SleepDelaySeconds, 0.2f, 60f),
            UserData = command.UserData,
            Flags = command.Flags &
                (ParticleSimulationFlags.CollideWithTiles |
                 ParticleSimulationFlags.AllowSleep |
                 ParticleSimulationFlags.KillOnCollision)
        };
    }

    private static void MoveWithTileCollisions(
        int slotIndex,
        ref ParticleSlot slot,
        float deltaTime,
        IParticleTileCollisionAdapter collisionAdapter,
        float tileSize,
        int maximumTileTests,
        int maximumCollisions,
        Span<ParticlePhysicsEvent> events,
        ref int tileTests,
        ref int collisions,
        ref int eventsWritten,
        ref int eventsDropped,
        ref ParticleStepFlags stepFlags,
        out bool collidedThisStep,
        out bool killedThisStep)
    {
        collidedThisStep = false;
        killedThisStep = false;
        var remainingTime = deltaTime;
        var collisionIteration = 0;

        while (remainingTime > SweepEpsilon)
        {
            if (collisionIteration >= maximumCollisions)
            {
                if (slot.Velocity.LengthSquared() > SweepEpsilon)
                {
                    stepFlags |= ParticleStepFlags.CollisionIterationLimitReached;
                }

                return;
            }

            var displacement = slot.Velocity * remainingTime;
            if (displacement.LengthSquared() <= SweepEpsilon * SweepEpsilon)
            {
                return;
            }

            var sweep = FindEarliestCollision(
                slot.Position,
                displacement,
                slot.Radius,
                collisionAdapter,
                tileSize,
                maximumTileTests,
                ref tileTests);
            if (sweep.BudgetExhausted)
            {
                stepFlags |= ParticleStepFlags.CollisionBudgetExhausted;
                return;
            }

            if (!sweep.Hit)
            {
                slot.Position = ClampVector(slot.Position + displacement, MaximumPosition);
                return;
            }

            collidedThisStep = true;
            collisionIteration++;
            collisions++;
            var incomingVelocity = slot.Velocity;
            slot.Position = ClampVector(
                slot.Position + (displacement * sweep.TravelFraction) +
                (sweep.Normal * (sweep.SeparationDistance + SeparationEpsilon)),
                MaximumPosition);
            slot.Velocity = ResolveCollisionVelocity(
                slot.Velocity,
                sweep.Normal,
                slot.Restitution,
                slot.Friction,
                sweep.Collider);
            var handle = new ParticleHandle(slotIndex, slot.Generation);
            WriteEvent(
                events,
                ref eventsWritten,
                ref eventsDropped,
                ref stepFlags,
                new ParticlePhysicsEvent(
                    ParticlePhysicsEventKind.Collision,
                    handle,
                    slot.UserData,
                    slot.Position,
                    sweep.Normal,
                    incomingVelocity,
                    slot.Velocity,
                    sweep.TileX,
                    sweep.TileY));

            if ((slot.Flags & ParticleSimulationFlags.KillOnCollision) != 0)
            {
                killedThisStep = true;
                WriteEvent(
                    events,
                    ref eventsWritten,
                    ref eventsDropped,
                    ref stepFlags,
                    new ParticlePhysicsEvent(
                        ParticlePhysicsEventKind.KilledOnCollision,
                        handle,
                        slot.UserData,
                        slot.Position,
                        sweep.Normal,
                        incomingVelocity,
                        slot.Velocity,
                        sweep.TileX,
                        sweep.TileY));
                return;
            }

            remainingTime *= MathF.Max(0f, 1f - sweep.TravelFraction);
        }
    }

    private static ParticleSweepResult FindEarliestCollision(
        Vector2 start,
        Vector2 displacement,
        float radius,
        IParticleTileCollisionAdapter collisionAdapter,
        float tileSize,
        int maximumTileTests,
        ref int tileTests)
    {
        var end = start + displacement;
        var minimumX = ToTileCoordinate(MathF.Min(start.X, end.X) - radius, tileSize);
        var maximumX = ToTileCoordinate(MathF.Max(start.X, end.X) + radius, tileSize);
        var minimumY = ToTileCoordinate(MathF.Min(start.Y, end.Y) - radius, tileSize);
        var maximumY = ToTileCoordinate(MathF.Max(start.Y, end.Y) + radius, tileSize);
        var result = ParticleSweepResult.None;

        for (var tileY = minimumY; ; tileY++)
        {
            for (var tileX = minimumX; ; tileX++)
            {
                if (tileTests >= maximumTileTests)
                {
                    return ParticleSweepResult.Exhausted;
                }

                tileTests++;
                if (collisionAdapter.TryGetCollider(tileX, tileY, out var collider) && collider.IsSolid)
                {
                    collider = NormalizeCollider(collider);
                    var tileMinimum = new Vector2(tileX * tileSize - radius, tileY * tileSize - radius);
                    var tileMaximum = tileMinimum + new Vector2(tileSize + (radius * 2f));
                    if (SweepPointAgainstAabb(
                            start,
                            displacement,
                            tileMinimum,
                            tileMaximum,
                            out var travelFraction,
                            out var normal,
                            out var separationDistance) &&
                        (!result.Hit || travelFraction < result.TravelFraction - SweepEpsilon))
                    {
                        result = new ParticleSweepResult(
                            true,
                            false,
                            travelFraction,
                            separationDistance,
                            normal,
                            tileX,
                            tileY,
                            collider);
                    }
                }

                if (tileX == maximumX || tileX == int.MaxValue)
                {
                    break;
                }
            }

            if (tileY == maximumY || tileY == int.MaxValue)
            {
                break;
            }
        }

        return result;
    }

    private static bool SweepPointAgainstAabb(
        Vector2 start,
        Vector2 displacement,
        Vector2 minimum,
        Vector2 maximum,
        out float travelFraction,
        out Vector2 normal,
        out float separationDistance)
    {
        if (start.X > minimum.X && start.X < maximum.X &&
            start.Y > minimum.Y && start.Y < maximum.Y)
        {
            ResolveInitialOverlap(start, minimum, maximum, out normal, out separationDistance);
            travelFraction = 0f;
            return true;
        }

        if (!TryResolveSlab(start.X, displacement.X, minimum.X, maximum.X, out var nearX, out var farX) ||
            !TryResolveSlab(start.Y, displacement.Y, minimum.Y, maximum.Y, out var nearY, out var farY))
        {
            travelFraction = 0f;
            normal = Vector2.Zero;
            separationDistance = 0f;
            return false;
        }

        var near = MathF.Max(nearX, nearY);
        var far = MathF.Min(farX, farY);
        if (near > far || far < 0f || near > 1f)
        {
            travelFraction = 0f;
            normal = Vector2.Zero;
            separationDistance = 0f;
            return false;
        }

        travelFraction = Math.Clamp(near, 0f, 1f);
        separationDistance = 0f;
        if (nearX > nearY)
        {
            normal = displacement.X > 0f ? -Vector2.UnitX : Vector2.UnitX;
        }
        else
        {
            normal = displacement.Y > 0f ? -Vector2.UnitY : Vector2.UnitY;
        }

        return true;
    }

    private static bool TryResolveSlab(
        float start,
        float displacement,
        float minimum,
        float maximum,
        out float near,
        out float far)
    {
        if (MathF.Abs(displacement) <= SweepEpsilon)
        {
            near = float.NegativeInfinity;
            far = float.PositiveInfinity;
            return start >= minimum && start <= maximum;
        }

        var inverse = 1f / displacement;
        near = (minimum - start) * inverse;
        far = (maximum - start) * inverse;
        if (near > far)
        {
            (near, far) = (far, near);
        }

        return true;
    }

    private static void ResolveInitialOverlap(
        Vector2 point,
        Vector2 minimum,
        Vector2 maximum,
        out Vector2 normal,
        out float separationDistance)
    {
        var left = point.X - minimum.X;
        var right = maximum.X - point.X;
        var top = point.Y - minimum.Y;
        var bottom = maximum.Y - point.Y;
        var smallest = left;
        normal = -Vector2.UnitX;
        if (right < smallest)
        {
            smallest = right;
            normal = Vector2.UnitX;
        }

        if (top < smallest)
        {
            smallest = top;
            normal = -Vector2.UnitY;
        }

        if (bottom < smallest)
        {
            smallest = bottom;
            normal = Vector2.UnitY;
        }
        separationDistance = smallest;
    }

    private static Vector2 ResolveCollisionVelocity(
        Vector2 velocity,
        Vector2 normal,
        float restitution,
        float friction,
        ParticleTileCollider collider)
    {
        var normalSpeed = Vector2.Dot(velocity, normal);
        if (normalSpeed >= 0f)
        {
            return velocity;
        }

        var combinedRestitution = MathF.Max(restitution, collider.Restitution);
        var combinedFriction = MathF.Sqrt(friction * collider.Friction);
        var normalVelocity = normal * normalSpeed;
        var tangentVelocity = velocity - normalVelocity;
        return (-combinedRestitution * normalVelocity) +
               (tangentVelocity * MathF.Max(0f, 1f - combinedFriction));
    }

    private static bool UpdateSleep(ref ParticleSlot slot, float deltaTime, bool collidedThisStep)
    {
        if ((slot.Flags & ParticleSimulationFlags.AllowSleep) == 0 || !collidedThisStep)
        {
            slot.SleepTimer = 0f;
            return false;
        }

        if (slot.Velocity.LengthSquared() > slot.SleepSpeedSquared)
        {
            slot.SleepTimer = 0f;
            return false;
        }

        slot.SleepTimer += deltaTime;
        if (slot.SleepTimer + SweepEpsilon < slot.SleepDelaySeconds)
        {
            return false;
        }

        slot.Sleeping = true;
        slot.Velocity = Vector2.Zero;
        return true;
    }

    private bool TryResolve(ParticleHandle handle, out int slotIndex)
    {
        slotIndex = handle.Slot;
        return handle.IsValid &&
               (uint)slotIndex < (uint)_slots.Length &&
               _slots[slotIndex].Active &&
               _slots[slotIndex].Generation == handle.Generation;
    }

    private void ReleaseSlot(int slotIndex)
    {
        ref var slot = ref _slots[slotIndex];
        var generation = NextGeneration(slot.Generation);
        slot = default;
        slot.Generation = generation;
        _freeSlots[_freeCount++] = slotIndex;
        _activeCount--;
    }

    private ParticleSnapshot CreateSnapshot(int slotIndex, in ParticleSlot slot)
    {
        return new ParticleSnapshot(
            new ParticleHandle(slotIndex, slot.Generation),
            slot.Position,
            slot.Velocity,
            slot.AgeSeconds,
            slot.LifetimeSeconds,
            slot.Radius,
            slot.UserData,
            slot.Flags,
            slot.Sleeping ? ParticleStateFlags.Sleeping : ParticleStateFlags.None);
    }

    private static ParticlePhysicsEvent CreateLifecycleEvent(
        ParticlePhysicsEventKind kind,
        int slotIndex,
        in ParticleSlot slot)
    {
        return new ParticlePhysicsEvent(
            kind,
            new ParticleHandle(slotIndex, slot.Generation),
            slot.UserData,
            slot.Position,
            Vector2.Zero,
            slot.Velocity,
            slot.Velocity,
            0,
            0);
    }

    private static void WriteEvent(
        Span<ParticlePhysicsEvent> events,
        ref int eventsWritten,
        ref int eventsDropped,
        ref ParticleStepFlags flags,
        ParticlePhysicsEvent physicsEvent)
    {
        if (eventsWritten < events.Length)
        {
            events[eventsWritten++] = physicsEvent;
            return;
        }

        eventsDropped++;
        flags |= ParticleStepFlags.EventCapacityExhausted;
    }

    private static ParticleTileCollider NormalizeCollider(ParticleTileCollider collider)
    {
        return collider with
        {
            Restitution = SanitizeScalar(collider.Restitution, 0f, 0f, 1f),
            Friction = SanitizeScalar(collider.Friction, 0f, 0f, 1f)
        };
    }

    private static float SanitizeDeltaTime(float value, ref ParticleStepFlags flags)
    {
        if (!float.IsFinite(value) || value <= 0f)
        {
            if (!float.IsFinite(value) || value < 0f)
            {
                flags |= ParticleStepFlags.InputSanitized;
            }

            return 0f;
        }

        if (value > MaximumDeltaTime)
        {
            flags |= ParticleStepFlags.InputSanitized;
            return MaximumDeltaTime;
        }

        return value;
    }

    private static Vector2 SanitizeVector(
        Vector2 value,
        float maximumMagnitudePerAxis,
        ref ParticleStepFlags flags)
    {
        var x = value.X;
        var y = value.Y;
        if (!float.IsFinite(x))
        {
            x = 0f;
            flags |= ParticleStepFlags.InputSanitized;
        }

        if (!float.IsFinite(y))
        {
            y = 0f;
            flags |= ParticleStepFlags.InputSanitized;
        }

        var clampedX = Math.Clamp(x, -maximumMagnitudePerAxis, maximumMagnitudePerAxis);
        var clampedY = Math.Clamp(y, -maximumMagnitudePerAxis, maximumMagnitudePerAxis);
        if (clampedX != x || clampedY != y)
        {
            flags |= ParticleStepFlags.InputSanitized;
        }

        return new Vector2(clampedX, clampedY);
    }

    private static Vector2 SanitizeVariance(Vector2 value, float maximum)
    {
        return new Vector2(
            SanitizeNonNegative(MathF.Abs(value.X), 0f, maximum),
            SanitizeNonNegative(MathF.Abs(value.Y), 0f, maximum));
    }

    private static Vector2 ClampVector(Vector2 value, float maximum)
    {
        return new Vector2(
            Math.Clamp(float.IsFinite(value.X) ? value.X : 0f, -maximum, maximum),
            Math.Clamp(float.IsFinite(value.Y) ? value.Y : 0f, -maximum, maximum));
    }

    private static float SanitizePositive(float value, float fallback, float maximum)
    {
        return float.IsFinite(value) && value > 0f
            ? MathF.Min(value, maximum)
            : fallback;
    }

    private static float SanitizeNonNegative(float value, float fallback, float maximum)
    {
        return float.IsFinite(value) && value >= 0f
            ? MathF.Min(value, maximum)
            : fallback;
    }

    private static float SanitizeScalar(float value, float fallback, float minimum, float maximum)
    {
        return float.IsFinite(value) ? Math.Clamp(value, minimum, maximum) : fallback;
    }

    private static int ToTileCoordinate(float value, float tileSize)
    {
        var coordinate = Math.Floor((double)value / tileSize);
        return coordinate <= int.MinValue
            ? int.MinValue
            : coordinate >= int.MaxValue
                ? int.MaxValue
                : (int)coordinate;
    }

    private static uint NextGeneration(uint generation)
    {
        generation++;
        return generation == 0 ? 1U : generation;
    }

    private static float Square(float value)
    {
        return value * value;
    }

    private struct ParticleSlot
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float AgeSeconds;
        public float LifetimeSeconds;
        public float Radius;
        public float GravityScale;
        public float LinearDrag;
        public float Restitution;
        public float Friction;
        public float SleepSpeedSquared;
        public float SleepDelaySeconds;
        public float SleepTimer;
        public uint Generation;
        public int UserData;
        public ParticleSimulationFlags Flags;
        public bool Active;
        public bool Sleeping;
    }

    private readonly record struct ParticleSweepResult(
        bool Hit,
        bool BudgetExhausted,
        float TravelFraction,
        float SeparationDistance,
        Vector2 Normal,
        int TileX,
        int TileY,
        ParticleTileCollider Collider)
    {
        public static ParticleSweepResult None { get; } = new(
            false,
            false,
            1f,
            0f,
            Vector2.Zero,
            0,
            0,
            default);

        public static ParticleSweepResult Exhausted { get; } = new(
            false,
            true,
            0f,
            0f,
            Vector2.Zero,
            0,
            0,
            default);
    }
}
