using Game.Core.Runtime;
using Xunit;

namespace Game.Tests.PerformanceTests;

[Collection(LongSessionPerformanceCollection.Name)]
public sealed class ImmutableSnapshotListPerformanceTests
{
    [Fact]
    public void PublicConstructor_DetachesFromMutableSources()
    {
        var array = new[] { 1, 2, 3 };
        var list = new List<int> { 4, 5, 6 };
        var arraySnapshot = new ImmutableSnapshotList<int>(array);
        var listSnapshot = new ImmutableSnapshotList<int>(list);

        array[0] = 99;
        list[0] = 99;
        list.Add(7);

        Assert.Equal([1, 2, 3], arraySnapshot);
        Assert.Equal([4, 5, 6], listSnapshot);
    }

    [Fact]
    public void DirectEnumeration_IsAllocationFreeAcrossSteadyStateLoop()
    {
        var snapshot = new ImmutableSnapshotList<int>(Enumerable.Range(1, 64));
        const int iterations = 100_000;
        var checksum = 0;

        for (var index = 0; index < 2_000; index++)
        {
            checksum ^= Sum(snapshot);
        }

        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < iterations; index++)
        {
            checksum ^= Sum(snapshot);
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        Assert.Equal(0, allocated);
        Assert.Equal(0, checksum);
    }

    [Fact]
    public void TrustedOwnershipFactory_DoesNotDuplicateOwnedArrayStorage()
    {
        var owned = Enumerable.Range(1, 64).ToArray();
        const int iterations = 10_000;
        var checksum = 0;

        for (var index = 0; index < 2_000; index++)
        {
            checksum ^= ImmutableSnapshotList<int>.FromOwned(owned)[0];
        }

        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < iterations; index++)
        {
            checksum ^= ImmutableSnapshotList<int>.FromOwned(owned)[0];
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        Assert.InRange(allocated, 1, 1_000_000);
        Assert.Equal(0, checksum);
    }

    [Fact]
    public void RepeatedFrameElements_UseValueSemantics()
    {
        Assert.True(typeof(EntityFrameSnapshot).IsValueType);
        Assert.True(typeof(FarmPlotFrameSnapshot).IsValueType);
    }

    private static int Sum(ImmutableSnapshotList<int> snapshot)
    {
        var sum = 0;
        foreach (var value in snapshot)
        {
            sum += value;
        }

        return sum;
    }
}
