using System.Numerics;
using GameWorld = Game.Core.World.World;

namespace Game.Core.Physics;

public readonly record struct PhysicsStepSettings(
    Vector2 Gravity,
    float MaximumDeltaSeconds,
    float MaximumLinearSpeed,
    int MaximumBodiesPerStep,
    int ContactsPerBody)
{
    public static PhysicsStepSettings Default { get; } = new(
        new Vector2(0f, 1_500f),
        1f / 15f,
        4_096f,
        4_096,
        4);

    public PhysicsStepSettings Validate()
    {
        if (!IsFinite(Gravity))
        {
            throw new ArgumentOutOfRangeException(nameof(Gravity));
        }

        if (!float.IsFinite(MaximumDeltaSeconds) || MaximumDeltaSeconds <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(MaximumDeltaSeconds));
        }

        if (!float.IsFinite(MaximumLinearSpeed) || MaximumLinearSpeed <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(MaximumLinearSpeed));
        }

        if (MaximumBodiesPerStep <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaximumBodiesPerStep));
        }

        if (ContactsPerBody < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ContactsPerBody));
        }

        if ((long)MaximumBodiesPerStep * ContactsPerBody > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(ContactsPerBody), "The fixed contact layout is too large.");
        }

        return this;
    }

    private static bool IsFinite(Vector2 value)
    {
        return float.IsFinite(value.X) && float.IsFinite(value.Y);
    }
}

public readonly record struct PhysicsStepTelemetry(
    int BodiesRequested,
    int BodiesSimulated,
    int BodiesDeferred,
    int DynamicBodies,
    int KinematicBodies,
    int StaticBodies,
    int ContactsFound,
    int ContactsWritten,
    int TilesTested,
    int TileBudgetExhaustions,
    float RequestedDeltaSeconds,
    float SimulatedDeltaSeconds)
{
    public bool DeltaClamped => SimulatedDeltaSeconds != RequestedDeltaSeconds;

    public int FirstBodyIndex { get; init; }

    public int NextBodyIndex { get; init; }

    public int BodyPairTests { get; init; }

    public int BodyPairsFound { get; init; }

    public int BodyPairsResolved { get; init; }

    public int BodyPairImpulses { get; init; }

    public int BodyPairPositionCorrections { get; init; }
}

public sealed class PhysicsWorld
{
    private readonly TileCollisionResolver _collisionResolver;
    private readonly PhysicsBroadphase _broadphase;
    private readonly PhysicsStepSettings _settings;

    public PhysicsWorld(
        TileCollisionResolver? collisionResolver = null,
        PhysicsStepSettings? settings = null,
        PhysicsBroadphaseSettings? broadphaseSettings = null)
    {
        _collisionResolver = collisionResolver ?? new TileCollisionResolver();
        _broadphase = new PhysicsBroadphase(broadphaseSettings);
        _settings = (settings ?? PhysicsStepSettings.Default).Validate();
    }

    public PhysicsStepSettings Settings => _settings;

    public PhysicsBroadphaseTelemetry QueryOverlaps(
        ReadOnlySpan<PhysicsBody> bodies,
        Span<int> sortedBodyIndices,
        Span<PhysicsBodyPair> pairs)
    {
        return _broadphase.QueryOverlaps(bodies, sortedBodyIndices, pairs);
    }

    public PhysicsStepTelemetry Step(
        GameWorld world,
        ReadOnlySpan<PhysicsBody> bodies,
        float deltaSeconds,
        Span<PhysicsMoveResult> results,
        Span<PhysicsContact> contacts)
    {
        ValidateStep(world, bodies, deltaSeconds, results);
        var simulatedDelta = Math.Min(deltaSeconds, _settings.MaximumDeltaSeconds);
        return StepValidated(world, bodies, deltaSeconds, simulatedDelta, results, contacts);
    }

