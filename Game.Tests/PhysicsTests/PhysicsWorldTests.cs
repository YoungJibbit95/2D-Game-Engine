using System.Diagnostics;
using System.Numerics;
using Game.Core;
using Game.Core.Physics;
using Game.Core.World;
using Game.Tests.PerformanceTests;
using Xunit;
using Xunit.Abstractions;

namespace Game.Tests.PhysicsTests;

[Collection(LongSessionPerformanceCollection.Name)]
public sealed class PhysicsWorldTests
{
    private readonly ITestOutputHelper _output;

    public PhysicsWorldTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Step_IntegratesDynamicForcesButMovesKinematicBodiesDirectly()
    {
        var world = CreateWorld(64, 32);
        var dynamicBody = new PhysicsBody
        {
            Position = new Vector2(32, 32),
            Size = new Vector2(8, 8),
            Mass = 2f,
            GravityScale = 0f,
            CollidesWithTiles = false
        };
        dynamicBody.AddForce(new Vector2(20, 0));
        var kinematicBody = new PhysicsBody
        {
            Position = new Vector2(32, 64),
            Size = new Vector2(8, 8),
            Velocity = new Vector2(5, 0),
            BodyType = PhysicsBodyType.Kinematic,
            CollidesWithTiles = false
        };
        PhysicsBody[] bodies = [dynamicBody, kinematicBody];
        var results = new PhysicsMoveResult[bodies.Length];
        var physics = new PhysicsWorld(settings: new PhysicsStepSettings(
            Vector2.Zero,
            1f,
            1_000f,
            16,
            0));

        var telemetry = physics.Step(world, bodies, 0.5f, results, Span<PhysicsContact>.Empty);

        Assert.Equal(new Vector2(34.5f, 32), dynamicBody.Position);
        Assert.Equal(new Vector2(34.5f, 64), kinematicBody.Position);
        Assert.Equal(Vector2.Zero, dynamicBody.AccumulatedForce);
        Assert.Equal(1, telemetry.DynamicBodies);
        Assert.Equal(1, telemetry.KinematicBodies);
        Assert.Equal(2, telemetry.BodiesSimulated);
    }

    [Fact]
    public void Step_RejectsBodyOverflowInsteadOfSilentlySlowingSimulationTime()
    {
        var world = CreateWorld(64, 32);
        world.SetTile(4, 10, KnownTileIds.Dirt);
        var bodies = new[]
        {
            CreateFallingBody(4),
            CreateFallingBody(4),
            CreateFallingBody(4)
        };
        var results = new PhysicsMoveResult[3];
        var contacts = new PhysicsContact[1];
        var physics = new PhysicsWorld(settings: new PhysicsStepSettings(
            Vector2.Zero,
            1f,
            1_000f,
            2,
            1));

        var error = Assert.Throws<InvalidOperationException>(() =>
            physics.Step(world, bodies, 1f, results, contacts));

        Assert.Contains("never silently deferred", error.Message, StringComparison.Ordinal);
        Assert.All(bodies, body => Assert.False(body.OnGround));
    }

    [Fact]
    public void Step_RejectsUndersizedResultStorage()
    {
        var world = CreateWorld(64, 32);
        PhysicsBody[] bodies = [CreateFallingBody(1), CreateFallingBody(2)];
        var physics = new PhysicsWorld();

        Assert.Throws<ArgumentException>(() =>
            physics.Step(world, bodies, 1f / 60f, new PhysicsMoveResult[1], Span<PhysicsContact>.Empty));
    }

    [Fact]
    public void Step_ValidatesCompleteBatchBeforeMutatingAnyBody()
    {
        var world = CreateWorld(64, 32);
        var validBody = new PhysicsBody
        {
            Position = new Vector2(8, 12),
            Size = new Vector2(8, 8),
            Velocity = new Vector2(20, 0),
            GravityScale = 0f,
            CollidesWithTiles = false
        };
        validBody.AddForce(new Vector2(10, 0));
        var invalidBody = new PhysicsBody
        {
            Position = new Vector2(24, 12),
            Size = new Vector2(8, 8),
            Mass = 0f,
            CollidesWithTiles = false
        };
        PhysicsBody[] bodies = [validBody, invalidBody];
        var physics = new PhysicsWorld(settings: new PhysicsStepSettings(
            Vector2.Zero,
            1f,
            1_000f,
            bodies.Length,
            0));

        Assert.Throws<InvalidOperationException>(() =>
            physics.Step(
                world,
                bodies,
                0.5f,
                new PhysicsMoveResult[bodies.Length],
                Span<PhysicsContact>.Empty));

        Assert.Equal(new Vector2(8, 12), validBody.Position);
        Assert.Equal(new Vector2(20, 0), validBody.Velocity);
        Assert.Equal(new Vector2(10, 0), validBody.AccumulatedForce);
    }

    [Fact]
    public void StepBody_UsesPerAxisVelocityLimitsWithoutTemporaryBatchStorage()
    {
        var world = CreateWorld(64, 32);
        var body = new PhysicsBody
        {
            Position = new Vector2(8, 12),
            Size = new Vector2(8, 8),
            Velocity = new Vector2(100, 0),
            GravityScale = 1f,
            MaximumAbsoluteVelocity = new Vector2(30, 40),
            CollidesWithTiles = false
        };
        var physics = new PhysicsWorld(settings: new PhysicsStepSettings(
            new Vector2(0, 100),
            1f,
            1_000f,
            1,
            0));

        var result = physics.StepBody(
            world,
            body,
            0.5f,
            Span<PhysicsContact>.Empty);

        Assert.Equal(new Vector2(30, 40), body.Velocity);
        Assert.Equal(new Vector2(23, 32), body.Position);
        Assert.Equal(new Vector2(15, 20), result.ActualDisplacement);
    }

