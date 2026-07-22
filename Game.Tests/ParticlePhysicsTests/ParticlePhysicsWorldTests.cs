using System.Numerics;
using Game.Core.Particles;
using Xunit;

namespace Game.Tests.ParticlePhysicsTests;

public sealed class ParticlePhysicsWorldTests
{
    [Fact]
    public void SeededSpawnAndSimulation_AreDeterministic()
    {
        const int particleCount = 128;
        var first = new ParticlePhysicsWorld(particleCount);
        var second = new ParticlePhysicsWorld(particleCount);
        for (var index = 0; index < particleCount; index++)
        {
            var command = ParticleSpawnCommand.Create(
                new Vector2(index * 0.25f, 10f),
                new Vector2(1f, -2f),
                30f,
                seed: 8_912) with
            {
                PositionVariance = new Vector2(3f, 2f),
                VelocityVariance = new Vector2(4f, 5f),
                LifetimeVarianceSeconds = 2f,
                Sequence = (ulong)index,
                LinearDrag = 0.15f,
                Flags = ParticleSimulationFlags.None,
                UserData = index
            };

            Assert.True(first.TrySpawn(command, out var firstHandle));
            Assert.True(second.TrySpawn(command, out var secondHandle));
            Assert.Equal(firstHandle, secondHandle);
        }

        var budget = new ParticleStepBudget(particleCount, 0, 0);
        var forces = new ParticleForces(new Vector2(0, 18f), new Vector2(2f, 0));
        for (var step = 0; step < 240; step++)
        {
            var firstResult = first.Step(1f / 120f, forces, budget, null, Span<ParticlePhysicsEvent>.Empty);
            var secondResult = second.Step(1f / 120f, forces, budget, null, Span<ParticlePhysicsEvent>.Empty);
            Assert.Equal(firstResult, secondResult);
        }

        var firstSnapshots = new ParticleSnapshot[particleCount];
        var secondSnapshots = new ParticleSnapshot[particleCount];
        Assert.Equal(particleCount, first.CopyActiveParticles(firstSnapshots));
        Assert.Equal(particleCount, second.CopyActiveParticles(secondSnapshots));
        Assert.Equal(firstSnapshots, secondSnapshots);
    }

    [Fact]
    public void SweptTileCollision_ReflectsVelocityAndReportsContact()
    {
        var world = new ParticlePhysicsWorld(1);
        var command = ParticleSpawnCommand.Create(
            new Vector2(40f, 40f),
            new Vector2(12f, 100f),
            10f) with
        {
            Radius = 2f,
            GravityScale = 0f,
            Restitution = 1f,
            Friction = 0f,
            Flags = ParticleSimulationFlags.CollideWithTiles,
            UserData = 42
        };
        Assert.True(world.TrySpawn(command, out var handle));
        var collisionField = new FloorCollisionAdapter(floorTileY: 4);
        Span<ParticlePhysicsEvent> events = stackalloc ParticlePhysicsEvent[4];

        var result = world.Step(
            0.25f,
            ParticleForces.None,
            new ParticleStepBudget(1, 128, 4),
            collisionField,
            events);

        Assert.Equal(1, result.Collisions);
        Assert.False(result.CollisionBudgetExhausted);
        Assert.True(world.TryGetParticle(handle, out var particle));
        Assert.True(particle.Position.Y < 62f);
        Assert.InRange(particle.Velocity.Y, -100.01f, -99.99f);
        Assert.Equal(12f, particle.Velocity.X);
        Assert.Equal(ParticlePhysicsEventKind.Collision, events[0].Kind);
        Assert.Equal(new Vector2(0, -1), events[0].Normal);
        Assert.Equal(42, events[0].UserData);
        Assert.Equal(4, events[0].TileY);
    }

    [Fact]
    public void UpdateBudget_RotatesFairlyAcrossActiveSlots()
    {
        const int particleCount = 6;
        var world = new ParticlePhysicsWorld(particleCount);
        var handles = new ParticleHandle[particleCount];
        for (var index = 0; index < particleCount; index++)
        {
            var command = ParticleSpawnCommand.Create(
                Vector2.Zero,
                Vector2.UnitX,
                10f) with
            {
                GravityScale = 0f,
                Flags = ParticleSimulationFlags.None,
                UserData = index
            };
            Assert.True(world.TrySpawn(command, out handles[index]));
        }

        var budget = new ParticleStepBudget(2, 0, 0);
        for (var step = 0; step < 3; step++)
        {
            var result = world.Step(
                0.1f,
                ParticleForces.None,
                budget,
                null,
                Span<ParticlePhysicsEvent>.Empty);
            Assert.Equal(2, result.UpdatedParticles);
            Assert.True(result.UpdateBudgetExhausted);
        }

        foreach (var handle in handles)
        {
            Assert.True(world.TryGetParticle(handle, out var particle));
            Assert.InRange(particle.Position.X, 0.09999f, 0.10001f);
        }
    }