    public PhysicsStepTelemetry StepWithBodyCollisions(
        GameWorld world,
        ReadOnlySpan<PhysicsBody> bodies,
        float deltaSeconds,
        Span<PhysicsMoveResult> results,
        Span<PhysicsContact> tileContacts,
        Span<int> sortedBodyIndices,
        Span<PhysicsBodyPair> bodyPairs,
        Span<PhysicsBodyContact> bodyContacts)
    {
        ValidateStep(world, bodies, deltaSeconds, results);
        if (sortedBodyIndices.Length < bodies.Length)
        {
            throw new ArgumentException(
                "Broadphase index storage must contain one slot for every submitted body.",
                nameof(sortedBodyIndices));
        }

        if (bodyContacts.Length < bodyPairs.Length)
        {
            throw new ArgumentException(
                "Body-contact storage must contain one slot for every body-pair slot.",
                nameof(bodyContacts));
        }

        var broadphase = _broadphase.QueryOverlaps(bodies, sortedBodyIndices, bodyPairs);
        if (broadphase.BodiesDeferred != 0 ||
            broadphase.PairBudgetExhausted ||
            broadphase.PairsFound != broadphase.PairsWritten)
        {
            throw new InvalidOperationException(
                $"Body collision workspace is incomplete: indexed {broadphase.BodiesIndexed}/{broadphase.BodiesRequested} bodies, " +
                $"wrote {broadphase.PairsWritten}/{broadphase.PairsFound} pairs after {broadphase.PairTests} tests. " +
                "Increase the explicit broadphase/pair capacities; authoritative contacts are never deferred.");
        }

        var resolution = ResolveBodyPairs(
            world,
            bodies,
            bodyPairs[..broadphase.PairsWritten],
            bodyContacts[..broadphase.PairsWritten]);
        var simulatedDelta = Math.Min(deltaSeconds, _settings.MaximumDeltaSeconds);
        var telemetry = StepValidated(
            world,
            bodies,
            deltaSeconds,
            simulatedDelta,
            results,
            tileContacts);
        return telemetry with
        {
            BodyPairTests = broadphase.PairTests,
            BodyPairsFound = broadphase.PairsFound,
            BodyPairsResolved = resolution.ContactsResolved,
            BodyPairImpulses = resolution.ImpulsesApplied,
            BodyPairPositionCorrections = resolution.PositionCorrections
        };
    }

    private PhysicsStepTelemetry StepValidated(
        GameWorld world,
        ReadOnlySpan<PhysicsBody> bodies,
        float requestedDeltaSeconds,
        float simulatedDelta,
        Span<PhysicsMoveResult> results,
        Span<PhysicsContact> contacts)
    {
        var bodyCount = bodies.Length;
        const int firstBodyIndex = 0;
        var dynamicBodies = 0;
        var kinematicBodies = 0;
        var staticBodies = 0;
        var contactsFound = 0;
        var contactsWritten = 0;
        var tilesTested = 0;
        var budgetExhaustions = 0;

        for (var resultIndex = 0; resultIndex < bodyCount; resultIndex++)
        {
            var body = bodies[resultIndex];
            var contactOffset = resultIndex * _settings.ContactsPerBody;
            var availableContacts = contactOffset < contacts.Length
                ? Math.Min(_settings.ContactsPerBody, contacts.Length - contactOffset)
                : 0;
            var bodyContacts = availableContacts > 0
                ? contacts.Slice(contactOffset, availableContacts)
                : Span<PhysicsContact>.Empty;

            CountBodyType(body, ref dynamicBodies, ref kinematicBodies, ref staticBodies);
            var result = StepBodyValidated(world, body, simulatedDelta, bodyContacts);
            results[resultIndex] = result;
            contactsFound += result.ContactsFound;
            contactsWritten += result.ContactsWritten;
            tilesTested += result.TilesTested;
            if (result.WorkBudgetExhausted)
            {
                budgetExhaustions++;
            }
        }

        return new PhysicsStepTelemetry(
            bodies.Length,
            bodyCount,
            0,
            dynamicBodies,
            kinematicBodies,
            staticBodies,
            contactsFound,
            contactsWritten,
            tilesTested,
            budgetExhaustions,
            requestedDeltaSeconds,
            simulatedDelta)
        {
            FirstBodyIndex = firstBodyIndex,
            NextBodyIndex = 0
        };
    }

