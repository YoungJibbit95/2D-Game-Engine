using Game.Core.Combat;
using Xunit;

namespace Game.Tests.CombatTests;

public sealed class AttackSequencerTests
{
    [Fact]
    public void AdvanceTo_UsesExactTransitionThenInputOrderOnSharedTick()
    {
        var sequencer = new AttackSequencer(CreateComboSequence());
        var events = new AttackEventBuffer(64);
        Assert.True(sequencer.QueueInput(0).Accepted);
        Assert.True(sequencer.QueueInput(3).Accepted);

        sequencer.AdvanceTo(3, events);

        Assert.Equal(AttackRuntimePhase.Active, sequencer.Phase);
        Assert.True(sequencer.HasQueuedCombo);
        Assert.Equal(
            new[]
            {
                AttackRuntimeEventKind.AttackStarted,
                AttackRuntimeEventKind.PhaseChanged,
                AttackRuntimeEventKind.ActiveWindowOpened,
                AttackRuntimeEventKind.ComboQueued
            },
            EventKinds(events));
        Assert.Equal(2UL, events[3].InputSequence);
    }

    [Fact]
    public void EarlyInput_IsBufferedAndConsumedAtComboWindowStart()
    {
        var sequencer = new AttackSequencer(CreateComboSequence(inputBufferTicks: 3));
        var events = new AttackEventBuffer(64);
        sequencer.QueueInput(0);
        sequencer.QueueInput(1);

        sequencer.AdvanceTo(1, events);
        Assert.True(sequencer.HasBufferedInput);
        Assert.Contains(AttackRuntimeEventKind.InputBuffered, EventKinds(events));

        sequencer.AdvanceTo(3, events);
        Assert.False(sequencer.HasBufferedInput);
        Assert.True(sequencer.HasQueuedCombo);
        Assert.Contains(AttackRuntimeEventKind.ComboQueued, EventKinds(events));
    }

    [Fact]
    public void QueuedCombo_AdvancesAndTerminalStepCompletes()
    {
        var sequencer = new AttackSequencer(CreateComboSequence());
        var events = new AttackEventBuffer(64);
        sequencer.QueueInput(0);
        sequencer.QueueInput(3);

        sequencer.AdvanceTo(7, events);

        Assert.Equal(1, sequencer.ComboIndex);
        Assert.Equal("slash-2", sequencer.CurrentStep?.Id);
        Assert.Equal(2UL, sequencer.AttackInstanceId);
        Assert.Contains(AttackRuntimeEventKind.ComboAdvanced, EventKinds(events));
        Assert.Contains(AttackRuntimeEventKind.AttackStarted, EventKinds(events));

        sequencer.AdvanceTo(11, events);
        Assert.Equal(AttackRuntimePhase.Cooldown, sequencer.Phase);
        Assert.Contains(AttackRuntimeEventKind.ComboCompleted, EventKinds(events));
        Assert.Contains(AttackRuntimeEventKind.AttackCompleted, EventKinds(events));

        sequencer.AdvanceTo(13, events);
        Assert.Equal(AttackRuntimePhase.Idle, sequencer.Phase);
    }

    [Fact]
    public void MissingComboInput_ResetsThenHonorsCooldownBuffer()
    {
        var sequencer = new AttackSequencer(CreateComboSequence(inputBufferTicks: 2));
        var events = new AttackEventBuffer(64);
        sequencer.QueueInput(0);
        sequencer.AdvanceTo(7, events);
        Assert.Equal(AttackRuntimePhase.Cooldown, sequencer.Phase);
        Assert.Contains(AttackRuntimeEventKind.ComboReset, EventKinds(events));

        sequencer.QueueInput(8);
        sequencer.AdvanceTo(8, events);
        Assert.True(sequencer.HasBufferedInput);

        sequencer.AdvanceTo(9, events);
        Assert.Equal(AttackRuntimePhase.Startup, sequencer.Phase);
        Assert.Equal(2UL, sequencer.AttackInstanceId);
        Assert.Contains(AttackRuntimeEventKind.AttackStarted, EventKinds(events));
    }

    [Fact]
    public void LateAndDuplicateComboInputs_AreRejectedWithTypedEvents()
    {
        var sequencer = new AttackSequencer(CreateComboSequence());
        var events = new AttackEventBuffer(64);
        sequencer.QueueInput(0);
        sequencer.QueueInput(3);
        sequencer.QueueInput(4);
        sequencer.QueueInput(6);

        sequencer.AdvanceTo(6, events);

        var rejected = events.AsSpan().ToArray()
            .Where(runtimeEvent => runtimeEvent.Kind == AttackRuntimeEventKind.InputRejected)
            .ToArray();
        Assert.Equal(2, rejected.Length);
        Assert.All(rejected, runtimeEvent => Assert.Equal(AttackInputFailure.LockedOut, runtimeEvent.Failure));
    }

