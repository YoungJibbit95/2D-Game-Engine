using Game.Core.Combat;
using Xunit;

namespace Game.Tests.CombatTests;

public sealed class ManaComponentTests
{
    [Fact]
    public void TrySpend_ConsumesManaAndBlocksWhenInsufficient()
    {
        var mana = new ManaComponent(maxMana: 40, currentMana: 18);

        Assert.True(mana.TrySpend(12));
        Assert.Equal(6, mana.Current);
        Assert.False(mana.TrySpend(7));
        Assert.Equal(6, mana.Current);
    }

    [Fact]
    public void Update_RegeneratesAcrossDelayBoundaryWithoutDroppingFrameRemainder()
    {
        var mana = new ManaComponent(maxMana: 40, currentMana: 20);

        Assert.True(mana.TrySpend(10));
        mana.Update(0.6f, regenPerSecond: 20);
        Assert.Equal(10, mana.Current);
        mana.Update(0.7f, regenPerSecond: 20);
        mana.Update(0.5f, regenPerSecond: 20);

        Assert.Equal(22, mana.Current);
        Assert.Equal(0f, mana.RegenDelayRemaining);
    }

    [Fact]
    public void Reservation_RefundsAtomicallyAndRestoresPreviousRegenerationState()
    {
        var mana = new ManaComponent(maxMana: 40, currentMana: 30);
        Assert.True(mana.TrySpend(5));
        mana.Update(0.4f);
        var previousDelay = mana.RegenDelayRemaining;

        var reserved = mana.TryReserve(3, regenerationDelaySeconds: 2f);
        var refunded = mana.FinalizeWithRefund(
            reserved.Reservation,
            ManaRefundPolicy.BeforeEffect,
            ManaRefundReason.CancelledBeforeEffect);

        Assert.Equal(ManaSpendStatus.Reserved, reserved.Status);
        Assert.Equal(ManaReservationFinalizationStatus.Refunded, refunded.Status);
        Assert.Equal(3, refunded.ManaRestored);
        Assert.Equal(25, mana.Current);
        Assert.Equal(previousDelay, mana.RegenDelayRemaining, precision: 5);
        Assert.Equal(0, mana.OpenReservationCount);
        Assert.Equal(
            ManaReservationFinalizationStatus.InvalidReservation,
            mana.Commit(reserved.Reservation).Status);
    }

    [Fact]
    public void Reservation_AfterEffectRefundIsDeniedAndCostRemainsCommitted()
    {
        var mana = new ManaComponent(maxMana: 20, currentMana: 20);
        var reserved = mana.TryReserve(6);

        var finalization = mana.FinalizeWithRefund(
            reserved.Reservation,
            ManaRefundPolicy.BeforeEffect,
            ManaRefundReason.CancelledAfterEffect);

        Assert.Equal(ManaReservationFinalizationStatus.RefundDenied, finalization.Status);
        Assert.Equal(14, mana.Current);
        Assert.Equal(0, mana.OpenReservationCount);
    }

    [Fact]
    public void Reservation_CapacityFailureDoesNotMutateMana()
    {
        var mana = new ManaComponent(maxMana: 20, reservationCapacity: 1);
        var first = mana.TryReserve(4);
        var currentAfterFirst = mana.Current;

        var rejected = mana.TryReserve(3);

        Assert.Equal(ManaSpendStatus.ReservationCapacityReached, rejected.Status);
        Assert.Equal(currentAfterFirst, mana.Current);
        Assert.Equal(1, mana.OpenReservationCount);
        Assert.Equal(
            ManaReservationFinalizationStatus.Committed,
            mana.Commit(first.Reservation).Status);
    }

    [Theory]
    [InlineData(-1, 1.2f, ManaSpendStatus.InvalidCost)]
    [InlineData(1, -0.1f, ManaSpendStatus.InvalidRegenerationDelay)]
    [InlineData(1, float.NaN, ManaSpendStatus.InvalidRegenerationDelay)]
    public void Reservation_InvalidRequestsAreTypedAndDoNotMutate(
        int cost,
        float delay,
        ManaSpendStatus expected)
    {
        var mana = new ManaComponent(maxMana: 20);
        var result = mana.TryReserve(cost, delay);

        Assert.Equal(expected, result.Status);
        Assert.Equal(20, mana.Current);
        Assert.Equal(0, mana.OpenReservationCount);
    }

    [Fact]
    public void RestoreFull_InvalidatesOutstandingReservationsAcrossRespawn()
    {
        var mana = new ManaComponent(maxMana: 20);
        var reserved = mana.TryReserve(5);

        mana.RestoreFull();

        Assert.Equal(20, mana.Current);
        Assert.Equal(0, mana.OpenReservationCount);
        Assert.Equal(
            ManaReservationFinalizationStatus.InvalidReservation,
            mana.Commit(reserved.Reservation).Status);
    }

    [Fact]
    public void SetMax_PreservesCurrentRatio()
    {
        var mana = new ManaComponent(maxMana: 100, currentMana: 50);

        mana.SetMax(40);

        Assert.Equal(40, mana.Max);
        Assert.Equal(20, mana.Current);
    }

    [Fact]
    public void ReserveCommitRestore_SteadyStateAllocatesNoManagedMemory()
    {
        var mana = new ManaComponent(maxMana: 20);
        for (var index = 0; index < 128; index++)
        {
            ExecuteTransaction(mana);
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < 10_000; index++)
        {
            ExecuteTransaction(mana);
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated);
    }

    private static void ExecuteTransaction(ManaComponent mana)
    {
        var reserved = mana.TryReserve(1, regenerationDelaySeconds: 0f);
        if (reserved.Status != ManaSpendStatus.Reserved ||
            mana.Commit(reserved.Reservation).Status != ManaReservationFinalizationStatus.Committed)
        {
            throw new InvalidOperationException("Mana transaction fixture failed.");
        }

        mana.Restore(1);
    }
}