    [Fact]
    public void CollisionBudget_ClampsAdapterCallsAndStopsAtLastSafePosition()
    {
        var world = new ParticlePhysicsWorld(1);
        var command = ParticleSpawnCommand.Create(
            new Vector2(1f, 1f),
            new Vector2(1_000f, 1_000f),
            10f) with
        {
            Radius = 1f,
            GravityScale = 0f,
            Flags = ParticleSimulationFlags.CollideWithTiles
        };
        Assert.True(world.TrySpawn(command, out var handle));
        var adapter = new EmptyCountingCollisionAdapter();

        var result = world.Step(
            0.25f,
            ParticleForces.None,
            new ParticleStepBudget(1, 3, 4),
            adapter,
            Span<ParticlePhysicsEvent>.Empty);

        Assert.Equal(3, result.TileTests);
        Assert.Equal(3, adapter.QueryCount);
        Assert.True(result.CollisionBudgetExhausted);
        Assert.True(world.TryGetParticle(handle, out var particle));
        Assert.Equal(new Vector2(1f, 1f), particle.Position);
    }

    [Fact]
    public void InitialTileOverlap_DepentratesInOneBoundedCollision()
    {
        var world = new ParticlePhysicsWorld(1);
        var command = ParticleSpawnCommand.Create(
            new Vector2(8f, 66f),
            new Vector2(0, 1f),
            10f) with
        {
            Radius = 2f,
            GravityScale = 0f,
            Restitution = 0f,
            Friction = 0f,
            Flags = ParticleSimulationFlags.CollideWithTiles
        };
        Assert.True(world.TrySpawn(command, out var handle));

        var result = world.Step(
            1f / 60f,
            ParticleForces.None,
            new ParticleStepBudget(1, 32, 4),
            new FloorCollisionAdapter(4),
            Span<ParticlePhysicsEvent>.Empty);

        Assert.Equal(1, result.Collisions);
        Assert.True(world.TryGetParticle(handle, out var particle));
        Assert.True(particle.Position.Y < 62f);
    }

    [Fact]
    public void ExpirationDuringPartialStep_DoesNotHideUpdateBudgetExhaustion()
    {
        var world = new ParticlePhysicsWorld(4);
        for (var index = 0; index < 4; index++)
        {
            Assert.True(world.TrySpawn(
                ParticleSpawnCommand.Create(Vector2.Zero, Vector2.Zero, 0.01f) with
                {
                    Flags = ParticleSimulationFlags.None
                },
                out _));
        }

        var result = world.Step(
            0.02f,
            ParticleForces.None,
            new ParticleStepBudget(2, 0, 0),
            null,
            Span<ParticlePhysicsEvent>.Empty);

        Assert.Equal(2, result.UpdatedParticles);
        Assert.Equal(2, result.ExpiredParticles);
        Assert.True(result.UpdateBudgetExhausted);
        Assert.Equal(2, result.ActiveParticles);
    }

    [Fact]
    public void NonFiniteAndExtremeInputs_AreSanitizedWithoutCorruptingState()
    {
        var world = new ParticlePhysicsWorld(1);
        var command = new ParticleSpawnCommand
        {
            Position = new Vector2(float.NaN, float.PositiveInfinity),
            Velocity = new Vector2(float.NegativeInfinity, float.MaxValue),
            PositionVariance = new Vector2(float.NaN, float.PositiveInfinity),
            VelocityVariance = new Vector2(float.NegativeInfinity, float.MaxValue),
            LifetimeSeconds = float.NaN,
            LifetimeVarianceSeconds = float.PositiveInfinity,
            Radius = float.NegativeInfinity,
            GravityScale = float.PositiveInfinity,
            LinearDrag = float.NaN,
            Restitution = float.PositiveInfinity,
            Friction = float.NegativeInfinity,
            SleepSpeed = float.NaN,
            SleepDelaySeconds = float.PositiveInfinity,
            Seed = ulong.MaxValue,
            Sequence = ulong.MaxValue,
            Flags = (ParticleSimulationFlags)byte.MaxValue
        };
        Assert.True(world.TrySpawn(command, out var handle));
        Assert.True(world.TryGetParticle(handle, out var spawned));
        AssertFinite(spawned);
        Assert.InRange(spawned.LifetimeSeconds, 0.001f, 86_400f);
        Assert.InRange(spawned.Radius, 0.001f, 4_096f);

        var result = world.Step(
            float.PositiveInfinity,
            new ParticleForces(
                new Vector2(float.NaN, float.MaxValue),
                new Vector2(float.NegativeInfinity, float.PositiveInfinity)),
            new ParticleStepBudget(int.MaxValue, int.MinValue, int.MaxValue),
            new InvalidCollisionAdapter(),
            Span<ParticlePhysicsEvent>.Empty);

        Assert.True(result.InputWasSanitized);
        Assert.True(world.TryGetParticle(handle, out var updated));
        AssertFinite(updated);
    }