    [Fact]
    public void CancelRules_AllowOnlyConfiguredPhaseWindowAndResetCombo()
    {
        var first = CreateStep(
            "slash-1",
            startup: 2,
            active: 3,
            recovery: 2,
            next: "slash-2",
            combo: new AttackPhaseWindow(2, 6)) with
        {
            CancelRules = new[]
            {
                new AttackCancelRuleDefinition
                {
                    Phase = AttackRuntimePhase.Active,
                    Window = new AttackPhaseWindow(1, 2)
                }
            }
        };
        var sequence = CreateSequence(new[] { first, CreateStep("slash-2", 0, 2, 1) });
        var sequencer = new AttackSequencer(sequence);
        var events = new AttackEventBuffer(64);
        sequencer.QueueInput(0);
        sequencer.QueueInput(2, AttackInputKind.Cancel);
        sequencer.QueueInput(3, AttackInputKind.Cancel);

        sequencer.AdvanceTo(3, events);

        Assert.Equal(AttackRuntimePhase.Cooldown, sequencer.Phase);
        Assert.Contains(events.AsSpan().ToArray(), runtimeEvent =>
            runtimeEvent.Kind == AttackRuntimeEventKind.InputRejected &&
            runtimeEvent.Failure == AttackInputFailure.CancelNotAllowed);
        Assert.Contains(AttackRuntimeEventKind.AttackCancelled, EventKinds(events));
    }

    [Fact]
    public void HitTracking_IsBoundedAndAcceptsEachTargetOncePerSwing()
    {
        var step = CreateStep("slash", startup: 0, active: 3, recovery: 1) with
        {
            MaxTargetsPerSwing = 2
        };
        var sequencer = new AttackSequencer(CreateSequence(new[] { step }));
        var events = new AttackEventBuffer(16);
        sequencer.QueueInput(0);
        sequencer.AdvanceTo(0, events);
        events.Clear();

        var first = sequencer.TryRegisterHit(7, events);
        var duplicate = sequencer.TryRegisterHit(7, events);
        var second = sequencer.TryRegisterHit(8, events);
        var overflow = sequencer.TryRegisterHit(9, events);

        Assert.True(first.Accepted);
        Assert.Equal(AttackInputFailure.DuplicateTarget, duplicate.Failure);
        Assert.True(second.Accepted);
        Assert.Equal(AttackInputFailure.HitCapacityReached, overflow.Failure);
        Assert.Equal(
            new[]
            {
                AttackRuntimeEventKind.HitAccepted,
                AttackRuntimeEventKind.HitRejectedDuplicate,
                AttackRuntimeEventKind.HitAccepted,
                AttackRuntimeEventKind.HitRejected
            },
            EventKinds(events));
    }

    [Fact]
    public void HitTracking_ResetsForNextComboSwing()
    {
        var sequencer = new AttackSequencer(CreateComboSequence());
        var events = new AttackEventBuffer(64);
        sequencer.QueueInput(0);
        sequencer.QueueInput(3);
        sequencer.AdvanceTo(3, events);
        Assert.True(sequencer.TryRegisterHit(10, events).Accepted);

        sequencer.AdvanceTo(7, events);
        sequencer.AdvanceTo(8, events);

        Assert.True(sequencer.TryRegisterHit(10, events).Accepted);
    }

    [Fact]
    public void CopyActiveMeleeShapes_ReturnsOnlyShapesEnabledForCurrentActiveTick()
    {
        var step = CreateStep("slash", startup: 0, active: 4, recovery: 1) with
        {
            MeleeShapes = new[]
            {
                CreateShape("early", 0, 2),
                CreateShape("late", 2, 4)
            }
        };
        var sequencer = new AttackSequencer(CreateSequence(new[] { step }));
        var events = new AttackEventBuffer(16);
        var destination = new SweptMeleeShapeDefinition[2];
        sequencer.QueueInput(0);
        sequencer.AdvanceTo(1, events);

        Assert.Equal(1, sequencer.CopyActiveMeleeShapes(destination));
        Assert.Equal("early", destination[0].Id);

        sequencer.AdvanceTo(2, events);
        Assert.Equal(1, sequencer.CopyActiveMeleeShapes(destination));
        Assert.Equal("late", destination[0].Id);
    }

    [Fact]
    public void SixtyHertzRuns_ProduceIdenticalEventStreamsAndState()
    {
        var first = RunDeterministicScenario();
        var second = RunDeterministicScenario();

        Assert.Equal(first.Events, second.Events);
        Assert.Equal(first.Phase, second.Phase);
        Assert.Equal(first.AttackInstanceId, second.AttackInstanceId);
        Assert.Equal(first.ComboIndex, second.ComboIndex);
    }

