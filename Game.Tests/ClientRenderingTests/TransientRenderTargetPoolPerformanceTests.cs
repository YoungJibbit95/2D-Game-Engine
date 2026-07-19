using System.Diagnostics;
using Game.Client.Rendering.Graph;
using Xunit;
using Xunit.Abstractions;

namespace Game.Tests.ClientRenderingTests;

public sealed class TransientRenderTargetPoolPerformanceTests
{
    private const int SampleCount = 10_000;
    private readonly ITestOutputHelper _output;

    public TransientRenderTargetPoolPerformanceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void AcquireAndBind_RecordZeroSteadyAllocationAndTimingDistribution()
    {
        var graph = TransientRenderTargetPoolTests.CreateLinearGraph();
        var plan = graph.Compile([TransientRenderTargetPoolTests.Resource(5)]);
        var factory = new TransientRenderTargetPoolTests.FakeFactory();
        using var pool = new TransientRenderTargetPool(factory, 3, 3);
        var descriptor = TransientRenderTargetPoolTests.Descriptor(256, 144);
        var lease = pool.PrepareForTesting(
            factory.DeviceA,
            plan,
            TransientRenderTargetPoolTests.Bindings(descriptor, descriptor, descriptor));
        for (var warmup = 0; warmup < 128; warmup++)
        {
            _ = pool.AcquireResourceForTesting(
                TransientRenderTargetPoolTests.Resource(2),
                lease,
                passIndex: 1);
            pool.BindForTesting(
                factory.DeviceA,
                TransientRenderTargetPoolTests.Resource(2),
                lease,
                passIndex: 1);
        }

        var microseconds = new double[SampleCount];
        ITransientRenderTargetResource? last = null;
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        for (var sample = 0; sample < SampleCount; sample++)
        {
            var startedAt = Stopwatch.GetTimestamp();
            last = pool.AcquireResourceForTesting(
                TransientRenderTargetPoolTests.Resource(2),
                lease,
                passIndex: 1);
            pool.BindForTesting(
                factory.DeviceA,
                TransientRenderTargetPoolTests.Resource(2),
                lease,
                passIndex: 1);
            microseconds[sample] = Stopwatch.GetElapsedTime(startedAt).TotalMicroseconds;
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        GC.KeepAlive(last);
        Array.Sort(microseconds);
        var measurement =
            $"transient target acquire+bind: p50={Percentile(microseconds, 0.50):F3} us, " +
            $"p95={Percentile(microseconds, 0.95):F3} us, " +
            $"p99={Percentile(microseconds, 0.99):F3} us, " +
            $"allocation={allocated / (double)SampleCount:F1} B/iteration";
        _output.WriteLine(measurement);

        Assert.Equal(0, allocated);
        Assert.Equal(SampleCount + 128, factory.BindCount);
    }

    private static double Percentile(double[] sorted, double percentile)
    {
        var index = Math.Clamp((int)Math.Ceiling(sorted.Length * percentile) - 1, 0, sorted.Length - 1);
        return sorted[index];
    }
}