    [Fact]
    public void LifetimeSleepAndKillOnCollision_HaveExplicitLifecycleResults()
    {
        var expiringWorld = new ParticlePhysicsWorld(1);
        Assert.True(expiringWorld.TrySpawn(
            ParticleSpawnCommand.Create(Vector2.Zero, Vector2.Zero, 0.01f) with
            {
                Flags = ParticleSimulationFlags.None
            },
            out var expiringHandle));
        Span<ParticlePhysicsEvent> expirationEvents = stackalloc ParticlePhysicsEvent[1];
        var expiration = expiringWorld.Step(
            0.02f,
            ParticleForces.None,
            new ParticleStepBudget(1, 0, 0),
            null,
            expirationEvents);
        Assert.Equal(1, expiration.ExpiredParticles);
        Assert.Equal(ParticlePhysicsEventKind.Expired, expirationEvents[0].Kind);
        Assert.False(expiringWorld.TryGetParticle(expiringHandle, out _));

        var collisionWorld = new ParticlePhysicsWorld(2);
        var sleepingCommand = ParticleSpawnCommand.Create(
            new Vector2(8f, 60f),
            new Vector2(0, 20f),
            10f) with
        {
            Radius = 2f,
            GravityScale = 0f,
            Restitution = 0f,
            SleepSpeed = 1f,
            SleepDelaySeconds = 0f,
            Flags = ParticleSimulationFlags.CollideWithTiles | ParticleSimulationFlags.AllowSleep
        };
        var killingCommand = sleepingCommand with
        {
            Position = new Vector2(24f, 60f),
            Flags = ParticleSimulationFlags.CollideWithTiles | ParticleSimulationFlags.KillOnCollision
        };
        Assert.True(collisionWorld.TrySpawn(sleepingCommand, out var sleepingHandle));
        Assert.True(collisionWorld.TrySpawn(killingCommand, out var killingHandle));
        Span<ParticlePhysicsEvent> collisionEvents = stackalloc ParticlePhysicsEvent[8];

        var collisionResult = collisionWorld.Step(
            0.25f,
            ParticleForces.None,
            new ParticleStepBudget(2, 64, 4),
            new FloorCollisionAdapter(4),
            collisionEvents);

        Assert.Equal(1, collisionResult.SleepingParticles);
        Assert.Equal(1, collisionResult.KilledParticles);
        Assert.True(collisionWorld.TryGetParticle(sleepingHandle, out var sleeping));
        Assert.Equal(ParticleStateFlags.Sleeping, sleeping.StateFlags);
        Assert.False(collisionWorld.TryGetParticle(killingHandle, out _));
        Assert.Contains(
            collisionEvents[..collisionResult.EventsWritten].ToArray(),
            physicsEvent => physicsEvent.Kind == ParticlePhysicsEventKind.KilledOnCollision);
    }

    [Fact]
    public void HandlesRejectStaleGenerationAfterSlotReuse()
    {
        var world = new ParticlePhysicsWorld(1);
        Assert.True(world.TrySpawn(
            ParticleSpawnCommand.Create(Vector2.Zero, Vector2.Zero, 1f),
            out var first));
        Assert.True(world.TryKill(first));
        Assert.True(world.TrySpawn(
            ParticleSpawnCommand.Create(Vector2.One, Vector2.Zero, 1f),
            out var second));

        Assert.Equal(first.Slot, second.Slot);
        Assert.NotEqual(first.Generation, second.Generation);
        Assert.False(world.TryGetParticle(first, out _));
        Assert.True(world.TryGetParticle(second, out _));
    }

    private static void AssertFinite(ParticleSnapshot particle)
    {
        Assert.True(float.IsFinite(particle.Position.X));
        Assert.True(float.IsFinite(particle.Position.Y));
        Assert.True(float.IsFinite(particle.Velocity.X));
        Assert.True(float.IsFinite(particle.Velocity.Y));
        Assert.True(float.IsFinite(particle.AgeSeconds));
        Assert.True(float.IsFinite(particle.LifetimeSeconds));
        Assert.True(float.IsFinite(particle.Radius));
    }

    private sealed class FloorCollisionAdapter : IParticleTileCollisionAdapter
    {
        private readonly int _floorTileY;

        public FloorCollisionAdapter(int floorTileY)
        {
            _floorTileY = floorTileY;
        }

        public float TileSize => 16f;

        public bool TryGetCollider(int tileX, int tileY, out ParticleTileCollider collider)
        {
            _ = tileX;
            collider = ParticleTileCollider.Solid;
            return tileY >= _floorTileY;
        }
    }

    private sealed class EmptyCountingCollisionAdapter : IParticleTileCollisionAdapter
    {
        public float TileSize => 16f;

        public int QueryCount { get; private set; }

        public bool TryGetCollider(int tileX, int tileY, out ParticleTileCollider collider)
        {
            _ = tileX;
            _ = tileY;
            QueryCount++;
            collider = default;
            return false;
        }
    }

    private sealed class InvalidCollisionAdapter : IParticleTileCollisionAdapter
    {
        public float TileSize => float.NaN;

        public bool TryGetCollider(int tileX, int tileY, out ParticleTileCollider collider)
        {
            _ = tileX;
            _ = tileY;
            collider = default;
            return false;
        }
    }
}