    [Fact]
    public void StepBody_SteadyStateRemainsAllocationFree()
    {
        const int measuredSteps = 10_000;
        var world = CreateWorld(64, 32);
        var body = new PhysicsBody
        {
            Position = new Vector2(8, 12),
            Size = new Vector2(8, 8),
            Velocity = new Vector2(1, 0),
            GravityScale = 0f,
            CollidesWithTiles = false
        };
        var physics = new PhysicsWorld(settings: new PhysicsStepSettings(
            Vector2.Zero,
            1f,
            1_000f,
            1,
            0));
        for (var warmup = 0; warmup < 16; warmup++)
        {
            physics.StepBody(world, body, 1f / 60f, Span<PhysicsContact>.Empty);
        }

        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        for (var step = 0; step < measuredSteps; step++)
        {
            physics.StepBody(world, body, 1f / 60f, Span<PhysicsContact>.Empty);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - allocatedBefore);
    }

    [Fact]
    public void StepWithBodyCollisions_SeparatesBodiesAndAppliesMaterialImpulse()
    {
        var world = CreateWorld(64, 32);
        var left = new PhysicsBody
        {
            Position = new Vector2(10, 20),
            Size = new Vector2(10, 10),
            Velocity = new Vector2(10, 0),
            GravityScale = 0f,
            CollidesWithTiles = false,
            Material = new PhysicsMaterial(0f, 1f),
            DeterministicOrder = 10
        };
        var right = new PhysicsBody
        {
            Position = new Vector2(18, 20),
            Size = new Vector2(10, 10),
            Velocity = new Vector2(-10, 0),
            GravityScale = 0f,
            CollidesWithTiles = false,
            Material = new PhysicsMaterial(0f, 1f),
            DeterministicOrder = 20
        };
        PhysicsBody[] bodies = [left, right];
        var results = new PhysicsMoveResult[2];
        var sortedIndices = new int[2];
        var pairs = new PhysicsBodyPair[1];
        var bodyContacts = new PhysicsBodyContact[1];
        var physics = CreateBodyCollisionWorld(2, maximumPairTests: 8);

        var telemetry = physics.StepWithBodyCollisions(
            world,
            bodies,
            0f,
            results,
            Span<PhysicsContact>.Empty,
            sortedIndices,
            pairs,
            bodyContacts);

        Assert.Equal(-10f, left.Velocity.X, precision: 4);
        Assert.Equal(10f, right.Velocity.X, precision: 4);
        Assert.True(left.Position.X < 10f);
        Assert.True(right.Position.X > 18f);
        Assert.Equal(1, telemetry.BodyPairsFound);
        Assert.Equal(1, telemetry.BodyPairsResolved);
        Assert.Equal(1, telemetry.BodyPairImpulses);
        Assert.Equal(1, telemetry.BodyPairPositionCorrections);
        Assert.Equal(Vector2.UnitX, bodyContacts[0].Normal);
        Assert.Equal(2f, bodyContacts[0].Penetration);
        Assert.Equal(20f, bodyContacts[0].NormalImpulse);
    }

    [Fact]
    public void StepWithBodyCollisions_UsesMutualMasksAndTreatsKinematicBodiesAsInfiniteMass()
    {
        var world = CreateWorld(64, 32);
        var dynamicBody = new PhysicsBody
        {
            Position = new Vector2(10, 20),
            Size = new Vector2(10, 10),
            Velocity = new Vector2(10, 0),
            GravityScale = 0f,
            CollidesWithTiles = false,
            CollisionLayer = PhysicsCollisionLayer.Enemy,
            CollisionMask = PhysicsCollisionLayer.Default
        };
        var kinematicBody = new PhysicsBody
        {
            Position = new Vector2(18, 20),
            Size = new Vector2(10, 10),
            BodyType = PhysicsBodyType.Kinematic,
            GravityScale = 0f,
            CollidesWithTiles = false,
            CollisionLayer = PhysicsCollisionLayer.Default,
            CollisionMask = PhysicsCollisionLayer.Enemy
        };
        PhysicsBody[] bodies = [dynamicBody, kinematicBody];
        var results = new PhysicsMoveResult[2];
        var sortedIndices = new int[2];
        var pairs = new PhysicsBodyPair[1];
        var bodyContacts = new PhysicsBodyContact[1];
        var physics = CreateBodyCollisionWorld(2, maximumPairTests: 8);

        var telemetry = physics.StepWithBodyCollisions(
            world,
            bodies,
            0f,
            results,
            Span<PhysicsContact>.Empty,
            sortedIndices,
            pairs,
            bodyContacts);

        Assert.Equal(0f, dynamicBody.Velocity.X);
        Assert.Equal(new Vector2(18, 20), kinematicBody.Position);
        Assert.Equal(1, telemetry.BodyPairsResolved);

        dynamicBody.Position = new Vector2(10, 20);
        dynamicBody.Velocity = new Vector2(10, 0);
        bodies[1] = new PhysicsBody
        {
            Position = new Vector2(18, 20),
            Size = new Vector2(10, 10),
            BodyType = PhysicsBodyType.Kinematic,
            GravityScale = 0f,
            CollidesWithTiles = false,
            CollisionLayer = PhysicsCollisionLayer.Default,
            CollisionMask = PhysicsCollisionLayer.Item
        };
        telemetry = physics.StepWithBodyCollisions(
            world,
            bodies,
            0f,
            results,
            Span<PhysicsContact>.Empty,
            sortedIndices,
            pairs,
            bodyContacts);

        Assert.Equal(new Vector2(10, 20), dynamicBody.Position);
        Assert.Equal(10f, dynamicBody.Velocity.X);
        Assert.Equal(0, telemetry.BodyPairsFound);
    }

