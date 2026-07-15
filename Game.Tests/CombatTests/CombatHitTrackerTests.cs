using Game.Core.Combat;
using Xunit;

namespace Game.Tests.CombatTests;

public sealed class CombatHitTrackerTests
{
    [Fact]
    public void TryRegister_EnforcesPerAttackTargetCapacity()
    {
        var tracker = new CombatHitTracker(concurrentAttackCapacity: 2, targetsPerAttackCapacity: 2);

        Assert.True(tracker.TryRegister(1, 10));
        Assert.False(tracker.TryRegister(1, 10));
        Assert.True(tracker.TryRegister(1, 11));
        Assert.False(tracker.TryRegister(1, 12));
    }

    [Fact]
    public void TryRegister_EnforcesConcurrentCapacityAndReusesCompletedSlot()
    {
        var tracker = new CombatHitTracker(concurrentAttackCapacity: 2, targetsPerAttackCapacity: 1);
        Assert.True(tracker.TryRegister(1, 10));
        Assert.True(tracker.TryRegister(2, 20));
        Assert.False(tracker.TryRegister(3, 30));

        Assert.True(tracker.CompleteAttack(1));
        Assert.True(tracker.TryRegister(3, 30));
        Assert.False(tracker.CompleteAttack(99));
    }

    [Fact]
    public void Clear_ResetsAllAttackAndTargetSlots()
    {
        var tracker = new CombatHitTracker(concurrentAttackCapacity: 1, targetsPerAttackCapacity: 1);
        Assert.True(tracker.TryRegister(1, 10));

        tracker.Clear();

        Assert.True(tracker.TryRegister(2, 10));
        Assert.True(tracker.CompleteAttack(2));
        Assert.True(tracker.TryRegister(2, 10));
    }

    [Fact]
    public void ZeroAttackId_PreservesUntrackedLegacyBehavior()
    {
        var tracker = new CombatHitTracker(1, 1);

        Assert.True(tracker.TryRegister(0, 10));
        Assert.True(tracker.TryRegister(0, 10));
        Assert.False(tracker.CompleteAttack(0));
    }

    [Fact]
    public void RegisterCompleteCycles_AllocateZeroBytesAfterConstruction()
    {
        var tracker = new CombatHitTracker(1, 4);
        for (var index = 0; index < 32; index++)
        {
            tracker.TryRegister((ulong)(index + 1), index);
            tracker.CompleteAttack((ulong)(index + 1));
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < 1024; index++)
        {
            tracker.TryRegister((ulong)(index + 100), index);
            tracker.CompleteAttack((ulong)(index + 100));
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(257, 1)]
    [InlineData(1, 0)]
    [InlineData(1, 1025)]
    public void Constructor_RejectsInvalidCapacities(int attacks, int targets)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CombatHitTracker(attacks, targets));
    }
}
