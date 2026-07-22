using System.Numerics;
using System.Runtime.InteropServices;
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

    public int ContinuousBodyPairs { get; init; }

    public float EarliestBodyTimeOfImpactSeconds { get; init; }

    public int ContinuousBodyToiPasses { get; init; }

    public bool ContinuousBodyToiPassLimitReached { get; init; }

    public int ContinuousBodiesFrozen { get; init; }
}

public sealed class PhysicsWorld
{
    private readonly TileCollisionResolver _collisionResolver;
    private readonly PhysicsBroadphase _broadphase;
    private readonly PhysicsStepSettings _settings;
    private readonly PhysicsContinuousCollisionSettings _continuousSettings;

    public PhysicsWorld(
        TileCollisionResolver? collisionResolver = null,
        PhysicsStepSettings? settings = null,
        PhysicsBroadphaseSettings? broadphaseSettings = null)
        : this(
            collisionResolver,
            settings,
            broadphaseSettings,
            PhysicsContinuousCollisionSettings.Default)
    {
    }

    public PhysicsWorld(
        TileCollisionResolver? collisionResolver,
        PhysicsStepSettings? settings,
        PhysicsBroadphaseSettings? broadphaseSettings,
        PhysicsContinuousCollisionSettings continuousSettings)
    {
        _collisionResolver = collisionResolver ?? new TileCollisionResolver();
        _broadphase = new PhysicsBroadphase(broadphaseSettings);
        _settings = (settings ?? PhysicsStepSettings.Default).Validate();
        _continuousSettings = continuousSettings.Validate();
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

    /// <summary>
    /// Runs a conservative swept-AABB step without hidden storage. The complete
    /// broadphase candidate set is frozen and capacity-checked before bodies mutate.
    /// TOIs for that set are rebuilt after every bounded solver pass, so impulses can
    /// propagate through candidate chains (including pairs touching at t=0). A pass
    /// never discovers a pair outside the initial swept-AABB closure; callers that
    /// need wider impulse propagation must use smaller authoritative substeps. If the
    /// configured pass limit is exhausted, bodies belonging to unresolved candidates
    /// are frozen for the unchecked remainder and reported through result/step telemetry.
    /// </summary>
    public PhysicsStepTelemetry StepWithContinuousBodyCollisions(
        GameWorld world,
        ReadOnlySpan<PhysicsBody> bodies,
        float deltaSeconds,
        Span<PhysicsMoveResult> results,
        Span<PhysicsContact> tileContacts,
        Span<int> sortedBodyIndices,
        Span<PhysicsBodyPair> bodyPairs,
        Span<PhysicsContinuousBodyContact> bodyContacts,
        Span<PhysicsContinuousContactCandidate> toiContacts,
        Span<PhysicsBodySweepState> sweepStates)
    {
        ValidateStep(world, bodies, deltaSeconds, results);
        if (sweepStates.Length < bodies.Length)
        {
            throw new ArgumentException(
                "Sweep-state storage must contain one slot for every submitted body.",
                nameof(sweepStates));
        }
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
        if (toiContacts.Length < bodyPairs.Length)
        {
            throw new ArgumentException(
                "TOI scratch storage must contain one slot for every body-pair slot.",
                nameof(toiContacts));
        }
        if (MemoryMarshal.AsBytes(bodyContacts).Overlaps(MemoryMarshal.AsBytes(toiContacts)))
        {
            throw new ArgumentException(
                "Body-contact output storage and TOI scratch storage must not overlap.",
                nameof(toiContacts));
        }

        var simulatedDelta = Math.Min(deltaSeconds, _settings.MaximumDeltaSeconds);
        for (var bodyIndex = 0; bodyIndex < bodies.Length; bodyIndex++)
        {
            var body = bodies[bodyIndex];
            sweepStates[bodyIndex] = new PhysicsBodySweepState
            {
                StartPosition = body.Position,
                PredictedVelocity = PredictVelocity(body, simulatedDelta)
            };
        }

        var broadphase = _broadphase.QuerySweptOverlaps(
            bodies,
            sweepStates,
            simulatedDelta,
            sortedBodyIndices,
            bodyPairs);
        if (broadphase.BodiesDeferred != 0 ||
            broadphase.PairBudgetExhausted ||
            broadphase.PairsFound != broadphase.PairsWritten)
        {
            throw new InvalidOperationException(
                $"Continuous body collision workspace is incomplete: indexed {broadphase.BodiesIndexed}/{broadphase.BodiesRequested} bodies, " +
                $"wrote {broadphase.PairsWritten}/{broadphase.PairsFound} swept pairs after {broadphase.PairTests} tests. " +
                "Increase the explicit broadphase/pair capacities; authoritative contacts are never deferred.");
        }

        for (var bodyIndex = 0; bodyIndex < bodies.Length; bodyIndex++)
        {
            if (bodies[bodyIndex].BodyType == PhysicsBodyType.Dynamic)
            {
                bodies[bodyIndex].Velocity = sweepStates[bodyIndex].PredictedVelocity;
            }
        }

        var contactsResolved = 0;
        var impulsesApplied = 0;
        var positionCorrections = 0;
        var continuousPairs = 0;
        var earliestTimeOfImpact = float.PositiveInfinity;
        var toiPasses = 0;
        var toiPassLimitReached = false;
        var candidatePairs = bodyPairs[..broadphase.PairsWritten];
        for (var passIndex = 0; passIndex < _continuousSettings.MaximumToiPasses; passIndex++)
        {
            var continuousContacts = BuildContinuousContacts(
                bodies,
                sweepStates,
                candidatePairs,
                simulatedDelta,
                toiContacts);
            if (continuousContacts == 0)
            {
                break;
            }

            toiPasses++;
            HeapSortContinuousContacts(toiContacts[..continuousContacts], bodies);
            var unresolvedContacts = false;
            var impulsesThisPass = 0;
            for (var contactIndex = 0; contactIndex < continuousContacts; contactIndex++)
            {
                var contact = toiContacts[contactIndex];
                if ((uint)contact.CandidatePairIndex >= (uint)candidatePairs.Length ||
                    candidatePairs[contact.CandidatePairIndex].BodyAIndex < 0)
                {
                    continue;
                }

                var contactData = contact.Contact;
                ref var stateA = ref sweepStates[contactData.BodyAIndex];
                ref var stateB = ref sweepStates[contactData.BodyBIndex];
                if (contact.BodyARevision != stateA.Revision ||
                    contact.BodyBRevision != stateB.Revision ||
                    contact.TimeOfImpactSeconds + 0.00001f < stateA.TimeAdvancedSeconds ||
                    contact.TimeOfImpactSeconds + 0.00001f < stateB.TimeAdvancedSeconds)
                {
                    unresolvedContacts = true;
                    continue;
                }

                var bodyA = bodies[contactData.BodyAIndex];
                var bodyB = bodies[contactData.BodyBIndex];
                var reachedA = AdvanceSweepBody(
                    world,
                    bodyA,
                    ref stateA,
                    contact.TimeOfImpactSeconds,
                    ResolveTileContactSpan(contactData.BodyAIndex, tileContacts));
                var reachedB = AdvanceSweepBody(
                    world,
                    bodyB,
                    ref stateB,
                    contact.TimeOfImpactSeconds,
                    ResolveTileContactSpan(contactData.BodyBIndex, tileContacts));
                if (!reachedA || !reachedB)
                {
                    unresolvedContacts = true;
                    continue;
                }

                if (contactData.Penetration > 0f)
                {
                    var pair = new PhysicsBodyPair(contactData.BodyAIndex, contactData.BodyBIndex);
                    if (!TryBuildBodyContact(bodyA, bodyB, pair, out var point, out var normal, out var penetration))
                    {
                        unresolvedContacts = true;
                        continue;
                    }
                    contactData = contactData with { Point = point, Normal = normal, Penetration = penetration };
                }
                else if (!AreAtContinuousContact(bodyA, bodyB, contactData.Normal))
                {
                    unresolvedContacts = true;
                    continue;
                }

                if (contactData.Penetration > 0f)
                {
                    var inverseMassSum = bodyA.InverseMass + bodyB.InverseMass;
                    const float penetrationSlop = 0.001f;
                    const float correctionPercent = 0.8f;
                    var correctionDistance = Math.Max(0f, contactData.Penetration - penetrationSlop) * correctionPercent;
                    if (inverseMassSum > 0f &&
                        correctionDistance > 0f &&
                        TryApplyPositionCorrection(
                            world,
                            bodyA,
                            bodyB,
                            contactData.Normal,
                            correctionDistance,
                            bodyA.InverseMass,
                            bodyB.InverseMass,
                            inverseMassSum))
                    {
                        positionCorrections++;
                        stateA.Revision++;
                        stateB.Revision++;
                    }
                }

                var velocityA = bodyA.Velocity;
                var velocityB = bodyB.Velocity;
                ApplyBodyContactImpulse(
                    bodyA,
                    bodyB,
                    contactData.Normal,
                    out var normalImpulse,
                    out var tangentImpulse);
                if (normalImpulse > 0f)
                {
                    impulsesApplied++;
                    impulsesThisPass++;
                }
                if (bodyA.Velocity != velocityA)
                {
                    stateA.Revision++;
                }
                if (bodyB.Velocity != velocityB)
                {
                    stateB.Revision++;
                }

                if (contact.TimeOfImpactSeconds > 0f)
                {
                    continuousPairs++;
                    earliestTimeOfImpact = Math.Min(earliestTimeOfImpact, contact.TimeOfImpactSeconds);
                }
                bodyContacts[contactsResolved++] = new PhysicsContinuousBodyContact(
                    contactData with
                    {
                        NormalImpulse = normalImpulse,
                        TangentImpulse = tangentImpulse
                    },
                    contact.TimeOfImpactSeconds);
                candidatePairs[contact.CandidatePairIndex] = new PhysicsBodyPair(-1, -1);
            }

            if (passIndex + 1 == _continuousSettings.MaximumToiPasses &&
                (unresolvedContacts || impulsesThisPass > 0 && HasActiveCandidatePairs(candidatePairs)))
            {
                toiPassLimitReached = true;
                MarkActiveCandidateBodiesBlocked(candidatePairs, sweepStates);
            }
        }

        var dynamicBodies = 0;
        var kinematicBodies = 0;
        var staticBodies = 0;
        var tileContactsFound = 0;
        var tileContactsWritten = 0;
        var tilesTested = 0;
        var tileBudgetExhaustions = 0;
        var continuousBodiesFrozen = 0;
        for (var bodyIndex = 0; bodyIndex < bodies.Length; bodyIndex++)
        {
            var body = bodies[bodyIndex];
            ref var state = ref sweepStates[bodyIndex];
            if (state.ContinuousMotionBlocked)
            {
                state.TimeAdvancedSeconds = simulatedDelta;
                state.ContactFlags |= PhysicsContactFlags.ContinuousMotionBlocked;
                continuousBodiesFrozen++;
            }
            else
            {
                AdvanceSweepBody(
                    world,
                    body,
                    ref state,
                    simulatedDelta,
                    ResolveTileContactSpan(bodyIndex, tileContacts));
            }
            CountBodyType(body, ref dynamicBodies, ref kinematicBodies, ref staticBodies);
            results[bodyIndex] = new PhysicsMoveResult(
                state.StartPosition,
                state.RequestedDisplacement,
                body.Position - state.StartPosition,
                body.Velocity,
                state.ContactFlags,
                state.ContactsFound,
                state.ContactsWritten,
                state.TilesTested,
                state.Substeps);
            tileContactsFound += state.ContactsFound;
            tileContactsWritten += state.ContactsWritten;
            tilesTested += state.TilesTested;
            if ((state.ContactFlags & PhysicsContactFlags.WorkBudgetExhausted) != 0)
            {
                tileBudgetExhaustions++;
            }
            body.ClearForces();
        }

        return new PhysicsStepTelemetry(
            bodies.Length,
            bodies.Length,
            0,
            dynamicBodies,
            kinematicBodies,
            staticBodies,
            tileContactsFound,
            tileContactsWritten,
            tilesTested,
            tileBudgetExhaustions,
            deltaSeconds,
            simulatedDelta)
        {
            BodyPairTests = broadphase.PairTests,
            BodyPairsFound = broadphase.PairsFound,
            BodyPairsResolved = contactsResolved,
            BodyPairImpulses = impulsesApplied,
            BodyPairPositionCorrections = positionCorrections,
            ContinuousBodyPairs = continuousPairs,
            EarliestBodyTimeOfImpactSeconds = float.IsPositiveInfinity(earliestTimeOfImpact)
                ? 0f
                : earliestTimeOfImpact,
            ContinuousBodyToiPasses = toiPasses,
            ContinuousBodyToiPassLimitReached = toiPassLimitReached,
            ContinuousBodiesFrozen = continuousBodiesFrozen
        };
    }

    private Span<PhysicsContact> ResolveTileContactSpan(
        int bodyIndex,
        Span<PhysicsContact> contacts)
    {
        var contactOffset = bodyIndex * _settings.ContactsPerBody;
        if (contactOffset >= contacts.Length)
        {
            return Span<PhysicsContact>.Empty;
        }

        return contacts.Slice(
            contactOffset,
            Math.Min(_settings.ContactsPerBody, contacts.Length - contactOffset));
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

    private int BuildContinuousContacts(
        ReadOnlySpan<PhysicsBody> bodies,
        ReadOnlySpan<PhysicsBodySweepState> sweepStates,
        ReadOnlySpan<PhysicsBodyPair> pairs,
        float deltaSeconds,
        Span<PhysicsContinuousContactCandidate> contacts)
    {
        var contactCount = 0;
        for (var pairIndex = 0; pairIndex < pairs.Length; pairIndex++)
        {
            var pair = pairs[pairIndex];
            if (pair.BodyAIndex < 0)
            {
                continue;
            }

            var bodyA = bodies[pair.BodyAIndex];
            var bodyB = bodies[pair.BodyBIndex];
            var stateA = sweepStates[pair.BodyAIndex];
            var stateB = sweepStates[pair.BodyBIndex];
            var baseTime = Math.Max(stateA.TimeAdvancedSeconds, stateB.TimeAdvancedSeconds);
            if (baseTime > deltaSeconds)
            {
                continue;
            }

            var velocityA = ResolveSweepVelocity(bodyA);
            var velocityB = ResolveSweepVelocity(bodyB);
            var positionA = ProjectPositionAtTime(bodyA, stateA, velocityA, baseTime);
            var positionB = ProjectPositionAtTime(bodyB, stateB, velocityB, baseTime);
            PhysicsContinuousContactCandidate contact;
            if (TryBuildBodyContactAtPositions(
                    bodyA,
                    positionA,
                    bodyB,
                    positionB,
                    pair,
                    out var point,
                    out var normal,
                    out var penetration))
            {
                contact = new PhysicsContinuousContactCandidate
                {
                    Contact = new PhysicsBodyContact(
                        pair.BodyAIndex,
                        pair.BodyBIndex,
                        point,
                        normal,
                        penetration,
                        0f,
                        0f),
                    TimeOfImpactSeconds = baseTime,
                    CandidatePairIndex = pairIndex,
                    BodyARevision = stateA.Revision,
                    BodyBRevision = stateB.Revision
                };
            }
            else if (!TryBuildSweptBodyContact(
                         bodyA,
                         positionA,
                         bodyB,
                         positionB,
                         pair,
                         velocityA,
                         velocityB,
                         deltaSeconds - baseTime,
                         baseTime,
                         pairIndex,
                         stateA.Revision,
                         stateB.Revision,
                         out contact))
            {
                continue;
            }

            contacts[contactCount++] = contact;
        }

        return contactCount;
    }

    private static Vector2 ProjectPositionAtTime(
        PhysicsBody body,
        PhysicsBodySweepState state,
        Vector2 velocity,
        float targetTimeSeconds)
    {
        var remainingTime = Math.Max(0f, targetTimeSeconds - state.TimeAdvancedSeconds);
        return body.Position + velocity * remainingTime;
    }

    private static Vector2 ResolveSweepVelocity(PhysicsBody body)
    {
        return body.BodyType == PhysicsBodyType.Static
            ? Vector2.Zero
            : body.Velocity;
    }

    private static bool TryBuildSweptBodyContact(
        PhysicsBody bodyA,
        Vector2 positionA,
        PhysicsBody bodyB,
        Vector2 positionB,
        PhysicsBodyPair pair,
        Vector2 velocityA,
        Vector2 velocityB,
        float deltaSeconds,
        float baseTimeSeconds,
        int candidatePairIndex,
        int bodyARevision,
        int bodyBRevision,
        out PhysicsContinuousContactCandidate contact)
    {
        var relativeVelocity = velocityA - velocityB;
        if (!TryCalculateAxisTimes(
                positionA.X,
                positionA.X + bodyA.Size.X,
                positionB.X,
                positionB.X + bodyB.Size.X,
                relativeVelocity.X,
                out var entryX,
                out var exitX) ||
            !TryCalculateAxisTimes(
                positionA.Y,
                positionA.Y + bodyA.Size.Y,
                positionB.Y,
                positionB.Y + bodyB.Size.Y,
                relativeVelocity.Y,
                out var entryY,
                out var exitY))
        {
            contact = default;
            return false;
        }

        var entryTime = Math.Max(entryX, entryY);
        var exitTime = Math.Min(exitX, exitY);
        if (entryTime < 0f || entryTime > deltaSeconds || entryTime > exitTime)
        {
            contact = default;
            return false;
        }

        var resolveHorizontally = entryX > entryY ||
                                  entryX == entryY &&
                                  (Math.Abs(relativeVelocity.X) > Math.Abs(relativeVelocity.Y) ||
                                   Math.Abs(relativeVelocity.X) == Math.Abs(relativeVelocity.Y) &&
                                   ResolvePairOrder(bodyA, bodyB, pair) <= 0);
        var normal = resolveHorizontally
            ? new Vector2(relativeVelocity.X > 0f ? 1f : -1f, 0f)
            : new Vector2(0f, relativeVelocity.Y > 0f ? 1f : -1f);
        var impactPositionA = positionA + velocityA * entryTime;
        var impactPositionB = positionB + velocityB * entryTime;
        var overlapLeft = Math.Max(impactPositionA.X, impactPositionB.X);
        var overlapTop = Math.Max(impactPositionA.Y, impactPositionB.Y);
        var overlapRight = Math.Min(impactPositionA.X + bodyA.Size.X, impactPositionB.X + bodyB.Size.X);
        var overlapBottom = Math.Min(impactPositionA.Y + bodyA.Size.Y, impactPositionB.Y + bodyB.Size.Y);
        var point = resolveHorizontally
            ? new Vector2(
                normal.X > 0f ? impactPositionA.X + bodyA.Size.X : impactPositionA.X,
                (overlapTop + overlapBottom) * 0.5f)
            : new Vector2(
                (overlapLeft + overlapRight) * 0.5f,
                normal.Y > 0f ? impactPositionA.Y + bodyA.Size.Y : impactPositionA.Y);
        contact = new PhysicsContinuousContactCandidate
        {
            Contact = new PhysicsBodyContact(
                pair.BodyAIndex,
                pair.BodyBIndex,
                point,
                normal,
                0f,
                0f,
                0f),
            TimeOfImpactSeconds = baseTimeSeconds + entryTime,
            CandidatePairIndex = candidatePairIndex,
            BodyARevision = bodyARevision,
            BodyBRevision = bodyBRevision
        };
        return true;
    }

    private static bool TryCalculateAxisTimes(
        float minimumA,
        float maximumA,
        float minimumB,
        float maximumB,
        float relativeVelocity,
        out float entryTime,
        out float exitTime)
    {
        if (relativeVelocity > 0f)
        {
            entryTime = (minimumB - maximumA) / relativeVelocity;
            exitTime = (maximumB - minimumA) / relativeVelocity;
            return true;
        }
        if (relativeVelocity < 0f)
        {
            entryTime = (maximumB - minimumA) / relativeVelocity;
            exitTime = (minimumB - maximumA) / relativeVelocity;
            return true;
        }

        entryTime = float.NegativeInfinity;
        exitTime = float.PositiveInfinity;
        return maximumA >= minimumB && maximumB >= minimumA;
    }

    private static bool ComesBeforeContinuousContact(
        PhysicsContinuousContactCandidate left,
        PhysicsContinuousContactCandidate right,
        ReadOnlySpan<PhysicsBody> bodies)
    {
        var comparison = left.TimeOfImpactSeconds.CompareTo(right.TimeOfImpactSeconds);
        if (comparison != 0)
        {
            return comparison < 0;
        }

        var leftA = bodies[left.Contact.BodyAIndex];
        var leftB = bodies[left.Contact.BodyBIndex];
        var rightA = bodies[right.Contact.BodyAIndex];
        var rightB = bodies[right.Contact.BodyBIndex];
        var leftMinimumOrder = Math.Min(leftA.DeterministicOrder, leftB.DeterministicOrder);
        var rightMinimumOrder = Math.Min(rightA.DeterministicOrder, rightB.DeterministicOrder);
        comparison = leftMinimumOrder.CompareTo(rightMinimumOrder);
        if (comparison != 0)
        {
            return comparison < 0;
        }
        var leftMaximumOrder = Math.Max(leftA.DeterministicOrder, leftB.DeterministicOrder);
        var rightMaximumOrder = Math.Max(rightA.DeterministicOrder, rightB.DeterministicOrder);
        comparison = leftMaximumOrder.CompareTo(rightMaximumOrder);
        if (comparison != 0)
        {
            return comparison < 0;
        }
        comparison = left.Contact.BodyAIndex.CompareTo(right.Contact.BodyAIndex);
        return comparison != 0
            ? comparison < 0
            : left.Contact.BodyBIndex < right.Contact.BodyBIndex;
    }

    private static void HeapSortContinuousContacts(
        Span<PhysicsContinuousContactCandidate> contacts,
        ReadOnlySpan<PhysicsBody> bodies)
    {
        for (var parent = contacts.Length / 2 - 1; parent >= 0; parent--)
        {
            SiftDownContinuousContacts(contacts, bodies, parent, contacts.Length);
        }
        for (var end = contacts.Length - 1; end > 0; end--)
        {
            (contacts[0], contacts[end]) = (contacts[end], contacts[0]);
            SiftDownContinuousContacts(contacts, bodies, 0, end);
        }
    }

    private static void SiftDownContinuousContacts(
        Span<PhysicsContinuousContactCandidate> contacts,
        ReadOnlySpan<PhysicsBody> bodies,
        int root,
        int count)
    {
        while (true)
        {
            var leftChild = root * 2 + 1;
            if (leftChild >= count)
            {
                return;
            }

            var laterChild = leftChild;
            var rightChild = leftChild + 1;
            if (rightChild < count &&
                ComesBeforeContinuousContact(contacts[leftChild], contacts[rightChild], bodies))
            {
                laterChild = rightChild;
            }
            if (!ComesBeforeContinuousContact(contacts[root], contacts[laterChild], bodies))
            {
                return;
            }

            (contacts[root], contacts[laterChild]) = (contacts[laterChild], contacts[root]);
            root = laterChild;
        }
    }

    private static bool HasActiveCandidatePairs(ReadOnlySpan<PhysicsBodyPair> pairs)
    {
        for (var pairIndex = 0; pairIndex < pairs.Length; pairIndex++)
        {
            if (pairs[pairIndex].BodyAIndex >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private static void MarkActiveCandidateBodiesBlocked(
        ReadOnlySpan<PhysicsBodyPair> pairs,
        Span<PhysicsBodySweepState> sweepStates)
    {
        for (var pairIndex = 0; pairIndex < pairs.Length; pairIndex++)
        {
            var pair = pairs[pairIndex];
            if (pair.BodyAIndex < 0)
            {
                continue;
            }

            sweepStates[pair.BodyAIndex].ContinuousMotionBlocked = true;
            sweepStates[pair.BodyBIndex].ContinuousMotionBlocked = true;
        }
    }

    private bool AdvanceSweepBody(
        GameWorld world,
        PhysicsBody body,
        ref PhysicsBodySweepState state,
        float targetTimeSeconds,
        Span<PhysicsContact> contacts)
    {
        var stepSeconds = targetTimeSeconds - state.TimeAdvancedSeconds;
        if (stepSeconds <= 0f)
        {
            state.TimeAdvancedSeconds = Math.Max(state.TimeAdvancedSeconds, targetTimeSeconds);
            return true;
        }
        if (body.BodyType == PhysicsBodyType.Static)
        {
            state.TimeAdvancedSeconds = targetTimeSeconds;
            return true;
        }

        var requestedDisplacement = body.Velocity * stepSeconds;
        var velocityBeforeMove = body.Velocity;
        var availableContacts = state.ContactsWritten < contacts.Length
            ? contacts[state.ContactsWritten..]
            : Span<PhysicsContact>.Empty;
        var result = _collisionResolver.MoveDetailed(
            world,
            body,
            stepSeconds,
            availableContacts);
        state.TimeAdvancedSeconds = targetTimeSeconds;
        state.RequestedDisplacement += requestedDisplacement;
        state.ContactFlags |= result.ContactFlags;
        state.ContactsFound += result.ContactsFound;
        state.ContactsWritten += result.ContactsWritten;
        state.TilesTested += result.TilesTested;
        state.Substeps += result.Substeps;
        if (body.Velocity != velocityBeforeMove)
        {
            state.Revision++;
        }
        var displacementError = result.ActualDisplacement - requestedDisplacement;
        return !result.Collided &&
               !result.WorkBudgetExhausted &&
               displacementError.LengthSquared() <= 0.0001f;
    }

    private static bool AreAtContinuousContact(
        PhysicsBody bodyA,
        PhysicsBody bodyB,
        Vector2 normal)
    {
        const float contactTolerance = 0.02f;
        if (normal.X != 0f)
        {
            var gap = normal.X > 0f
                ? bodyB.Position.X - (bodyA.Position.X + bodyA.Size.X)
                : bodyA.Position.X - (bodyB.Position.X + bodyB.Size.X);
            var tangentOverlap = Math.Min(
                bodyA.Position.Y + bodyA.Size.Y,
                bodyB.Position.Y + bodyB.Size.Y) - Math.Max(bodyA.Position.Y, bodyB.Position.Y);
            return Math.Abs(gap) <= contactTolerance && tangentOverlap >= -contactTolerance;
        }

        var verticalGap = normal.Y > 0f
            ? bodyB.Position.Y - (bodyA.Position.Y + bodyA.Size.Y)
            : bodyA.Position.Y - (bodyB.Position.Y + bodyB.Size.Y);
        var horizontalOverlap = Math.Min(
            bodyA.Position.X + bodyA.Size.X,
            bodyB.Position.X + bodyB.Size.X) - Math.Max(bodyA.Position.X, bodyB.Position.X);
        return Math.Abs(verticalGap) <= contactTolerance && horizontalOverlap >= -contactTolerance;
    }

    private static int ResolvePairOrder(
        PhysicsBody bodyA,
        PhysicsBody bodyB,
        PhysicsBodyPair pair)
    {
        var comparison = bodyA.DeterministicOrder.CompareTo(bodyB.DeterministicOrder);
        return comparison != 0
            ? comparison
            : pair.BodyAIndex.CompareTo(pair.BodyBIndex);
    }

    private static void ApplyBodyContactImpulse(
        PhysicsBody bodyA,
        PhysicsBody bodyB,
        Vector2 normal,
        out float normalImpulse,
        out float tangentImpulse)
    {
        normalImpulse = 0f;
        tangentImpulse = 0f;
        var inverseMassSum = bodyA.InverseMass + bodyB.InverseMass;
        if (inverseMassSum <= 0f)
        {
            return;
        }

        var relativeVelocity = bodyB.Velocity - bodyA.Velocity;
        var velocityAlongNormal = Vector2.Dot(relativeVelocity, normal);
        if (velocityAlongNormal >= 0f)
        {
            return;
        }

        var materialA = bodyA.Material.Normalized();
        var materialB = bodyB.Material.Normalized();
        var restitution = Math.Min(materialA.Restitution, materialB.Restitution);
        normalImpulse = -(1f + restitution) * velocityAlongNormal / inverseMassSum;
        var impulse = normal * normalImpulse;
        bodyA.Velocity -= impulse * bodyA.InverseMass;
        bodyB.Velocity += impulse * bodyB.InverseMass;
        relativeVelocity = bodyB.Velocity - bodyA.Velocity;
        var tangent = relativeVelocity - Vector2.Dot(relativeVelocity, normal) * normal;
        var tangentLengthSquared = tangent.LengthSquared();
        if (tangentLengthSquared <= 0.000001f)
        {
            return;
        }

        tangent /= MathF.Sqrt(tangentLengthSquared);
        var unclampedTangentImpulse = -Vector2.Dot(relativeVelocity, tangent) / inverseMassSum;
        var friction = MathF.Sqrt(materialA.Friction * materialB.Friction);
        tangentImpulse = Math.Clamp(
            unclampedTangentImpulse,
            -normalImpulse * friction,
            normalImpulse * friction);
        var frictionImpulse = tangent * tangentImpulse;
        bodyA.Velocity -= frictionImpulse * bodyA.InverseMass;
        bodyB.Velocity += frictionImpulse * bodyB.InverseMass;
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
        return TryBuildBodyContactAtPositions(
            bodyA,
            bodyA.Position,
            bodyB,
            bodyB.Position,
            pair,
            out point,
            out normal,
            out penetration);
    }

    private static bool TryBuildBodyContactAtPositions(
        PhysicsBody bodyA,
        Vector2 positionA,
        PhysicsBody bodyB,
        Vector2 positionB,
        PhysicsBodyPair pair,
        out Vector2 point,
        out Vector2 normal,
        out float penetration)
    {
        var overlapLeft = Math.Max(positionA.X, positionB.X);
        var overlapTop = Math.Max(positionA.Y, positionB.Y);
        var overlapRight = Math.Min(positionA.X + bodyA.Size.X, positionB.X + bodyB.Size.X);
        var overlapBottom = Math.Min(positionA.Y + bodyA.Size.Y, positionB.Y + bodyB.Size.Y);
        var penetrationX = overlapRight - overlapLeft;
        var penetrationY = overlapBottom - overlapTop;
        if (penetrationX <= 0f || penetrationY <= 0f)
        {
            point = default;
            normal = default;
            penetration = 0f;
            return false;
        }

        var centerDelta = positionB + bodyB.Size * 0.5f - (positionA + bodyA.Size * 0.5f);
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
        body.Velocity = PredictVelocity(body, deltaSeconds);
    }

    private Vector2 PredictVelocity(PhysicsBody body, float deltaSeconds)
    {
        if (body.BodyType == PhysicsBodyType.Static)
        {
            return Vector2.Zero;
        }
        if (body.BodyType == PhysicsBodyType.Kinematic)
        {
            return body.Velocity;
        }

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

        return velocity;
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