    [Fact]
    public void StepWithBodyCollisions_IsIndependentOfCallerOrderWhenBodiesShareAOrigin()
    {
        var world = CreateWorld(64, 32);
        var firstLow = CreateOrderedOverlapBody(order: 10);
        var firstHigh = CreateOrderedOverlapBody(order: 20);
        var secondLow = CreateOrderedOverlapBody(order: 10);
        var secondHigh = CreateOrderedOverlapBody(order: 20);
        var physics = CreateBodyCollisionWorld(2, maximumPairTests: 8);

        StepBodyCollisionBatch(physics, world, [firstLow, firstHigh]);
        StepBodyCollisionBatch(physics, world, [secondHigh, secondLow]);

        Assert.Equal(firstLow.Position, secondLow.Position);
        Assert.Equal(firstHigh.Position, secondHigh.Position);
        Assert.Equal(firstLow.Velocity, secondLow.Velocity);
        Assert.Equal(firstHigh.Velocity, secondHigh.Velocity);
        Assert.True(firstLow.Position.X < firstHigh.Position.X);
    }

    [Fact]
    public void StepWithBodyCollisions_DoesNotPushPositionCorrectionIntoSolidTiles()
    {
        var world = CreateWorld(64, 32);
        world.SetTile(0, 1, KnownTileIds.Dirt);
        var wallAdjacent = new PhysicsBody
        {
            Position = new Vector2(16, 16),
            Size = new Vector2(10, 10),
            GravityScale = 0f,
            DeterministicOrder = 10
        };
        var openSide = new PhysicsBody
        {
            Position = new Vector2(22, 16),
            Size = new Vector2(10, 10),
            GravityScale = 0f,
            DeterministicOrder = 20
        };
        var physics = CreateBodyCollisionWorld(2, maximumPairTests: 8);

        var telemetry = StepBodyCollisionBatch(physics, world, [wallAdjacent, openSide]);

        Assert.Equal(16f, wallAdjacent.Position.X);
        Assert.True(openSide.Position.X > 22f);
        Assert.Equal(1, telemetry.BodyPairPositionCorrections);
        Assert.False(world.GetTile(0, 1).IsAir);
    }

    [Fact]
    public void StepWithBodyCollisions_RejectsIncompletePairStorageBeforeMutatingBodies()
    {
        var world = CreateWorld(64, 32);
        PhysicsBody[] bodies =
        [
            CreateOrderedOverlapBody(order: 10),
            CreateOrderedOverlapBody(order: 20),
            CreateOrderedOverlapBody(order: 30)
        ];
        var physics = CreateBodyCollisionWorld(3, maximumPairTests: 8);
        var initialPositions = bodies.Select(body => body.Position).ToArray();

        var error = Assert.Throws<InvalidOperationException>(() =>
            physics.StepWithBodyCollisions(
                world,
                bodies,
                1f / 60f,
                new PhysicsMoveResult[3],
                Span<PhysicsContact>.Empty,
                new int[3],
                new PhysicsBodyPair[1],
                new PhysicsBodyContact[1]));

        Assert.Contains("never deferred", error.Message, StringComparison.Ordinal);
        Assert.Equal(initialPositions, bodies.Select(body => body.Position));
        Assert.All(bodies, body => Assert.Equal(Vector2.Zero, body.Velocity));
    }

    [Fact]
    public void StepWithBodyCollisions_FiveHundredPackedBodiesRemainAllocationFreeAndWithinCpuGate()
    {
        const int bodyCount = 500;
        const int measuredSteps = 180;
        var world = CreateWorld(512, 32);
        var bodies = new PhysicsBody[bodyCount];
        for (var index = 0; index < bodyCount; index++)
        {
            bodies[index] = new PhysicsBody
            {
                Position = new Vector2(index * 7f, 64),
                Size = new Vector2(8, 8),
                GravityScale = 0f,
                CollidesWithTiles = false,
                CollisionLayer = PhysicsCollisionLayer.Enemy,
                DeterministicOrder = index + 1
            };
        }

        var results = new PhysicsMoveResult[bodyCount];
        var sortedIndices = new int[bodyCount];
        var pairs = new PhysicsBodyPair[bodyCount * 2];
        var bodyContacts = new PhysicsBodyContact[pairs.Length];
        var physics = CreateBodyCollisionWorld(bodyCount, maximumPairTests: bodyCount * 4);
        var initial = physics.StepWithBodyCollisions(
            world,
            bodies,
            0f,
            results,
            Span<PhysicsContact>.Empty,
            sortedIndices,
            pairs,
            bodyContacts);
        Assert.True(initial.BodyPairsResolved >= bodyCount - 1);

        for (var warmup = 0; warmup < 16; warmup++)
        {
            physics.StepWithBodyCollisions(
                world,
                bodies,
                0f,
                results,
                Span<PhysicsContact>.Empty,
                sortedIndices,
                pairs,
                bodyContacts);
        }

        var stopwatch = new Stopwatch();
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        stopwatch.Start();
        for (var sample = 0; sample < measuredSteps; sample++)
        {
            physics.StepWithBodyCollisions(
                world,
                bodies,
                0f,
                results,
                Span<PhysicsContact>.Empty,
                sortedIndices,
                pairs,
                bodyContacts);
        }

        stopwatch.Stop();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        var millisecondsPerStep = stopwatch.Elapsed.TotalMilliseconds / measuredSteps;
        _output.WriteLine(
            "500 packed body collisions: {0:F3} ms/step, {1} B across {2} measured steps.",
            millisecondsPerStep,
            allocated,
            measuredSteps);

        Assert.Equal(0, allocated);
        Assert.True(
            millisecondsPerStep <= 4.0,
            $"500-body collision step averaged {millisecondsPerStep:F3} ms; budget is 4.000 ms.");
    }

