using System.Diagnostics;
using System.Numerics;
using Game.Core;
using Game.Core.Combat;
using Game.Core.Entities;
using Game.Core.Entities.AI;
using Game.Core.Physics;
using Game.Core.Projectiles;
using Game.Core.World;
using Xunit;
using Xunit.Abstractions;

namespace Game.Tests.PerformanceTests;

[Collection(CombatQueryPerformanceCollection.Name)]
public sealed class DenseAuthoritativeScenePerformanceTests
{
    private const int ActorCount = 192;
    private const int ColliderProjectileCount = 48;
    private const int ProbeProjectileCount = 8;
    private const int BodyCount = ActorCount + ColliderProjectileCount + ProbeProjectileCount;
    private const int DecisionBudget = 32;
    private const int WarmupTicks = 256;
    private const int MeasurementTicks = 180;
    private const float FixedDeltaSeconds = 1f / 60f;
    private readonly ITestOutputHelper _output;

    public DenseAuthoritativeScenePerformanceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void DenseScene_ReplaysAiShapesContactsAndContinuousPairsDeterministically()
    {
        var first = CreateFixture();
        var second = CreateFixture();
        var firstRollingHash = FnvOffsetBasis;
        var secondRollingHash = FnvOffsetBasis;
        var sawTileContacts = false;
        var sawContinuousPairs = false;
        var sawDeferredDecisions = false;

        for (var tick = 0; tick < WarmupTicks + MeasurementTicks; tick++)
        {
            first.Manager.UpdateAll(first.World, FixedDeltaSeconds, first.Player, isNight: false, tick);
            second.Manager.UpdateAll(second.World, FixedDeltaSeconds, second.Player, isNight: false, tick);

            var firstHash = ComputeStateHash(first);
            var secondHash = ComputeStateHash(second);
            Assert.Equal(firstHash, secondHash);
            firstRollingHash = Mix(firstRollingHash, firstHash);
            secondRollingHash = Mix(secondRollingHash, secondHash);

            var telemetry = first.Manager.PhysicsTelemetryLastUpdate;
            sawTileContacts |= telemetry.ContactsWritten > 0;
            sawContinuousPairs |= telemetry.ContinuousBodyPairs > 0;
            sawDeferredDecisions |= first.Manager.AiSchedulingTelemetryLastUpdate.DecisionsDeferred > 0;
            Assert.Equal(BodyCount, telemetry.BodiesRequested);
            Assert.Equal(BodyCount, telemetry.BodiesSimulated);
            Assert.Equal(0, telemetry.BodiesDeferred);
            Assert.Equal(0, telemetry.TileBudgetExhaustions);
            Assert.InRange(telemetry.ContinuousBodyToiPasses, 0, 4);
            Assert.InRange(telemetry.ContinuousBodiesFrozen, 0, BodyCount);
        }

        Assert.Equal(firstRollingHash, secondRollingHash);
        Assert.True(sawTileContacts);
        Assert.True(sawContinuousPairs);
        Assert.True(sawDeferredDecisions);
        AssertProbeProjectilesRemainIsolated(first);
        AssertColliderProjectilesStayActive(first);
        AssertFiniteState(first);
    }

