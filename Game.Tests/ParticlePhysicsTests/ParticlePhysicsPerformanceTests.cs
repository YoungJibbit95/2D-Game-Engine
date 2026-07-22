using System.Diagnostics;
using System.Numerics;
using Game.Core.Particles;
using Game.Tests.PerformanceTests;
using Xunit;
using Xunit.Abstractions;

namespace Game.Tests.ParticlePhysicsTests;

[Collection(LongSessionPerformanceCollection.Name)]
public sealed class ParticlePhysicsPerformanceTests
{
    private readonly ITestOutputHelper _output;

    public ParticlePhysicsPerformanceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void TenThousandParticles_StayWithinCpuBudgetAndAllocateZeroPerStep()
    {
        const int particleCount = 10_000;
        const int warmupSteps = 32;
        const int measuredSteps = 240;
        var world = new ParticlePhysicsWorld(particleCount);
        for (var index = 0; index < particleCount; index++)
        {
            var command = ParticleSpawnCommand.Create(
                new Vector2(index % 200, index / 200),
                new Vector2((index & 1) == 0 ? 2f : -2f, -1f),
                1_000f,
                seed: 44) with
            {
                Sequence = (ulong)index,
                VelocityVariance = new Vector2(0.2f, 0.1f),
                GravityScale = 1f,
                LinearDrag = 0.08f,
                Flags = ParticleSimulationFlags.None,
                UserData = index
            };
            Assert.True(world.TrySpawn(command, out _));
        }

        var budget = new ParticleStepBudget(particleCount, 0, 0);
        var forces = new ParticleForces(new Vector2(0, 9.81f), new Vector2(0.5f, 0));
        for (var step = 0; step < warmupSteps; step++)
        {
            world.Step(1f / 120f, forces, budget, null, Span<ParticlePhysicsEvent>.Empty);
        }

        var elapsedMilliseconds = new double[measuredSteps];
        var updatedParticles = 0;
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        for (var step = 0; step < measuredSteps; step++)
        {
            var startedAt = Stopwatch.GetTimestamp();
            var result = world.Step(
                1f / 120f,
                forces,
                budget,
                null,
                Span<ParticlePhysicsEvent>.Empty);
            elapsedMilliseconds[step] = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
            updatedParticles += result.UpdatedParticles;
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        Array.Sort(elapsedMilliseconds);
        var p50 = Percentile(elapsedMilliseconds, 0.50);
        var p95 = Percentile(elapsedMilliseconds, 0.95);
        var p99 = Percentile(elapsedMilliseconds, 0.99);
        var measurement =
            $"particle physics {particleCount:N0}: p50={p50:F4} ms, p95={p95:F4} ms, " +
            $"p99={p99:F4} ms, allocation={allocated / (double)measuredSteps:F1} B/step";
        _output.WriteLine(measurement);

        Assert.Equal(particleCount * measuredSteps, updatedParticles);
        Assert.Equal(0, allocated);
        Assert.True(p95 <= 2, measurement);
        Assert.True(p99 <= 10, measurement);
    }

    private static double Percentile(double[] sorted, double percentile)
    {
        var index = Math.Clamp(
            (int)Math.Ceiling(sorted.Length * percentile) - 1,
            0,
            sorted.Length - 1);
        return sorted[index];
    }
}