    [Fact]
    public void StepWithContinuousBodyCollisions_PreventsFastDynamicBodiesFromTunneling()
    {
        var world = CreateWorld(64, 32);
        var left = CreateFastBody(new Vector2(0, 20), new Vector2(1_000, 0), order: 10);
        var right = CreateFastBody(new Vector2(100, 20), new Vector2(-1_000, 0), order: 20);
        PhysicsBody[] bodies = [left, right];
        var contacts = new PhysicsBodyContact[1];
        var telemetry = StepContinuousBatch(
            CreateBodyCollisionWorld(2, 8),
            world,
            bodies,
            0.1f,
            contacts);

        Assert.Equal(-1_000f, left.Velocity.X, precision: 3);
        Assert.Equal(1_000f, right.Velocity.X, precision: 3);
        Assert.True(left.Position.X < right.Position.X);
        Assert.Equal(-10f, left.Position.X, precision: 3);
        Assert.Equal(110f, right.Position.X, precision: 3);
        Assert.Equal(1, telemetry.ContinuousBodyPairs);
        Assert.Equal(1, telemetry.BodyPairsResolved);
        Assert.Equal(0.045f, telemetry.EarliestBodyTimeOfImpactSeconds, precision: 5);
        Assert.Equal(0.045f, contacts[0].TimeOfImpactSeconds, precision: 5);
        Assert.Equal(Vector2.UnitX, contacts[0].Normal);
    }

    [Fact]
    public void StepWithContinuousBodyCollisions_RequeriesToiAcrossThreeBodyImpulseChain()
    {
        var world = CreateWorld(64, 32);
        var striker = CreateFastBody(new Vector2(0, 20), new Vector2(1_000, 0), order: 10);
        var middle = CreateFastBody(new Vector2(50, 20), Vector2.Zero, order: 20);
        var receiver = CreateFastBody(new Vector2(60, 20), Vector2.Zero, order: 30);
        PhysicsBody[] bodies = [striker, middle, receiver];
        var contacts = new PhysicsBodyContact[3];

        var telemetry = StepContinuousBatch(
            CreateBodyCollisionWorld(3, 16),
            world,
            bodies,
            0.1f,
            contacts);

        Assert.Equal(0f, striker.Velocity.X, precision: 3);
        Assert.Equal(0f, middle.Velocity.X, precision: 3);
        Assert.Equal(1_000f, receiver.Velocity.X, precision: 3);
        Assert.Equal(40f, striker.Position.X, precision: 3);
        Assert.Equal(50f, middle.Position.X, precision: 3);
        Assert.Equal(120f, receiver.Position.X, precision: 3);
        Assert.Equal(2, telemetry.ContinuousBodyPairs);
        Assert.Equal(2, telemetry.BodyPairsResolved);
        Assert.Equal(2, telemetry.ContinuousBodyToiPasses);
        Assert.False(telemetry.ContinuousBodyToiPassLimitReached);
        Assert.Equal(0.04f, contacts[0].TimeOfImpactSeconds, precision: 5);
        Assert.Equal(0.04f, contacts[1].TimeOfImpactSeconds, precision: 5);
    }

    [Fact]
    public void StepWithContinuousBodyCollisions_TouchingAtZeroUsesDirectedNormalOnlyWhenApproaching()
    {
        var world = CreateWorld(64, 32);
        var approaching = CreateFastBody(new Vector2(0, 20), new Vector2(100, 0), order: 10);
        var target = CreateFastBody(new Vector2(10, 20), Vector2.Zero, order: 20, kinematic: true);
        PhysicsBody[] bodies = [approaching, target];
        var contacts = new PhysicsBodyContact[1];

        var telemetry = StepContinuousBatch(
            CreateBodyCollisionWorld(2, 8),
            world,
            bodies,
            0.1f,
            contacts);

        Assert.Equal(1, telemetry.BodyPairsResolved);
        Assert.Equal(0f, contacts[0].TimeOfImpactSeconds);
        Assert.Equal(Vector2.UnitX, contacts[0].Normal);
        Assert.Equal(-100f, approaching.Velocity.X, precision: 3);

        var separating = CreateFastBody(new Vector2(0, 20), new Vector2(-100, 0), order: 10);
        target = CreateFastBody(new Vector2(10, 20), Vector2.Zero, order: 20, kinematic: true);
        bodies = [separating, target];
        telemetry = StepContinuousBatch(
            CreateBodyCollisionWorld(2, 8),
            world,
            bodies,
            0.1f,
            new PhysicsBodyContact[1]);

        Assert.Equal(0, telemetry.BodyPairsResolved);
        Assert.Equal(-10f, separating.Position.X, precision: 3);
    }