    [Fact]
    public void AdvanceTo_ExtremeTicksDoesNotIterateEveryTickOrOverflow()
    {
        var sequencer = new AttackSequencer(CreateSequence(new[]
        {
            CreateStep("slash", startup: 2, active: 3, recovery: 2, cooldown: 0)
        }));
        var events = new AttackEventBuffer(32);
        var start = ulong.MaxValue - 10;
        sequencer.AdvanceTo(start, events);
        Assert.True(sequencer.QueueInput(start).Accepted);

        sequencer.AdvanceTo(ulong.MaxValue - 3, events);

        Assert.Equal(AttackRuntimePhase.Idle, sequencer.Phase);
        Assert.Contains(AttackRuntimeEventKind.ComboCompleted, EventKinds(events));
    }

    [Fact]
    public void SteadyAttackCycles_AllocateZeroBytesAfterWarmup()
    {
        var sequencer = new AttackSequencer(CreateSequence(new[]
        {
            CreateStep("slash", startup: 0, active: 1, recovery: 1)
        }));
        var events = new AttackEventBuffer(16);
        for (ulong tick = 0; tick < 64; tick += 2)
        {
            sequencer.QueueInput(tick);
            sequencer.AdvanceTo(tick + 2, events);
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (ulong tick = 64; tick < 2064; tick += 2)
        {
            sequencer.QueueInput(tick);
            sequencer.AdvanceTo(tick + 2, events);
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated);
    }

    [Fact]
    public void TinyEventBuffer_DropsEventsWithoutChangingRuntimeState()
    {
        var sequencer = new AttackSequencer(CreateSequence(new[]
        {
            CreateStep("slash", startup: 1, active: 1, recovery: 0, cooldown: 0)
        }));
        var events = new AttackEventBuffer(1);
        sequencer.QueueInput(0);

        var result = sequencer.AdvanceTo(2, events);

        Assert.Equal(AttackRuntimePhase.Idle, sequencer.Phase);
        Assert.Equal(1, result.EventsWritten);
        Assert.True(result.EventsDropped >= 5);
    }

    private static DeterministicResult RunDeterministicScenario()
    {
        var sequencer = new AttackSequencer(CreateComboSequence());
        var buffer = new AttackEventBuffer(64);
        var events = new List<AttackRuntimeEvent>();
        sequencer.QueueInput(0);
        sequencer.QueueInput(3);
        sequencer.QueueInput(15);
        for (ulong tick = 0; tick < 60; tick++)
        {
            sequencer.AdvanceTo(tick, buffer);
            events.AddRange(buffer.AsSpan().ToArray());
        }

        return new DeterministicResult(events.ToArray(), sequencer.Phase, sequencer.AttackInstanceId, sequencer.ComboIndex);
    }

    private static AttackSequenceDefinition CreateComboSequence(int inputBufferTicks = 3)
    {
        return CreateSequence(
            new[]
            {
                CreateStep(
                    "slash-1",
                    startup: 2,
                    active: 3,
                    recovery: 2,
                    cooldown: 2,
                    next: "slash-2",
                    combo: new AttackPhaseWindow(3, 6)),
                CreateStep("slash-2", startup: 0, active: 2, recovery: 2, cooldown: 2)
            },
            inputBufferTicks);
    }

    private static AttackSequenceDefinition CreateSequence(
        AttackComboStepDefinition[] steps,
        int inputBufferTicks = 3)
    {
        return new AttackSequenceDefinition
        {
            Id = "test-combo",
            Steps = steps,
            InputBufferTicks = inputBufferTicks,
            CommandCapacity = 16,
            EventCapacity = 64
        };
    }

    private static AttackComboStepDefinition CreateStep(
        string id,
        int startup = 1,
        int active = 3,
        int recovery = 2,
        int cooldown = 2,
        string? next = null,
        AttackPhaseWindow? combo = null)
    {
        return new AttackComboStepDefinition
        {
            Id = id,
            NextStepId = next,
            Timeline = AttackTimelineDefinition.Create(startup, active, recovery, cooldown, combo),
            Cost = new AttackResourceCost(Stamina: 4)
        };
    }

    private static SweptMeleeShapeDefinition CreateShape(string id, int start, int end)
    {
        return new SweptMeleeShapeDefinition
        {
            Id = id,
            Sweep = new MeleeSweepDefinition(),
            ActiveStartTickInclusive = start,
            ActiveEndTickExclusive = end
        };
    }

    private static AttackRuntimeEventKind[] EventKinds(AttackEventBuffer events) =>
        events.AsSpan().ToArray().Select(runtimeEvent => runtimeEvent.Kind).ToArray();

    private sealed record DeterministicResult(
        AttackRuntimeEvent[] Events,
        AttackRuntimePhase Phase,
        ulong AttackInstanceId,
        int ComboIndex);
}
