using Game.Core.Combat;
using Xunit;

namespace Game.Tests.CombatTests;

public sealed class AttackCommandBufferTests
{
    [Fact]
    public void TryEnqueue_PreservesTickThenInsertionOrder()
    {
        var buffer = new AttackCommandBuffer(4);

        var first = buffer.TryEnqueue(10, AttackInputKind.Attack);
        var second = buffer.TryEnqueue(10, AttackInputKind.Cancel);
        var third = buffer.TryEnqueue(12, AttackInputKind.Attack);

        Assert.True(first.Accepted);
        Assert.True(second.Accepted);
        Assert.True(third.Accepted);
        Assert.True(buffer.TryDequeue(out var firstDequeued));
        Assert.True(buffer.TryDequeue(out var secondDequeued));
        Assert.True(buffer.TryDequeue(out var thirdDequeued));
        Assert.Equal(first.Command, firstDequeued);
        Assert.Equal(second.Command, secondDequeued);
        Assert.Equal(third.Command, thirdDequeued);
        Assert.True(firstDequeued.Sequence < secondDequeued.Sequence);
    }

    [Fact]
    public void TryEnqueue_RejectsOutOfOrderAndCapacityWithoutMutatingQueue()
    {
        var buffer = new AttackCommandBuffer(2);
        Assert.True(buffer.TryEnqueue(10, AttackInputKind.Attack).Accepted);

        var outOfOrder = buffer.TryEnqueue(9, AttackInputKind.Attack);
        Assert.True(buffer.TryEnqueue(10, AttackInputKind.Cancel).Accepted);
        var full = buffer.TryEnqueue(11, AttackInputKind.Attack);

        Assert.Equal(AttackInputFailure.OutOfOrder, outOfOrder.Failure);
        Assert.Equal(AttackInputFailure.BufferFull, full.Failure);
        Assert.Equal(2, buffer.Count);
    }

    [Fact]
    public void RingBuffer_ReusesCapacityWithoutChangingSequenceOrder()
    {
        var buffer = new AttackCommandBuffer(2);
        var first = buffer.TryEnqueue(1, AttackInputKind.Attack).Command;
        Assert.True(buffer.TryDequeue(out _));
        var second = buffer.TryEnqueue(2, AttackInputKind.Attack).Command;
        var third = buffer.TryEnqueue(3, AttackInputKind.Cancel).Command;

        Assert.Equal(first.Sequence + 1, second.Sequence);
        Assert.Equal(second.Sequence + 1, third.Sequence);
        Assert.True(buffer.TryDequeue(out var dequeuedSecond));
        Assert.True(buffer.TryDequeue(out var dequeuedThird));
        Assert.Equal(second, dequeuedSecond);
        Assert.Equal(third, dequeuedThird);
    }

    [Fact]
    public void TryEnqueue_HandlesMaximumTickAndReportsSequenceExhaustion()
    {
        var buffer = new AttackCommandBuffer(1, nextSequence: ulong.MaxValue);

        var maximum = buffer.TryEnqueue(ulong.MaxValue, AttackInputKind.Attack);
        Assert.True(maximum.Accepted);
        Assert.Equal(ulong.MaxValue, maximum.Command.Tick);
        Assert.Equal(ulong.MaxValue, maximum.Command.Sequence);
        Assert.True(buffer.TryDequeue(out _));

        var exhausted = buffer.TryEnqueue(ulong.MaxValue, AttackInputKind.Attack);
        Assert.False(exhausted.Accepted);
        Assert.Equal(AttackInputFailure.SequenceExhausted, exhausted.Failure);
    }

    [Fact]
    public void EventBuffer_IsBoundedAndReportsDroppedEvents()
    {
        var buffer = new AttackEventBuffer(1);
        var runtimeEvent = new AttackRuntimeEvent(
            AttackRuntimeEventKind.AttackStarted,
            1,
            2,
            3,
            "slash",
            0,
            AttackRuntimePhase.Idle,
            AttackRuntimePhase.Startup);

        Assert.True(buffer.TryWrite(runtimeEvent));
        Assert.False(buffer.TryWrite(runtimeEvent));
        Assert.Equal(1, buffer.Count);
        Assert.Equal(1, buffer.DroppedCount);
        Assert.Equal(runtimeEvent, buffer[0]);

        buffer.Clear();
        Assert.Equal(0, buffer.Count);
        Assert.Equal(0, buffer.DroppedCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(257)]
    public void Constructor_RejectsInvalidCapacity(int capacity)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new AttackCommandBuffer(capacity));
    }
}