    [Fact]
    public void StepWithContinuousBodyCollisions_UsesMutualMasksAndKinematicInfiniteMass()
    {
        var world = CreateWorld(64, 32);
        var dynamicBody = CreateFastBody(
            new Vector2(0, 20),
            new Vector2(1_000, 0),
            order: 10,
            layer: PhysicsCollisionLayer.Enemy,
            mask: PhysicsCollisionLayer.Default);
        var kinematicBody = CreateFastBody(
            new Vector2(100, 20),
            Vector2.Zero,
            order: 20,
            kinematic: true,
            layer: PhysicsCollisionLayer.Default,
            mask: PhysicsCollisionLayer.Enemy);
        PhysicsBody[] bodies = [dynamicBody, kinematicBody];
        var telemetry = StepContinuousBatch(
            CreateBodyCollisionWorld(2, 8),
            world,
            bodies,
            0.1f,
            new PhysicsBodyContact[1]);

        Assert.Equal(-1_000f, dynamicBody.Velocity.X, precision: 3);
        Assert.Equal(Vector2.Zero, kinematicBody.Velocity);
        Assert.Equal(new Vector2(100, 20), kinematicBody.Position);
        Assert.Equal(1, telemetry.ContinuousBodyPairs);

        dynamicBody.Position = new Vector2(0, 20);
        dynamicBody.Velocity = new Vector2(1_000, 0);
        kinematicBody = CreateFastBody(
            new Vector2(100, 20),
            Vector2.Zero,
            order: 20,
            kinematic: true,
            layer: PhysicsCollisionLayer.Default,
            mask: PhysicsCollisionLayer.Item);
        bodies[1] = kinematicBody;
        telemetry = StepContinuousBatch(
            CreateBodyCollisionWorld(2, 8),
            world,
            bodies,
            0.1f,
            new PhysicsBodyContact[1]);

        Assert.Equal(0, telemetry.BodyPairsFound);
        Assert.Equal(100f, dynamicBody.Position.X, precision: 3);
    }

    [Fact]
    public void StepWithContinuousBodyCollisions_IsOrderIndependentAtLargeNegativeCoordinates()
    {
        const float origin = -35_440f;
        var world = new World(
            64,
            32,
            WorldMetadata.CreateDefault(seed: 1),
            isHorizontallyInfinite: true);
        var firstLow = CreateFastBody(new Vector2(origin, 20), new Vector2(1_000, 0), order: 10);
        var firstHigh = CreateFastBody(new Vector2(origin + 100, 20), new Vector2(-1_000, 0), order: 20);
        var secondLow = CreateFastBody(new Vector2(origin, 20), new Vector2(1_000, 0), order: 10);
        var secondHigh = CreateFastBody(new Vector2(origin + 100, 20), new Vector2(-1_000, 0), order: 20);
        var physics = CreateBodyCollisionWorld(2, 8);

        StepContinuousBatch(physics, world, [firstLow, firstHigh], 0.1f, new PhysicsBodyContact[1]);
        StepContinuousBatch(physics, world, [secondHigh, secondLow], 0.1f, new PhysicsBodyContact[1]);

        Assert.Equal(firstLow.Position, secondLow.Position);
        Assert.Equal(firstHigh.Position, secondHigh.Position);
        Assert.Equal(firstLow.Velocity, secondLow.Velocity);
        Assert.Equal(firstHigh.Velocity, secondHigh.Velocity);
        Assert.True(firstLow.Position.X < firstHigh.Position.X);
    }

    [Fact]
    public void StepWithContinuousBodyCollisions_RejectsIncompleteSweptPairCapacityBeforeMutation()
    {
        var world = CreateWorld(64, 32);
        PhysicsBody[] bodies =
        [
            CreateFastBody(new Vector2(0, 20), new Vector2(1_000, 0), order: 10),
            CreateFastBody(new Vector2(50, 20), Vector2.Zero, order: 20),
            CreateFastBody(new Vector2(100, 20), new Vector2(-1_000, 0), order: 30)
        ];
        var initialPositions = bodies.Select(body => body.Position).ToArray();
        var initialVelocities = bodies.Select(body => body.Velocity).ToArray();
        var physics = CreateBodyCollisionWorld(3, 16);

        var error = Assert.Throws<InvalidOperationException>(() =>
            physics.StepWithContinuousBodyCollisions(
                world,
                bodies,
                0.1f,
                new PhysicsMoveResult[3],
                Span<PhysicsContact>.Empty,
                new int[3],
                new PhysicsBodyPair[1],
                new PhysicsBodyContact[1],
                new PhysicsBodyContact[1],
                new PhysicsBodySweepState[3]));

        Assert.Contains("never deferred", error.Message, StringComparison.Ordinal);
        Assert.Equal(initialPositions, bodies.Select(body => body.Position));
        Assert.Equal(initialVelocities, bodies.Select(body => body.Velocity));
    }

    [Fact]
    public void StepWithContinuousBodyCollisions_TileImpactWinsBeforeBodyTimeOfImpact()
    {
        var world = CreateWorld(64, 32);
        world.SetTile(3, 1, KnownTileIds.Dirt);
        var fastBody = CreateFastBody(
            new Vector2(0, 20),
            new Vector2(1_000, 0),
            order: 10,
            collidesWithTiles: true,
            restitution: 0f);
        var target = CreateFastBody(new Vector2(100, 20), Vector2.Zero, order: 20, kinematic: true);
        PhysicsBody[] bodies = [fastBody, target];

        var telemetry = StepContinuousBatch(
            CreateBodyCollisionWorld(2, 8),
            world,
            bodies,
            0.1f,
            new PhysicsBodyContact[1]);

        Assert.Equal(38f, fastBody.Position.X, precision: 3);
        Assert.Equal(0f, fastBody.Velocity.X, precision: 3);
        Assert.Equal(0, telemetry.ContinuousBodyPairs);
        Assert.True((telemetry.ContactsFound) > 0);
    }