    private void ValidateStep(
        GameWorld world,
        ReadOnlySpan<PhysicsBody> bodies,
        float deltaSeconds,
        Span<PhysicsMoveResult> results)
    {
        ArgumentNullException.ThrowIfNull(world);

        if (!float.IsFinite(deltaSeconds) || deltaSeconds < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
        }

        if (bodies.Length > _settings.MaximumBodiesPerStep)
        {
            throw new InvalidOperationException(
                $"Physics step received {bodies.Length} bodies but its fixed capacity is {_settings.MaximumBodiesPerStep}. " +
                "Submit explicit complete batches every tick; authoritative time is never silently deferred.");
        }

        if (results.Length < bodies.Length)
        {
            throw new ArgumentException(
                "Physics result storage must contain one slot for every submitted body.",
                nameof(results));
        }

        for (var bodyIndex = 0; bodyIndex < bodies.Length; bodyIndex++)
        {
            ValidateBody(bodies[bodyIndex], nameof(bodies));
        }
    }

    private BodyPairResolutionTelemetry ResolveBodyPairs(
        GameWorld world,
        ReadOnlySpan<PhysicsBody> bodies,
        ReadOnlySpan<PhysicsBodyPair> pairs,
        Span<PhysicsBodyContact> contacts)
    {
        var contactsResolved = 0;
        var impulsesApplied = 0;
        var positionCorrections = 0;
        for (var pairIndex = 0; pairIndex < pairs.Length; pairIndex++)
        {
            var pair = pairs[pairIndex];
            var bodyA = bodies[pair.BodyAIndex];
            var bodyB = bodies[pair.BodyBIndex];
            if (!TryBuildBodyContact(bodyA, bodyB, pair, out var point, out var normal, out var penetration))
            {
                continue;
            }

            var inverseMassA = bodyA.InverseMass;
            var inverseMassB = bodyB.InverseMass;
            var inverseMassSum = inverseMassA + inverseMassB;
            var normalImpulse = 0f;
            var tangentImpulse = 0f;
            if (inverseMassSum > 0f)
            {
                const float penetrationSlop = 0.001f;
                const float correctionPercent = 0.8f;
                var correctionDistance = Math.Max(0f, penetration - penetrationSlop) * correctionPercent;
                if (correctionDistance > 0f &&
                    TryApplyPositionCorrection(
                        world,
                        bodyA,
                        bodyB,
                        normal,
                        correctionDistance,
                        inverseMassA,
                        inverseMassB,
                        inverseMassSum))
                {
                    positionCorrections++;
                }

                var relativeVelocity = bodyB.Velocity - bodyA.Velocity;
                var velocityAlongNormal = Vector2.Dot(relativeVelocity, normal);
                if (velocityAlongNormal < 0f)
                {
                    var materialA = bodyA.Material.Normalized();
                    var materialB = bodyB.Material.Normalized();
                    var restitution = Math.Min(materialA.Restitution, materialB.Restitution);
                    normalImpulse = -(1f + restitution) * velocityAlongNormal / inverseMassSum;
                    var impulse = normal * normalImpulse;
                    bodyA.Velocity -= impulse * inverseMassA;
                    bodyB.Velocity += impulse * inverseMassB;

                    relativeVelocity = bodyB.Velocity - bodyA.Velocity;
                    var tangent = relativeVelocity - Vector2.Dot(relativeVelocity, normal) * normal;
                    var tangentLengthSquared = tangent.LengthSquared();
                    if (tangentLengthSquared > 0.000001f)
                    {
                        tangent /= MathF.Sqrt(tangentLengthSquared);
                        var unclampedTangentImpulse = -Vector2.Dot(relativeVelocity, tangent) / inverseMassSum;
                        var friction = MathF.Sqrt(materialA.Friction * materialB.Friction);
                        tangentImpulse = Math.Clamp(
                            unclampedTangentImpulse,
                            -normalImpulse * friction,
                            normalImpulse * friction);
                        var frictionImpulse = tangent * tangentImpulse;
                        bodyA.Velocity -= frictionImpulse * inverseMassA;
                        bodyB.Velocity += frictionImpulse * inverseMassB;
                    }

                    impulsesApplied++;
                }
            }

            contacts[contactsResolved++] = new PhysicsBodyContact(
                pair.BodyAIndex,
                pair.BodyBIndex,
                point,
                normal,
                penetration,
                normalImpulse,
                tangentImpulse);
        }

        return new BodyPairResolutionTelemetry(
            contactsResolved,
            impulsesApplied,
            positionCorrections);
    }

