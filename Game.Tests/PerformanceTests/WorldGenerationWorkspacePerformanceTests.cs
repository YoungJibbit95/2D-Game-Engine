using System.Diagnostics;
using Game.Core.World;
using Xunit;

namespace Game.Tests.PerformanceTests;

[Collection(LongSessionPerformanceCollection.Name)]
public sealed class WorldGenerationWorkspacePerformanceTests
{
    [Fact]
    [Trait("Category", "Performance")]
    public void SimpleWorld256By128_StaysInsideMaterializationAllocationBudget()
    {
        const int allocationBudgetBytes = 1_400_000;
        var generator = new SimpleWorldGenerator();
        _ = generator.Generate(256, 128, seed: 1337);

        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();

        var before = GC.GetAllocatedBytesForCurrentThread();
        var startedAt = Stopwatch.GetTimestamp();
        var world = generator.Generate(256, 128, seed: 1337);
        var elapsed = Stopwatch.GetElapsedTime(startedAt);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(24, world.Chunks.Count);
        Assert.True(
            allocated <= allocationBudgetBytes,
            $"Finite generation allocated {allocated:N0} B; budget is {allocationBudgetBytes:N0} B. " +
            $"Elapsed={elapsed.TotalMilliseconds:F3} ms.");
    }
}