    [Fact]
    public void StepWithContinuousBodyCollisions_FiveHundredFastBodiesRemainAllocationFreeAndWithinCpuGate()
    {
        const int bodyCount = 500;
        const int measuredSteps = 120;
        const float deltaSeconds = 0.02f;
        var world = CreateWorld(2_048, 32);
        var bodies = new PhysicsBody[bodyCount];
        for (var index = 0; index < bodyCount; index += 2)
        {
            var origin = index / 2 * 64f;
            bodies[index] = CreateFastBody(new Vector2(origin, 64), new Vector2(500, 0), index + 1);
            bodies[index + 1] = CreateFastBody(new Vector2(origin + 20, 64), new Vector2(-500, 0), index + 2);
        }
        var results = new PhysicsMoveResult[bodyCount];
        var sortedIndices = new int[bodyCount];
        var pairs = new PhysicsBodyPair[bodyCount];
        var bodyContacts = new PhysicsBodyContact[pairs.Length];
        var toiContacts = new PhysicsBodyContact[pairs.Length];
        var sweepStates = new PhysicsBodySweepState[bodyCount];
        var physics = CreateBodyCollisionWorld(bodyCount, bodyCount * 8);

        for (var warmup = 0; warmup < 16; warmup++)
        {
            ResetFastBodyPairs(bodies);
            physics.StepWithContinuousBodyCollisions(
                world,
                bodies,
                deltaSeconds,
                results,
                Span<PhysicsContact>.Empty,
                sortedIndices,
                pairs,
                bodyContacts,
                toiContacts,
                sweepStates);
        }

        var stopwatch = Stopwatch.StartNew();
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        PhysicsStepTelemetry lastTelemetry = default;
        for (var sample = 0; sample < measuredSteps; sample++)
        {
            ResetFastBodyPairs(bodies);
            lastTelemetry = physics.StepWithContinuousBodyCollisions(
                world,
                bodies,
                deltaSeconds,
                results,
                Span<PhysicsContact>.Empty,
                sortedIndices,
                pairs,
                bodyContacts,
                toiContacts,
                sweepStates);
        }
        stopwatch.Stop();

        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        var millisecondsPerStep = stopwatch.Elapsed.TotalMilliseconds / measuredSteps;
        _output.WriteLine(
            "500 fast swept bodies: {0:F3} ms/step, {1} B across {2} measured steps.",
            millisecondsPerStep,
            allocated,
            measuredSteps);
        Assert.Equal(bodyCount / 2, lastTelemetry.ContinuousBodyPairs);
        Assert.Equal(0, allocated);
        Assert.True(
            millisecondsPerStep <= 4.0,
            $"500-body continuous step averaged {millisecondsPerStep:F3} ms; budget is 4.000 ms.");
    }

    [Fact]
    public void StepWithContinuousBodyCollisions_DenseEightThousandPairWorkspaceStaysAllocationFreeAndBounded()
    {
        const int bodyCount = 128;
        const int pairCount = bodyCount * (bodyCount - 1) / 2;
        var world = CreateWorld(64, 32);
        var bodies = new PhysicsBody[bodyCount];
        for (var index = 0; index < bodyCount; index++)
        {
            bodies[index] = CreateFastBody(new Vector2(20, 20), Vector2.Zero, index + 1);
        }

        var results = new PhysicsMoveResult[bodyCount];
        var sortedIndices = new int[bodyCount];
        var pairs = new PhysicsBodyPair[pairCount];
        var bodyContacts = new PhysicsBodyContact[pairCount];
        var toiContacts = new PhysicsBodyContact[pairCount];
        var sweepStates = new PhysicsBodySweepState[bodyCount];
        var physics = CreateBodyCollisionWorld(bodyCount, pairCount);
        for (var warmup = 0; warmup < 4; warmup++)
        {
            ResetDenseBodies(bodies);
            physics.StepWithContinuousBodyCollisions(
                world,
                bodies,
                0.01f,
                results,
                Span<PhysicsContact>.Empty,
                sortedIndices,
                pairs,
                bodyContacts,
                toiContacts,
                sweepStates);
        }

        ResetDenseBodies(bodies);
        var stopwatch = Stopwatch.StartNew();
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var telemetry = physics.StepWithContinuousBodyCollisions(
            world,
            bodies,
            0.01f,
            results,
            Span<PhysicsContact>.Empty,
            sortedIndices,
            pairs,
            bodyContacts,
            toiContacts,
            sweepStates);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        stopwatch.Stop();

        _output.WriteLine(
            "128 dense swept bodies ({0} pairs): {1:F3} ms, {2} B.",
            pairCount,
            stopwatch.Elapsed.TotalMilliseconds,
            allocated);
        Assert.Equal(pairCount, telemetry.BodyPairsFound);
        Assert.Equal(0, allocated);
        Assert.True(
            stopwatch.Elapsed.TotalMilliseconds <= 50.0,
            $"Dense {pairCount}-pair continuous step took {stopwatch.Elapsed.TotalMilliseconds:F3} ms; budget is 50.000 ms.");
    }