    private bool TryApplyPositionCorrection(
        GameWorld world,
        PhysicsBody bodyA,
        PhysicsBody bodyB,
        Vector2 normal,
        float correctionDistance,
        float inverseMassA,
        float inverseMassB,
        float inverseMassSum)
    {
        var originalA = bodyA.Position;
        var originalB = bodyB.Position;
        var candidateA = originalA - normal * (correctionDistance * inverseMassA / inverseMassSum);
        var candidateB = originalB + normal * (correctionDistance * inverseMassB / inverseMassSum);
        var canMoveA = inverseMassA > 0f && _collisionResolver.CanOccupy(world, bodyA, candidateA);
        var canMoveB = inverseMassB > 0f && _collisionResolver.CanOccupy(world, bodyB, candidateB);

        if (canMoveA && canMoveB)
        {
            bodyA.Position = candidateA;
            bodyB.Position = candidateB;
            return true;
        }

        if (canMoveA)
        {
            candidateA = originalA - normal * correctionDistance;
            if (_collisionResolver.CanOccupy(world, bodyA, candidateA))
            {
                bodyA.Position = candidateA;
                return true;
            }
        }

        if (canMoveB)
        {
            candidateB = originalB + normal * correctionDistance;
            if (_collisionResolver.CanOccupy(world, bodyB, candidateB))
            {
                bodyB.Position = candidateB;
                return true;
            }
        }

        return false;
    }

    private static bool TryBuildBodyContact(
        PhysicsBody bodyA,
        PhysicsBody bodyB,
        PhysicsBodyPair pair,
        out Vector2 point,
        out Vector2 normal,
        out float penetration)
    {
        var overlapLeft = Math.Max(bodyA.Position.X, bodyB.Position.X);
        var overlapTop = Math.Max(bodyA.Position.Y, bodyB.Position.Y);
        var overlapRight = Math.Min(bodyA.Position.X + bodyA.Size.X, bodyB.Position.X + bodyB.Size.X);
        var overlapBottom = Math.Min(bodyA.Position.Y + bodyA.Size.Y, bodyB.Position.Y + bodyB.Size.Y);
        var penetrationX = overlapRight - overlapLeft;
        var penetrationY = overlapBottom - overlapTop;
        if (penetrationX <= 0f || penetrationY <= 0f)
        {
            point = default;
            normal = default;
            penetration = 0f;
            return false;
        }

        var centerDelta = bodyB.Center - bodyA.Center;
        var resolveHorizontally = penetrationX < penetrationY ||
                                  (penetrationX == penetrationY &&
                                   Math.Abs(centerDelta.X) >= Math.Abs(centerDelta.Y));
        if (resolveHorizontally)
        {
            normal = new Vector2(ResolveNormalSign(centerDelta.X, bodyA, bodyB, pair), 0f);
            penetration = penetrationX;
        }
        else
        {
            normal = new Vector2(0f, ResolveNormalSign(centerDelta.Y, bodyA, bodyB, pair));
            penetration = penetrationY;
        }

        point = new Vector2(
            (overlapLeft + overlapRight) * 0.5f,
            (overlapTop + overlapBottom) * 0.5f);
        return true;
    }

    private static float ResolveNormalSign(
        float centerDelta,
        PhysicsBody bodyA,
        PhysicsBody bodyB,
        PhysicsBodyPair pair)
    {
        if (centerDelta != 0f)
        {
            return centerDelta > 0f ? 1f : -1f;
        }

        if (bodyA.DeterministicOrder != bodyB.DeterministicOrder)
        {
            return bodyA.DeterministicOrder < bodyB.DeterministicOrder ? 1f : -1f;
        }

        return pair.BodyAIndex < pair.BodyBIndex ? 1f : -1f;
    }

    private readonly record struct BodyPairResolutionTelemetry(
        int ContactsResolved,
        int ImpulsesApplied,
        int PositionCorrections);

    public PhysicsMoveResult StepBody(
        GameWorld world,
        PhysicsBody body,
        float deltaSeconds,
        Span<PhysicsContact> contacts)
    {
        ArgumentNullException.ThrowIfNull(world);

        if (!float.IsFinite(deltaSeconds) || deltaSeconds < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
        }

        ValidateBody(body, nameof(body));
        return StepBodyValidated(
            world,
            body,
            Math.Min(deltaSeconds, _settings.MaximumDeltaSeconds),
            contacts);
    }