    [Fact]
    public void DenseScene_WarmAuthoritativeUpdateIsBoundedAndAllocationFree()
    {
        var fixture = CreateFixture();
        for (var tick = 0; tick < WarmupTicks; tick++)
        {
            fixture.Manager.UpdateAll(
                fixture.World,
                FixedDeltaSeconds,
                fixture.Player,
                isNight: false,
                tick);
        }

        var samples = new double[MeasurementTicks];
        var timerWarmup = Stopwatch.GetTimestamp();
        _ = Stopwatch.GetElapsedTime(timerWarmup).TotalMilliseconds;
        var contactsWritten = 0L;
        var bodyPairsResolved = 0L;
        var continuousPairs = 0L;
        var frozenBodies = 0L;
        var passLimitTicks = 0;
        var decisionsScheduled = 0L;
        var decisionsDeferred = 0L;
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var allocationCheckpoint = allocatedBefore;
        var allocationTickCount = 0;
        var firstAllocationTick = -1;
        var largestTickAllocation = 0L;
        for (var index = 0; index < samples.Length; index++)
        {
            var started = Stopwatch.GetTimestamp();
            fixture.Manager.UpdateAll(
                fixture.World,
                FixedDeltaSeconds,
                fixture.Player,
                isNight: false,
                WarmupTicks + index);
            samples[index] = Stopwatch.GetElapsedTime(started).TotalMilliseconds;

            var physics = fixture.Manager.PhysicsTelemetryLastUpdate;
            var ai = fixture.Manager.AiSchedulingTelemetryLastUpdate;
            contactsWritten += physics.ContactsWritten;
            bodyPairsResolved += physics.BodyPairsResolved;
            continuousPairs += physics.ContinuousBodyPairs;
            frozenBodies += physics.ContinuousBodiesFrozen;
            passLimitTicks += physics.ContinuousBodyToiPassLimitReached ? 1 : 0;
            decisionsScheduled += ai.DecisionsScheduled;
            decisionsDeferred += ai.DecisionsDeferred;
            var allocationAfterTick = GC.GetAllocatedBytesForCurrentThread();
            var tickAllocation = allocationAfterTick - allocationCheckpoint;
            if (tickAllocation > 0)
            {
                allocationTickCount++;
                firstAllocationTick = firstAllocationTick < 0 ? index : firstAllocationTick;
                largestTickAllocation = Math.Max(largestTickAllocation, tickAllocation);
            }
            allocationCheckpoint = allocationAfterTick;
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        Array.Sort(samples);
        var p95 = Percentile(samples, 0.95);
        var p99 = Percentile(samples, 0.99);
        var p95Runs = new double[7];
        var p99Runs = new double[7];
        p95Runs[0] = p95;
        p99Runs[0] = p99;
        for (var run = 1; run < p95Runs.Length; run++)
        {
            var confirmation = MeasureConfirmation();
            Assert.Equal(0L, confirmation.AllocatedBytes);
            p95Runs[run] = confirmation.P95;
            p99Runs[run] = confirmation.P99;
        }

        Array.Sort(p95Runs);
        Array.Sort(p99Runs);
        var gateP95 = p95Runs[p95Runs.Length / 2];
        var gateP99 = p99Runs[p99Runs.Length / 2];
        _output.WriteLine(
            "dense confirmation p95 min/median/max={0:F3}/{1:F3}/{2:F3} p99 min/median/max={3:F3}/{4:F3}/{5:F3} median={6:F3}/{7:F3}",
            p95Runs[0],
            gateP95,
            p95Runs[^1],
            p99Runs[0],
            gateP99,
            p99Runs[^1],
            gateP95,
            gateP99);
        var spatial = fixture.Manager.SpatialIndexTelemetry;
        _output.WriteLine(
            "dense authoritative scene: p95={0:F3} ms p99={1:F3} ms allocation={2} B " +
            "tileContacts={3} bodyPairs={4} continuousPairs={5} frozenBodies={6} " +
            "passLimitTicks={7} decisions={8} deferred={9} allocationTicks={10} " +
            "firstAllocationTick={11} maxTickAllocation={12} spatialActive={13}->{14}/peak{15} " +
            "spatialPrepared={16}->{17} hash=0x{18:X16}",
            p95,
            p99,
            allocated,
            contactsWritten,
            bodyPairsResolved,
            continuousPairs,
            frozenBodies,
            passLimitTicks,
            decisionsScheduled,
            decisionsDeferred,
            allocationTickCount,
            firstAllocationTick,
            largestTickAllocation,
            fixture.InitialSpatialTelemetry.ActiveBuckets,
            spatial.ActiveBuckets,
            spatial.PeakActiveBuckets,
            fixture.InitialSpatialTelemetry.PreparedBuckets,
            spatial.PreparedBuckets,
            ComputeStateHash(fixture));

        Assert.Equal(0, allocated);
        Assert.True(contactsWritten > 0);
        Assert.True(bodyPairsResolved > 0);
        Assert.True(continuousPairs > 0);
        Assert.True(decisionsScheduled > 0);
        Assert.True(decisionsDeferred > 0);
        Assert.True(passLimitTicks > 0);
        Assert.True(frozenBodies > 0);
        Assert.Equal(BodyCount, fixture.Manager.PhysicsTelemetryLastUpdate.BodiesSimulated);
        Assert.InRange(
            fixture.Manager.AiSchedulingTelemetryLastUpdate.DecisionsScheduled,
            1,
            DecisionBudget);
        Assert.Equal(0, fixture.Manager.PhysicsTelemetryLastUpdate.TileBudgetExhaustions);
        Assert.Equal(
            fixture.InitialSpatialTelemetry.PreparedBucketReserve,
            fixture.InitialSpatialTelemetry.PreparedBuckets);
        Assert.True(spatial.PreparedBuckets > 0);
#if DEBUG
        const double p95BudgetMilliseconds = 20;
        const double p99BudgetMilliseconds = 30;
#else
        // Keep the existing 60 Hz regression ceilings, but require the median
        // of seven independently warmed fixtures. A reproducible slowdown still
        // fails at the same thresholds while brief host scheduler pauses do not
        // outweigh the stable 0 B and deterministic workload invariants.
        const double p95BudgetMilliseconds = 5;
        const double p99BudgetMilliseconds = 8;
#endif
        Assert.True(
            gateP95 <= p95BudgetMilliseconds,
            $"dense scene median p95 {gateP95:F3} ms exceeded {p95BudgetMilliseconds:F3} ms");
        Assert.True(
            gateP99 <= p99BudgetMilliseconds,
            $"dense scene median p99 {gateP99:F3} ms exceeded {p99BudgetMilliseconds:F3} ms, " +
            $"run min/median/max={p99Runs[0]:F3}/{gateP99:F3}/{p99Runs[^1]:F3}");
        AssertProbeProjectilesRemainIsolated(fixture);
        AssertColliderProjectilesStayActive(fixture);
        AssertFiniteState(fixture);
    }

    private static (double P95, double P99, long AllocatedBytes) MeasureConfirmation()
    {
        var fixture = CreateFixture();
        for (var tick = 0; tick < WarmupTicks; tick++)
        {
            fixture.Manager.UpdateAll(
                fixture.World,
                FixedDeltaSeconds,
                fixture.Player,
                isNight: false,
                tick);
        }

        var samples = new double[MeasurementTicks];
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < samples.Length; index++)
        {
            var started = Stopwatch.GetTimestamp();
            fixture.Manager.UpdateAll(
                fixture.World,
                FixedDeltaSeconds,
                fixture.Player,
                isNight: false,
                WarmupTicks + index);
            samples[index] = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        Array.Sort(samples);
        return (Percentile(samples, 0.95), Percentile(samples, 0.99), allocated);
    }

    private static DenseSceneFixture CreateFixture()
    {
        const int worldWidth = 4_096;
        const int worldHeight = 48;
        const int floorTileY = 24;
        var world = new World(worldWidth, worldHeight, WorldMetadata.CreateDefault(seed: 73_931));
        BuildMixedShapeFloor(world, floorTileY);

        var collision = new TileCollisionResolver();
        var manager = new EntityManager(
            spatialCellSize: 32,
            physicsCollisionResolver: collision,
            maximumPhysicsBodies: BodyCount,
            maximumPhysicsBodyPairs: 8_192,
            aiSchedulingOptions: new EntityAiSchedulingOptions
            {
                DecisionBudgetPerTick = DecisionBudget,
                FullRatePopulationThreshold = 0,
                NearDistance = 64,
                MidDistance = 256,
                MidCadenceTicks = 2,
                FarCadenceTicks = 4,
                StarvationThresholdTicks = 8
            });
        var player = new PlayerEntity(new Vector2(16, 64), collision);
        var actors = new EnemyEntity[ActorCount];
        var behaviors = new DenseSceneAiBehavior[ActorCount];
        for (var index = 0; index < actors.Length; index++)
        {
            var behavior = new DenseSceneAiBehavior();
            var actor = new EnemyEntity(
                "dense-scene-actor",
                new Vector2(1_024 + index * 16, floorTileY * GameConstants.TileSize - 44),
                new Vector2(12, 10),
                new HealthComponent(20),
                behavior,
                collision,
                contactDamage: 0,
                movementMode: EntityMovementMode.Ground);
            manager.Add(actor);
            actors[index] = actor;
            behaviors[index] = behavior;
        }

        var colliderDefinition = new ProjectileDefinition
        {
            Id = "dense-scene-collider",
            TexturePath = "projectiles/dense-scene-collider",
            Speed = 1_400,
            Damage = 1,
            Lifetime = 60,
            CollisionRadius = 2,
            BounceRestitution = 1,
            TileCollisionBehavior = ProjectileTileCollisionBehavior.Ignore,
            EntityCollisionBehavior = ProjectileEntityCollisionBehavior.Ignore
        };
        var colliderProjectiles = new ProjectileEntity[ColliderProjectileCount];
        for (var index = 0; index < colliderProjectiles.Length; index++)
        {
            var direction = (index & 1) == 0 ? 1f : -1f;
            var projectile = new ProjectileEntity(
                colliderDefinition,
                new Vector2(12_000 + index * 20, 112 + (index % 3) * 6),
                new Vector2(direction * colliderDefinition.Speed, 0),
                ownerEntityId: index + 1);
            projectile.Body.CollisionMask = PhysicsCollisionLayer.Projectile | PhysicsCollisionLayer.Enemy;
            manager.Add(projectile);
            colliderProjectiles[index] = projectile;
        }

        var probeDefinition = colliderDefinition with
        {
            Id = "dense-scene-contact-slice-probe",
            Speed = 240
        };
        var probeProjectiles = new ProjectileEntity[ProbeProjectileCount];
        for (var index = 0; index < probeProjectiles.Length; index++)
        {
            var projectile = new ProjectileEntity(
                probeDefinition,
                new Vector2(20_000 + index * 256, 48),
                new Vector2(probeDefinition.Speed, 0),
                ownerEntityId: ColliderProjectileCount + index + 1);
            projectile.Body.CollisionMask = PhysicsCollisionLayer.World;
            manager.Add(projectile);
            probeProjectiles[index] = projectile;
        }

        return new DenseSceneFixture(
            world,
            manager,
            player,
            actors,
            behaviors,
            colliderProjectiles,
            probeProjectiles,
            manager.SpatialIndexTelemetry);
    }

    private static void BuildMixedShapeFloor(World world, int floorTileY)
    {
        for (var tileX = 0; tileX < world.WidthTiles; tileX++)
        {
            world.SetTile(tileX, floorTileY + 1, KnownTileIds.Dirt);
            var flags = (tileX % 5) switch
            {
                0 => TileFlags.None,
                1 => TileFlags.HalfBlock,
                2 => TileFlags.SlopeAscendingRight,
                3 => TileFlags.SlopeAscendingLeft,
                _ => TileFlags.Platform
            };
            world.SetTile(
                tileX,
                floorTileY,
                TileInstance.FromTileId(KnownTileIds.Dirt, flags));
        }
    }

    private static ulong ComputeStateHash(DenseSceneFixture fixture)
    {
        var hash = FnvOffsetBasis;
        var physics = fixture.Manager.PhysicsTelemetryLastUpdate;
        var ai = fixture.Manager.AiSchedulingTelemetryLastUpdate;
        hash = Mix(hash, physics.BodiesSimulated);
        hash = Mix(hash, physics.ContactsFound);
        hash = Mix(hash, physics.ContactsWritten);
        hash = Mix(hash, physics.BodyPairsResolved);
        hash = Mix(hash, physics.ContinuousBodyPairs);
        hash = Mix(hash, physics.ContinuousBodiesFrozen);
        hash = Mix(hash, ai.DecisionsScheduled);
        hash = Mix(hash, ai.DecisionsDeferred);
        hash = Mix(hash, ai.StarvationPromotions);

        for (var index = 0; index < fixture.Actors.Length; index++)
        {
            var actor = fixture.Actors[index];
            hash = Mix(hash, actor.Id);
            hash = Mix(hash, BitConverter.SingleToInt32Bits(actor.Body.Position.X));
            hash = Mix(hash, BitConverter.SingleToInt32Bits(actor.Body.Position.Y));
            hash = Mix(hash, BitConverter.SingleToInt32Bits(actor.Body.Velocity.X));
            hash = Mix(hash, BitConverter.SingleToInt32Bits(actor.Body.Velocity.Y));
            hash = Mix(hash, actor.Body.OnGround ? 1 : 0);
            hash = Mix(hash, fixture.Behaviors[index].UpdateCount);
        }

        for (var index = 0; index < fixture.ColliderProjectiles.Length; index++)
        {
            hash = MixProjectile(hash, fixture.ColliderProjectiles[index]);
        }

        for (var index = 0; index < fixture.ProbeProjectiles.Length; index++)
        {
            hash = MixProjectile(hash, fixture.ProbeProjectiles[index]);
        }

        return hash;
    }

    private static ulong MixProjectile(ulong hash, ProjectileEntity projectile)
    {
        hash = Mix(hash, projectile.Id);
        hash = Mix(hash, projectile.IsActive ? 1 : 0);
        hash = Mix(hash, BitConverter.SingleToInt32Bits(projectile.Position.X));
        hash = Mix(hash, BitConverter.SingleToInt32Bits(projectile.Position.Y));
        hash = Mix(hash, BitConverter.SingleToInt32Bits(projectile.Velocity.X));
        hash = Mix(hash, BitConverter.SingleToInt32Bits(projectile.Velocity.Y));
        hash = Mix(hash, BitConverter.SingleToInt32Bits(projectile.Age));
        hash = Mix(hash, (int)projectile.RuntimeState.TerminationReason);
        return hash;
    }

    private static void AssertProbeProjectilesRemainIsolated(DenseSceneFixture fixture)
    {
        for (var index = 0; index < fixture.ProbeProjectiles.Length; index++)
        {
            var projectile = fixture.ProbeProjectiles[index];
            Assert.True(projectile.IsActive);
            Assert.False(projectile.HasPendingTileCollisionResult);
            Assert.True(projectile.Position.X > 20_000 + index * 256);
        }
    }

    private static void AssertColliderProjectilesStayActive(DenseSceneFixture fixture)
    {
        for (var index = 0; index < fixture.ColliderProjectiles.Length; index++)
        {
            var projectile = fixture.ColliderProjectiles[index];
            Assert.True(projectile.IsActive);
            Assert.True(projectile.Velocity.LengthSquared() > 0);
        }
    }

    private static void AssertFiniteState(DenseSceneFixture fixture)
    {
        for (var index = 0; index < fixture.Actors.Length; index++)
        {
            AssertFinite(fixture.Actors[index].Body.Position);
            AssertFinite(fixture.Actors[index].Body.Velocity);
        }

        for (var index = 0; index < fixture.ColliderProjectiles.Length; index++)
        {
            AssertFinite(fixture.ColliderProjectiles[index].Position);
            AssertFinite(fixture.ColliderProjectiles[index].Velocity);
        }

        for (var index = 0; index < fixture.ProbeProjectiles.Length; index++)
        {
            AssertFinite(fixture.ProbeProjectiles[index].Position);
            AssertFinite(fixture.ProbeProjectiles[index].Velocity);
        }
    }

    private static void AssertFinite(Vector2 value)
    {
        Assert.True(float.IsFinite(value.X));
        Assert.True(float.IsFinite(value.Y));
    }

    private static double Percentile(double[] sortedSamples, double percentile)
    {
        var index = (int)Math.Ceiling(sortedSamples.Length * percentile) - 1;
        return sortedSamples[Math.Clamp(index, 0, sortedSamples.Length - 1)];
    }

    private const ulong FnvOffsetBasis = 14_695_981_039_346_656_037UL;
    private const ulong FnvPrime = 1_099_511_628_211UL;

    private static ulong Mix(ulong hash, long value)
    {
        return Mix(hash, unchecked((ulong)value));
    }

    private static ulong Mix(ulong hash, ulong value)
    {
        for (var byteIndex = 0; byteIndex < sizeof(ulong); byteIndex++)
        {
            hash ^= (byte)value;
            hash *= FnvPrime;
            value >>= 8;
        }

        return hash;
    }

    private sealed record DenseSceneFixture(
        World World,
        EntityManager Manager,
        PlayerEntity Player,
        EnemyEntity[] Actors,
        DenseSceneAiBehavior[] Behaviors,
        ProjectileEntity[] ColliderProjectiles,
        ProjectileEntity[] ProbeProjectiles,
        EntitySpatialIndexTelemetry InitialSpatialTelemetry);

    private sealed class DenseSceneAiBehavior : IAiBehavior
    {
        private uint _state = 0xA341_316Cu;

        public AiState CurrentState => AiState.Wander;

        public int? TargetEntityId => null;

        public long UpdateCount { get; private set; }

        public void Update(EnemyEntity entity, AiUpdateContext context, float deltaSeconds)
        {
            UpdateCount++;
            var state = _state ^ unchecked((uint)entity.Id) ^ unchecked((uint)context.TickNumber);
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            _state = state;

            var direction = ((entity.Id + context.TickNumber / 12) & 1) == 0 ? -1f : 1f;
            var speed = 80f + (state & 31);
            entity.Body.Velocity = new Vector2(direction * speed, entity.Body.Velocity.Y);
        }

        public bool TryConsumeAttackIntent(out AiAttackIntent intent)
        {
            intent = default;
            return false;
        }
    }
}