    [Fact]
    public void Step_OneThousandSettledBodiesStayAllocationFreeAndWithinCpuGate()
    {
        const int bodyCount = 1_000;
        const int measuredSteps = 120;
        var world = CreateWorld(1_024, 32);
        for (var tileX = 0; tileX < 1_024; tileX++)
        {
            world.SetTile(tileX, 10, KnownTileIds.Dirt);
        }

        var bodies = new PhysicsBody[bodyCount];
        for (var index = 0; index < bodies.Length; index++)
        {
            bodies[index] = new PhysicsBody
            {
                Position = new Vector2((index % 1_000) * GameConstants.TileSize, 10 * GameConstants.TileSize - 8),
                Size = new Vector2(8, 8),
                GravityScale = 1f,
                CollisionLayer = PhysicsCollisionLayer.Enemy
            };
        }

        var results = new PhysicsMoveResult[bodyCount];
        var contacts = new PhysicsContact[bodyCount];
        var sortedIndices = new int[bodyCount];
        var pairs = new PhysicsBodyPair[bodyCount];
        var bodyContacts = new PhysicsBodyContact[pairs.Length];
        var physics = new PhysicsWorld(settings: new PhysicsStepSettings(
            new Vector2(0, 1_500f),
            1f / 30f,
            4_096f,
            bodyCount,
            1));

        for (var warmup = 0; warmup < 16; warmup++)
        {
            physics.StepWithBodyCollisions(
                world,
                bodies,
                1f / 60f,
                results,
                contacts,
                sortedIndices,
                pairs,
                bodyContacts);
        }

        var stopwatch = new Stopwatch();
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        stopwatch.Start();
        for (var sample = 0; sample < measuredSteps; sample++)
        {
            physics.StepWithBodyCollisions(
                world,
                bodies,
                1f / 60f,
                results,
                contacts,
                sortedIndices,
                pairs,
                bodyContacts);
        }

        stopwatch.Stop();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        var millisecondsPerStep = stopwatch.Elapsed.TotalMilliseconds / measuredSteps;

        _output.WriteLine(
            "1,000 settled bodies: {0:F3} ms/step, {1} B across {2} measured steps.",
            millisecondsPerStep,
            allocated,
            measuredSteps);

        Assert.Equal(0, allocated);
        Assert.True(
            millisecondsPerStep <= 4.0,
            $"1,000-body physics step averaged {millisecondsPerStep:F3} ms; budget is 4.000 ms.");
        Assert.All(bodies, body => Assert.True(body.OnGround));
    }

    [Fact]
    public void QueryOverlaps_IsDeterministicAndAppliesMutualLayerMasks()
    {
        var bodies = new[]
        {
            CreateOverlapBody(new Vector2(10, 10), PhysicsCollisionLayer.Player, PhysicsCollisionLayer.Enemy),
            CreateOverlapBody(new Vector2(12, 12), PhysicsCollisionLayer.Enemy, PhysicsCollisionLayer.Player),
            CreateOverlapBody(new Vector2(11, 11), PhysicsCollisionLayer.Sensor, PhysicsCollisionLayer.All),
            CreateOverlapBody(new Vector2(100, 100), PhysicsCollisionLayer.Enemy, PhysicsCollisionLayer.All)
        };
        Span<int> sortedIndices = stackalloc int[bodies.Length];
        Span<PhysicsBodyPair> pairs = stackalloc PhysicsBodyPair[8];

        var telemetry = new PhysicsBroadphase().QueryOverlaps(bodies, sortedIndices, pairs);

        Assert.Equal(1, telemetry.PairsFound);
        Assert.Equal(new PhysicsBodyPair(0, 1), pairs[0]);
        Assert.Equal(new[] { 0, 2, 1, 3 }, sortedIndices.ToArray());
    }

    [Fact]
    public void QueryOverlaps_ReportsPairBudgetExhaustionWithoutGrowingStorage()
    {
        var bodies = new PhysicsBody[32];
        for (var index = 0; index < bodies.Length; index++)
        {
            bodies[index] = CreateOverlapBody(
                new Vector2(index * 0.01f, 10),
                PhysicsCollisionLayer.Default,
                PhysicsCollisionLayer.All);
        }

        var sortedIndices = new int[bodies.Length];
        Span<PhysicsBodyPair> pairs = stackalloc PhysicsBodyPair[4];
        var broadphase = new PhysicsBroadphase(new PhysicsBroadphaseSettings(32, 10, false));

        var telemetry = broadphase.QueryOverlaps(bodies, sortedIndices, pairs);

        Assert.True(telemetry.PairBudgetExhausted);
        Assert.Equal(10, telemetry.PairTests);
        Assert.Equal(10, telemetry.PairsFound);
        Assert.Equal(4, telemetry.PairsWritten);
    }

