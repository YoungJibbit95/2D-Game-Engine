using System.Diagnostics;
using System.Numerics;
using Game.Core.Combat;
using Game.Core.Entities;
using Game.Core.Loot;
using Game.Core.Physics;
using Game.Core.Projectiles;
using Game.Core.World;
using Xunit;
using Xunit.Abstractions;

namespace Game.Tests.PerformanceTests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class CombatQueryPerformanceCollection
{
    public const string Name = "Combat query performance";
}

[Collection(CombatQueryPerformanceCollection.Name)]
public sealed class CombatQueryPerformanceTests
{
    private const int ProjectileCount = 500;
    // Long enough for tiered JIT promotion to settle before exact allocation
    // accounting; short warmups were intermittently counting runtime work in a
    // full Debug suite even though isolated steady-state runs remained at 0 B.
    private const int WarmupIterations = 256;
    private const int MeasurementIterations = 180;
    private readonly ITestOutputHelper _output;

    public CombatQueryPerformanceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void FiveHundredProjectileQueries_ReuseCallerWorkspaceWithoutAllocation()
    {
        var entities = new EntityManager(spatialCellSize: 16);
        for (var index = 0; index < ProjectileCount; index++)
        {
            entities.Add(new ProjectileEntity(
                "query-probe",
                new Vector2(index * 32, (index & 3) * 32),
                Vector2.Zero,
                damage: 1,
                gravity: 0,
                pierce: 0,
                lifetime: 60));
        }

        var combat = new CombatSystem(new LootRoller(new Random(17)), new TileCollisionResolver());
        var loot = LootTableRegistry.Create(Array.Empty<LootTableDefinition>());
        var workspace = new CombatQueryWorkspace(initialCandidateCapacity: 8);

        for (var iteration = 0; iteration < WarmupIterations; iteration++)
        {
            _ = combat.ResolveProjectileHits(entities, loot, workspace: workspace);
        }

        workspace.Candidates.ResetTelemetry();
        var samples = new double[MeasurementIterations];
        // Stopwatch initializes runtime-specific timestamp helpers lazily on some
        // testhost/JIT combinations. Keep that one-time cost outside the 0 B
        // steady-state combat-query window.
        var timerWarmup = Stopwatch.GetTimestamp();
        _ = Stopwatch.GetElapsedTime(timerWarmup).TotalMilliseconds;
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        for (var iteration = 0; iteration < samples.Length; iteration++)
        {
            var started = Stopwatch.GetTimestamp();
            _ = combat.ResolveProjectileHits(entities, loot, workspace: workspace);
            samples[iteration] = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        Array.Sort(samples);
        var p95 = Percentile(samples, 0.95);
        var p99 = Percentile(samples, 0.99);
        var p99Runs = new double[7];
        p99Runs[0] = p99;
        for (var run = 1; run < p99Runs.Length; run++)
        {
            var confirmationSamples = new double[MeasurementIterations];
            var confirmationAllocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            for (var iteration = 0; iteration < confirmationSamples.Length; iteration++)
            {
                var started = Stopwatch.GetTimestamp();
                _ = combat.ResolveProjectileHits(entities, loot, workspace: workspace);
                confirmationSamples[iteration] = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            }
            var confirmationAllocated = GC.GetAllocatedBytesForCurrentThread() - confirmationAllocatedBefore;
            Assert.Equal(0L, confirmationAllocated);
            Array.Sort(confirmationSamples);
            p99Runs[run] = Percentile(confirmationSamples, 0.99);
        }
        Array.Sort(p99Runs);
        var medianP99 = p99Runs[p99Runs.Length / 2];
        var average = samples.Average();
        _output.WriteLine(
            "500-projectile spatial combat: avg {0:F3} ms, p95 {1:F3} ms, p99 {2:F3} ms, " +
            "{3} B across {4} resolutions, run min/median/max {5:F3}/{6:F3}/{7:F3} ms.",
            average,
            p95,
            p99,
            allocated,
            MeasurementIterations,
            p99Runs[0],
            medianP99,
            p99Runs[^1],
            medianP99);

        Assert.Equal(0, allocated);
        Assert.Equal((long)ProjectileCount * MeasurementIterations * p99Runs.Length, workspace.Candidates.QueryCount);
        Assert.InRange(workspace.Candidates.PeakResultCount, 1, 2);
        Assert.True(medianP99 <= 10, $"500-projectile combat query median p99 {medianP99:F3} ms exceeded 10 ms.");
    }

    [Fact]
    public void FiveHundredProjectilePhysicsSyncs_ReuseCallerContactStorageWithoutAllocation()
    {
        var world = new World(2_048, 32, WorldMetadata.CreateDefault(seed: 41));
        var entities = new EntityManager(spatialCellSize: 16);
        for (var index = 0; index < ProjectileCount; index++)
        {
            entities.Add(new ProjectileEntity(
                "contact-routing-probe",
                new Vector2(8 + index * 32, 8 + (index & 1) * 16),
                new Vector2(12, 0),
                damage: 1,
                gravity: 0,
                pierce: 0,
                lifetime: 60,
                ownerEntityId: index + 1));
        }

        const float fixedDeltaSeconds = 1f / 60f;
        for (var tick = 0; tick < WarmupIterations; tick++)
        {
            entities.UpdateAll(world, fixedDeltaSeconds);
        }

        var samples = new double[MeasurementIterations];
        var timerWarmup = Stopwatch.GetTimestamp();
        _ = Stopwatch.GetElapsedTime(timerWarmup).TotalMilliseconds;
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        for (var tick = 0; tick < samples.Length; tick++)
        {
            var started = Stopwatch.GetTimestamp();
            entities.UpdateAll(world, fixedDeltaSeconds);
            samples[tick] = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        Array.Sort(samples);
        var p99 = Percentile(samples, 0.99);
        var p99Runs = new double[7];
        p99Runs[0] = p99;
        for (var run = 1; run < p99Runs.Length; run++)
        {
            var confirmationSamples = new double[MeasurementIterations];
            var confirmationAllocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            for (var tick = 0; tick < confirmationSamples.Length; tick++)
            {
                var started = Stopwatch.GetTimestamp();
                entities.UpdateAll(world, fixedDeltaSeconds);
                confirmationSamples[tick] = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            }
            var confirmationAllocated = GC.GetAllocatedBytesForCurrentThread() - confirmationAllocatedBefore;
            Assert.Equal(0L, confirmationAllocated);
            Array.Sort(confirmationSamples);
            p99Runs[run] = Percentile(confirmationSamples, 0.99);
        }
        Array.Sort(p99Runs);
        var medianP99 = p99Runs[p99Runs.Length / 2];
        _output.WriteLine(
            "500-projectile contact routing: p99 {0:F3} ms, {1} B across {2} fixed ticks, " +
            "run min/median/max {3:F3}/{4:F3}/{5:F3} ms, median {6:F3} ms.",
            p99,
            allocated,
            MeasurementIterations,
            p99Runs[0],
            medianP99,
            p99Runs[^1],
            medianP99);

        Assert.Equal(0, allocated);
        Assert.Equal(ProjectileCount, entities.PhysicsTelemetryLastUpdate.BodiesSimulated);
        Assert.Equal(ProjectileCount, entities.Entities.Count);
        Assert.True(medianP99 <= 10, $"500-projectile contact routing median p99 {medianP99:F3} ms exceeded 10 ms.");
    }

    private static double Percentile(double[] sortedSamples, double percentile)
    {
        var index = (int)Math.Ceiling(sortedSamples.Length * percentile) - 1;
        return sortedSamples[Math.Clamp(index, 0, sortedSamples.Length - 1)];
    }
}
