using Game.Core.Combat;
using Xunit;

namespace Game.Tests.CombatTests;

public sealed class AttackRuntimeStateTests
{
    [Fact]
    public void Advance_TransitionsThroughWindupActiveRecoveryAndIdle()
    {
        var state = new AttackRuntimeState();
        var definition = CreateAttack();

        var started = state.TryStart(definition);
        var active = state.Advance(0.1f);
        var recovery = state.Advance(0.2f);
        var completed = state.Advance(0.3f);

        Assert.True(started.Accepted);
        Assert.Equal(AttackPhase.Active, active.Phase);
        Assert.Contains(active.Events, gameEvent => gameEvent is AttackPhaseChangedCombatEvent);
        Assert.Equal(AttackPhase.Recovery, recovery.Phase);
        Assert.Equal(AttackPhase.Idle, completed.Phase);
        Assert.True(completed.Completed);
        Assert.Contains(completed.Events, gameEvent => gameEvent is AttackCompletedCombatEvent);
    }

    [Fact]
    public void TryRegisterHit_OnlyAcceptsEachTargetOnceDuringActivePhase()
    {
        var state = new AttackRuntimeState();
        _ = state.TryStart(CreateAttack());

        Assert.False(state.TryRegisterHit(7));
        state.Advance(0.1f);
        Assert.True(state.TryRegisterHit(7));
        Assert.False(state.TryRegisterHit(7));
        Assert.True(state.TryRegisterHit(8));
        state.Advance(0.2f);
        Assert.False(state.TryRegisterHit(9));
    }

    [Fact]
    public void TryRegisterHit_StopsAtConfiguredBoundedCapacity()
    {
        var state = new AttackRuntimeState(hitCapacity: 2);
        _ = state.TryStart(CreateAttack() with { WindupSeconds = 0 });

        Assert.True(state.TryRegisterHit(1));
        Assert.True(state.TryRegisterHit(2));
        Assert.False(state.TryRegisterHit(3));
    }

    [Fact]
    public void TryQueueCombo_InsideWindowStartsNextAttackDeterministically()
    {
        var state = new AttackRuntimeState(nextInstanceId: 10);
        var first = CreateAttack() with
        {
            ComboWindowStartSeconds = 0.2f,
            ComboWindowEndSeconds = 0.5f
        };
        var second = CreateAttack() with
        {
            Id = "slash-2",
            WindupSeconds = 0
        };
        _ = state.TryStart(first);
        state.Advance(0.2f);

        var queued = state.TryQueueCombo(second);
        var advanced = state.Advance(0.4f);

        Assert.True(queued.Accepted);
        Assert.Equal("slash-2", state.Definition?.Id);
        Assert.Equal(1, state.ComboIndex);
        Assert.Equal(11UL, state.AttackInstanceId);
        Assert.True(state.IsAttacking);
        Assert.Contains(advanced.Events, gameEvent => gameEvent is AttackStartedCombatEvent started && started.AttackId == "slash-2");
    }

    [Fact]
    public void TryQueueCombo_OutsideWindowReturnsTypedFailure()
    {
        var state = new AttackRuntimeState();
        var definition = CreateAttack() with
        {
            ComboWindowStartSeconds = 0.2f,
            ComboWindowEndSeconds = 0.4f
        };
        _ = state.TryStart(definition);

        var result = state.TryQueueCombo(CreateAttack() with { Id = "slash-2" });

        Assert.False(result.Accepted);
        Assert.Equal(AttackCommandFailure.OutsideComboWindow, result.Failure);
    }

    [Fact]
    public void TryStart_InvalidDefinitionReturnsTypedFailure()
    {
        var state = new AttackRuntimeState();

        var result = state.TryStart(CreateAttack() with { ActiveSeconds = 0 });

        Assert.False(result.Accepted);
        Assert.Equal(AttackCommandFailure.InvalidDefinition, result.Failure);
        Assert.Equal(AttackPhase.Idle, state.Phase);
    }

    private static AttackDefinition CreateAttack()
    {
        return new AttackDefinition
        {
            Id = "slash-1",
            WindupSeconds = 0.1f,
            ActiveSeconds = 0.2f,
            RecoverySeconds = 0.3f
        };
    }
}