    [Fact]
    public void QueryOverlaps_OneThousandReverseOrderedBodiesUsesCallerOwnedStorageWithoutAllocations()
    {
        const int bodyCount = 1_000;
        const int measuredQueries = 240;
        var bodies = new PhysicsBody[bodyCount];
        for (var index = 0; index < bodies.Length; index++)
        {
            bodies[index] = CreateOverlapBody(
                new Vector2((bodyCount - index) * 12, index % 4),
                PhysicsCollisionLayer.Enemy,
                PhysicsCollisionLayer.All);
        }

        var sortedIndices = new int[bodyCount];
        var pairs = new PhysicsBodyPair[bodyCount];
        var broadphase = new PhysicsBroadphase(new PhysicsBroadphaseSettings(bodyCount, 8_192, false));
        for (var warmup = 0; warmup < 16; warmup++)
        {
            broadphase.QueryOverlaps(bodies, sortedIndices, pairs);
        }

        var stopwatch = new Stopwatch();
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        stopwatch.Start();
        for (var sample = 0; sample < measuredQueries; sample++)
        {
            broadphase.QueryOverlaps(bodies, sortedIndices, pairs);
        }

        stopwatch.Stop();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        var millisecondsPerQuery = stopwatch.Elapsed.TotalMilliseconds / measuredQueries;
        _output.WriteLine(
            "1,000 reverse-ordered bodies: {0:F3} ms/query, {1} B across {2} broadphase queries.",
            millisecondsPerQuery,
            allocated,
            measuredQueries);

        Assert.Equal(0, allocated);
        Assert.True(
            millisecondsPerQuery <= 1.0,
            $"1,000-body broadphase averaged {millisecondsPerQuery:F3} ms; budget is 1.000 ms.");
    }

    private static PhysicsBody CreateFallingBody(int tileX)
    {
        return new PhysicsBody
        {
            Position = new Vector2(tileX * GameConstants.TileSize, 9 * GameConstants.TileSize),
            Size = new Vector2(8, 8),
            Velocity = new Vector2(0, 100),
            GravityScale = 0f
        };
    }

    private static PhysicsWorld CreateBodyCollisionWorld(int maximumBodies, int maximumPairTests)
    {
        return new PhysicsWorld(
            settings: new PhysicsStepSettings(
                Vector2.Zero,
                1f,
                4_096f,
                maximumBodies,
                0),
            broadphaseSettings: new PhysicsBroadphaseSettings(
                maximumBodies,
                maximumPairTests,
                false));
    }

    private static PhysicsBody CreateOrderedOverlapBody(long order)
    {
        return new PhysicsBody
        {
            Position = new Vector2(20, 20),
            Size = new Vector2(10, 10),
            GravityScale = 0f,
            CollidesWithTiles = false,
            DeterministicOrder = order
        };
    }

    private static PhysicsStepTelemetry StepBodyCollisionBatch(
        PhysicsWorld physics,
        World world,
        PhysicsBody[] bodies)
    {
        return physics.StepWithBodyCollisions(
            world,
            bodies,
            0f,
            new PhysicsMoveResult[bodies.Length],
            Span<PhysicsContact>.Empty,
            new int[bodies.Length],
            new PhysicsBodyPair[1],
            new PhysicsBodyContact[1]);
    }

    private static PhysicsBody CreateOverlapBody(
        Vector2 position,
        PhysicsCollisionLayer layer,
        PhysicsCollisionLayer mask)
    {
        return new PhysicsBody
        {
            Position = position,
            Size = new Vector2(16, 16),
            CollisionLayer = layer,
            CollisionMask = mask,
            GravityScale = 0f
        };
    }

    private static PhysicsBody CreateFastBody(
        Vector2 position,
        Vector2 velocity,
        long order,
        bool kinematic = false,
        PhysicsCollisionLayer layer = PhysicsCollisionLayer.Default,
        PhysicsCollisionLayer mask = PhysicsCollisionLayer.All,
        bool collidesWithTiles = false,
        float restitution = 1f)
    {
        return new PhysicsBody
        {
            Position = position,
            Velocity = velocity,
            Size = new Vector2(10, 10),
            GravityScale = 0f,
            CollidesWithTiles = collidesWithTiles,
            BodyType = kinematic ? PhysicsBodyType.Kinematic : PhysicsBodyType.Dynamic,
            CollisionLayer = layer,
            CollisionMask = mask,
            Material = new PhysicsMaterial(0f, restitution),
            DeterministicOrder = order
        };
    }

    private static PhysicsStepTelemetry StepContinuousBatch(
        PhysicsWorld physics,
        World world,
        PhysicsBody[] bodies,
        float deltaSeconds,
        PhysicsBodyContact[] bodyContacts)
    {
        return physics.StepWithContinuousBodyCollisions(
            world,
            bodies,
            deltaSeconds,
            new PhysicsMoveResult[bodies.Length],
            Span<PhysicsContact>.Empty,
            new int[bodies.Length],
            new PhysicsBodyPair[bodyContacts.Length],
            bodyContacts,
            new PhysicsBodyContact[bodyContacts.Length],
            new PhysicsBodySweepState[bodies.Length]);
    }

    private static void ResetFastBodyPairs(PhysicsBody[] bodies)
    {
        for (var index = 0; index < bodies.Length; index += 2)
        {
            var origin = index / 2 * 64f;
            bodies[index].Position = new Vector2(origin, 64);
            bodies[index].Velocity = new Vector2(500, 0);
            bodies[index].OnGround = false;
            bodies[index + 1].Position = new Vector2(origin + 20, 64);
            bodies[index + 1].Velocity = new Vector2(-500, 0);
            bodies[index + 1].OnGround = false;
        }
    }

    private static void ResetDenseBodies(PhysicsBody[] bodies)
    {
        for (var index = 0; index < bodies.Length; index++)
        {
            bodies[index].Position = new Vector2(20, 20);
            bodies[index].Velocity = Vector2.Zero;
            bodies[index].OnGround = false;
        }
    }

    private static World CreateWorld(int width, int height)
    {
        return new World(width, height, WorldMetadata.CreateDefault(seed: 1));
    }
}