    private PhysicsMoveResult StepBodyValidated(
        GameWorld world,
        PhysicsBody body,
        float deltaSeconds,
        Span<PhysicsContact> contacts)
    {
        if (body.BodyType == PhysicsBodyType.Static)
        {
            body.ClearForces();
            return CreateStationaryResult(body);
        }

        if (body.BodyType == PhysicsBodyType.Dynamic)
        {
            IntegrateVelocity(body, deltaSeconds);
        }

        var result = _collisionResolver.MoveDetailed(world, body, deltaSeconds, contacts);
        body.ClearForces();
        return result;
    }

    private void IntegrateVelocity(PhysicsBody body, float deltaSeconds)
    {
        var acceleration = _settings.Gravity * body.GravityScale + body.AccumulatedForce * body.InverseMass;
        var velocity = body.Velocity + acceleration * deltaSeconds;
        if (body.LinearDamping > 0f)
        {
            velocity *= Math.Max(0f, 1f - body.LinearDamping * deltaSeconds);
        }

        velocity = new Vector2(
            Math.Clamp(
                velocity.X,
                -body.MaximumAbsoluteVelocity.X,
                body.MaximumAbsoluteVelocity.X),
            Math.Clamp(
                velocity.Y,
                -body.MaximumAbsoluteVelocity.Y,
                body.MaximumAbsoluteVelocity.Y));

        var speedSquared = velocity.LengthSquared();
        var maximumSpeedSquared = _settings.MaximumLinearSpeed * _settings.MaximumLinearSpeed;
        if (speedSquared > maximumSpeedSquared)
        {
            velocity *= _settings.MaximumLinearSpeed / MathF.Sqrt(speedSquared);
        }

        body.Velocity = velocity;
    }

    private static void CountBodyType(
        PhysicsBody body,
        ref int dynamicBodies,
        ref int kinematicBodies,
        ref int staticBodies)
    {
        switch (body.BodyType)
        {
            case PhysicsBodyType.Static:
                staticBodies++;
                break;
            case PhysicsBodyType.Kinematic:
                kinematicBodies++;
                break;
            case PhysicsBodyType.Dynamic:
                dynamicBodies++;
                break;
        }
    }

    private static void ValidateBody(PhysicsBody? body, string parameterName)
    {
        if (body is null)
        {
            throw new ArgumentException("Physics body spans must not contain null entries.", parameterName);
        }

        if (body.BodyType != PhysicsBodyType.Static &&
            body.BodyType != PhysicsBodyType.Kinematic &&
            body.BodyType != PhysicsBodyType.Dynamic)
        {
            throw new ArgumentOutOfRangeException(parameterName, body.BodyType, "Unknown physics body type.");
        }

        if (!IsFinite(body.Position) || !IsFinite(body.Velocity))
        {
            throw new InvalidOperationException("Physics body position and velocity must be finite.");
        }

        if (!IsFinite(body.Size) || body.Size.X <= 0f || body.Size.Y <= 0f)
        {
            throw new InvalidOperationException("Physics body size must be positive and finite.");
        }

        if (body.BodyType == PhysicsBodyType.Dynamic && (!float.IsFinite(body.Mass) || body.Mass <= 0f))
        {
            throw new InvalidOperationException("Dynamic physics bodies require a positive finite mass.");
        }

        if (!float.IsFinite(body.GravityScale) ||
            !float.IsFinite(body.LinearDamping) ||
            body.LinearDamping < 0f ||
            !IsFinite(body.AccumulatedForce))
        {
            throw new InvalidOperationException("Physics body integration parameters must be finite and damping must not be negative.");
        }

        if (!IsValidVelocityLimit(body.MaximumAbsoluteVelocity.X) ||
            !IsValidVelocityLimit(body.MaximumAbsoluteVelocity.Y))
        {
            throw new InvalidOperationException("Physics body velocity limits must be positive finite values or positive infinity.");
        }
    }

    private static bool IsValidVelocityLimit(float value)
    {
        return value > 0f && (float.IsFinite(value) || float.IsPositiveInfinity(value));
    }

    private static bool IsFinite(Vector2 value)
    {
        return float.IsFinite(value.X) && float.IsFinite(value.Y);
    }

    private static PhysicsMoveResult CreateStationaryResult(PhysicsBody body)
    {
        return new PhysicsMoveResult(
            body.Position,
            Vector2.Zero,
            Vector2.Zero,
            body.Velocity,
            PhysicsContactFlags.None,
            0,
            0,
            0,
            0);
    }
}
