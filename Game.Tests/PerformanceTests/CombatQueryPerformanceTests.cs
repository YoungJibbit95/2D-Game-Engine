using System.Diagnostics;
using System.Numerics;
using Game.Core.Combat;
using Game.Core.Entities;
using Game.Core.Loot;
using Game.Core.Physics;
using Game.Core.Projectiles;
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
    private const int WarmupIterations = 24;
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
        var average = samples.Average();
        _output.WriteLine(
            "500-projectile spatial combat: avg {0:F3} ms, p95 {1:F3} ms, p99 {2:F3} ms, {3} B across {4} resolutions.",
            average,
            p95,
            p99,
            allocated,
            MeasurementIterations);

        Assert.Equal(0, allocated);
        Assert.Equal((long)ProjectileCount * MeasurementIterations, workspace.Candidates.QueryCount);
        Assert.InRange(workspace.Candidates.PeakResultCount, 1, 2);
        Assert.True(p99 <= 10, $"500-projectile combat query p99 {p99:F3} ms exceeded 10 ms.");
    }

    private static double Percentile(double[] sortedSamples, double percentile)
    {
        var index = (int)Math.Ceiling(sortedSamples.Length * percentile) - 1;
        return sortedSamples[Math.Clamp(index, 0, sortedSamples.Length - 1)];
    }
}
