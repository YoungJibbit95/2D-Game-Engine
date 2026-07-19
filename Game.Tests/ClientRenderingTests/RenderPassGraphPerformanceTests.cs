using System.Diagnostics;
using Game.Client.Rendering.Graph;
using Xunit;
using Xunit.Abstractions;

namespace Game.Tests.ClientRenderingTests;

public sealed class RenderPassGraphPerformanceTests
{
    private const int SampleCount = 2_000;
    private readonly ITestOutputHelper _output;

    public RenderPassGraphPerformanceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void CompilePlanQueryAndExecution_RecordZeroSteadyAllocationAndTimingDistribution()
    {
        var graph = CreateRepresentativeGraph();
        var outputs = new[] { new RenderResourceId(15) };
        for (var warmup = 0; warmup < 128; warmup++)
        {
            var warmPlan = graph.Compile(outputs);
            var warmExecutor = new CountingExecutor();
            warmPlan.Execute(ref warmExecutor);
        }

        var microseconds = new double[SampleCount];
        var checksum = 0;
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        for (var sample = 0; sample < SampleCount; sample++)
        {
            var startedAt = Stopwatch.GetTimestamp();
            var plan = graph.Compile(outputs);
            var executor = new CountingExecutor();
            checksum += plan.Execute(ref executor);
            checksum += plan.GetResourceLifetime(new RenderResourceId(7)).AliasSlot;
            checksum += executor.Checksum;
            microseconds[sample] = Stopwatch.GetElapsedTime(startedAt).TotalMicroseconds;
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        Array.Sort(microseconds);
        var measurement =
            $"render graph compile+query+execute: p50={Percentile(microseconds, 0.50):F3} us, " +
            $"p95={Percentile(microseconds, 0.95):F3} us, " +
            $"p99={Percentile(microseconds, 0.99):F3} us, " +
            $"allocation={allocated / (double)SampleCount:F1} B/compile";
        _output.WriteLine(measurement);

        Assert.NotEqual(0, checksum);
        Assert.Equal(0, allocated);
    }

    private static RenderPassGraph CreateRepresentativeGraph()
    {
        var graph = new RenderPassGraph(maximumPasses: 32, maximumResources: 32);
        graph.DeclareResource(new RenderResourceId(0), RenderResourceFlags.Imported);
        for (var resource = 1; resource <= 14; resource++)
        {
            graph.DeclareResource(new RenderResourceId(resource), RenderResourceFlags.Transient);
        }

        graph.DeclareResource(new RenderResourceId(15), RenderResourceFlags.None);
        for (var pass = 0; pass < 14; pass++)
        {
            var phase = pass switch
            {
                0 => RenderPassPhase.Opaque,
                <= 3 => RenderPassPhase.Lighting,
                <= 10 => RenderPassPhase.PostProcess,
                _ => RenderPassPhase.Composite
            };
            graph.DeclarePass(
                new RenderPassId(pass),
                phase,
                [new RenderResourceId(pass)],
                [new RenderResourceId(pass + 1)]);
        }

        graph.DeclarePass(
            new RenderPassId(14),
            RenderPassPhase.Composite,
            [new RenderResourceId(14)],
            [new RenderResourceId(15)]);
        return graph;
    }

    private static double Percentile(double[] sorted, double percentile)
    {
        var index = Math.Clamp((int)Math.Ceiling(sorted.Length * percentile) - 1, 0, sorted.Length - 1);
        return sorted[index];
    }

    private struct CountingExecutor : IRenderPassExecutor
    {
        public int Checksum { get; private set; }

        public void Execute(in RenderPassDescriptor pass)
        {
            Checksum = unchecked(Checksum * 31 + pass.Id.Value + 1);
        }
    }
}
