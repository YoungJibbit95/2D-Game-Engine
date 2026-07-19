namespace Game.Core.Combat;

public sealed class AttackSequencer
{
    private readonly AttackComboStepDefinition[] _steps;
    private readonly int[] _nextStepIndices;
    private readonly int[] _hitTargets;
    private readonly AttackCommandBuffer _commands;
    private int _stepIndex = -1;
    private int _comboIndex;
    private int _hitCount;
    private ulong _currentTick;
    private ulong _attackStartTick;
    private ulong _nextAttackInstanceId = 1;
    private ulong _nextTransitionTick;
    private ulong _cooldownUntilTick;
    private bool _hasTransition;
    private bool _comboQueued;
    private bool _hasBufferedInput;
    private ulong _queuedComboInputSequence;
    private AttackInputCommand _bufferedInput;
    private ulong _bufferActivationTick;
    private ulong _bufferExpiryTick;
    private IAttackStartGate? _startGate;

    public AttackSequencer(AttackSequenceDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        definition.Validate();
        Definition = definition;
        _steps = new AttackComboStepDefinition[definition.Steps.Count];
        _nextStepIndices = new int[definition.Steps.Count];
        var maximumHitCapacity = 1;
        for (var index = 0; index < definition.Steps.Count; index++)
        {
            _steps[index] = definition.Steps[index];
            maximumHitCapacity = Math.Max(maximumHitCapacity, _steps[index].MaxTargetsPerSwing);
        }

        for (var index = 0; index < _steps.Length; index++)
        {
            _nextStepIndices[index] = FindStepIndex(_steps[index].NextStepId);
        }

        _hitTargets = new int[maximumHitCapacity];
        _commands = new AttackCommandBuffer(definition.CommandCapacity);
    }

    public AttackSequenceDefinition Definition { get; }

    public AttackRuntimePhase Phase { get; private set; }

    public ulong CurrentTick => _currentTick;

    public ulong AttackInstanceId { get; private set; }

    public int ComboIndex => _comboIndex;

    public AttackComboStepDefinition? CurrentStep => _stepIndex < 0 ? null : _steps[_stepIndex];

    public bool IsAttacking => Phase is
        AttackRuntimePhase.Startup or AttackRuntimePhase.Active or AttackRuntimePhase.Recovery;

    public bool IsActiveWindowOpen => Phase == AttackRuntimePhase.Active;

    public bool HasQueuedCombo => _comboQueued;

    public bool HasBufferedInput => _hasBufferedInput;

    public int PendingCommandCount => _commands.Count;

    public AttackEventBuffer CreateEventBuffer() => new(Definition.EventCapacity);

    public int ActivePhaseTick
    {
        get
        {
            if (Phase != AttackRuntimePhase.Active || _stepIndex < 0)
            {
                return -1;
            }

            var activeStart = AddSaturating(_attackStartTick, (ulong)_steps[_stepIndex].Timeline.Active.StartTickInclusive);
            return SaturateToInt(_currentTick - activeStart);
        }
    }

    public AttackInputResult QueueInput(ulong tick, AttackInputKind kind = AttackInputKind.Attack)
    {
        if (tick < _currentTick)
        {
            return AttackInputResult.Rejected(AttackInputFailure.OutOfOrder);
        }

        return _commands.TryEnqueue(tick, kind);
    }

    public AttackSequencerAdvanceResult AdvanceTo(
        ulong tick,
        AttackEventBuffer events,
        IAttackStartGate? startGate = null)
    {
        ArgumentNullException.ThrowIfNull(events);
        if (tick < _currentTick)
        {
            throw new ArgumentOutOfRangeException(nameof(tick));
        }

        events.Clear();
        _startGate = startGate;
        try
        {
            while (TryGetNextMilestone(tick, out var milestone))
            {
                _currentTick = milestone;
                ProcessTransitions(events);
                ProcessBufferedInput(events);
                ProcessCommandsAtCurrentTick(events);
            }

            _currentTick = tick;
            return new AttackSequencerAdvanceResult(
                tick,
                Phase,
                events.Count,
                events.DroppedCount,
                events.Count > 0 || events.DroppedCount > 0);
        }
        finally
        {
            _startGate = null;
        }
    }

    public AttackHitRegistrationResult TryRegisterHit(int targetEntityId, AttackEventBuffer events)
    {
        ArgumentNullException.ThrowIfNull(events);
        if (targetEntityId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(targetEntityId));
        }

        if (Phase != AttackRuntimePhase.Active || _stepIndex < 0)
        {
            WriteEvent(
                events,
                AttackRuntimeEventKind.HitRejected,
                failure: AttackInputFailure.HitWindowClosed,
                targetEntityId: targetEntityId);
            return new AttackHitRegistrationResult(
                false,
                AttackInputFailure.HitWindowClosed,
                AttackInstanceId,
                targetEntityId);
        }

        for (var index = 0; index < _hitCount; index++)
        {
            if (_hitTargets[index] != targetEntityId)
            {
                continue;
            }

            WriteEvent(
                events,
                AttackRuntimeEventKind.HitRejectedDuplicate,
                failure: AttackInputFailure.DuplicateTarget,
                targetEntityId: targetEntityId);
            return new AttackHitRegistrationResult(
                false,
                AttackInputFailure.DuplicateTarget,
                AttackInstanceId,
                targetEntityId);
        }

        if (_hitCount >= _steps[_stepIndex].MaxTargetsPerSwing)
        {
            WriteEvent(
                events,
                AttackRuntimeEventKind.HitRejected,
                failure: AttackInputFailure.HitCapacityReached,
                targetEntityId: targetEntityId);
            return new AttackHitRegistrationResult(
                false,
                AttackInputFailure.HitCapacityReached,
                AttackInstanceId,
                targetEntityId);
        }

        _hitTargets[_hitCount++] = targetEntityId;
        WriteEvent(events, AttackRuntimeEventKind.HitAccepted, targetEntityId: targetEntityId);
        return new AttackHitRegistrationResult(true, AttackInputFailure.None, AttackInstanceId, targetEntityId);
    }

    public int CopyActiveMeleeShapes(Span<SweptMeleeShapeDefinition> destination)
    {
        if (Phase != AttackRuntimePhase.Active || _stepIndex < 0)
        {
            return 0;
        }

        var activeTick = ActivePhaseTick;
        var shapes = _steps[_stepIndex].MeleeShapes;
        var count = 0;
        for (var index = 0; index < shapes.Count && count < destination.Length; index++)
        {
            var shape = shapes[index];
            if (activeTick >= shape.ActiveStartTickInclusive && activeTick < shape.ActiveEndTickExclusive)
            {
                destination[count++] = shape;
            }
        }

        return count;
    }

    private void ProcessCommandsAtCurrentTick(AttackEventBuffer events)
    {
        while (_commands.TryPeek(out var command) && command.Tick <= _currentTick)
        {
            _commands.TryDequeue(out command);
            if (command.Kind == AttackInputKind.Cancel)
            {
                ProcessCancel(command, events);
            }
            else
            {
                ProcessAttackInput(command, events);
            }
        }
    }

    private void ProcessAttackInput(AttackInputCommand command, AttackEventBuffer events)
    {
        if (Phase == AttackRuntimePhase.Idle)
        {
            StartAttack(stepIndex: 0, comboIndex: 0, command.Sequence, events);
            return;
        }

        if (Phase == AttackRuntimePhase.Cooldown)
        {
            BufferOrReject(command, _cooldownUntilTick, AttackInputFailure.LockedOut, events);
            return;
        }

        var nextStepIndex = _nextStepIndices[_stepIndex];
        if (nextStepIndex < 0)
        {
            RejectInput(command, AttackInputFailure.LockedOut, events);
            return;
        }

        if (_comboQueued)
        {
            RejectInput(command, AttackInputFailure.LockedOut, events);
            return;
        }

        var comboWindow = _steps[_stepIndex].Timeline.ComboWindow!.Value;
        var comboOpen = AddSaturating(_attackStartTick, (ulong)comboWindow.StartTickInclusive);
        var comboClose = AddSaturating(_attackStartTick, (ulong)comboWindow.EndTickExclusive);
        if (_currentTick >= comboOpen && _currentTick < comboClose)
        {
            _comboQueued = true;
            _queuedComboInputSequence = command.Sequence;
            WriteEvent(events, AttackRuntimeEventKind.ComboQueued, command.Sequence);
            return;
        }

        if (_currentTick < comboOpen)
        {
            BufferOrReject(command, comboOpen, AttackInputFailure.OutsideComboWindow, events);
            return;
        }

        RejectInput(command, AttackInputFailure.OutsideComboWindow, events);
    }

    private void ProcessCancel(AttackInputCommand command, AttackEventBuffer events)
    {
        if (!IsAttacking || _stepIndex < 0 || !CanCancelAtCurrentTick())
        {
            RejectInput(command, AttackInputFailure.CancelNotAllowed, events);
            return;
        }

        var previous = Phase;
        var hadComboProgress = _comboIndex > 0 || _comboQueued;
        var cancelledPhase = _steps[_stepIndex].Timeline.CooldownTicks > 0
            ? AttackRuntimePhase.Cooldown
            : AttackRuntimePhase.Idle;
        WriteEvent(events, AttackRuntimeEventKind.AttackCancelled, command.Sequence, previous, cancelledPhase);
        if (hadComboProgress)
        {
            WriteEvent(events, AttackRuntimeEventKind.ComboReset, command.Sequence);
        }

        EnterCooldown(previous, events);
    }

    private bool CanCancelAtCurrentTick()
    {
        var step = _steps[_stepIndex];
        var phaseStart = Phase switch
        {
            AttackRuntimePhase.Startup => step.Timeline.Startup.StartTickInclusive,
            AttackRuntimePhase.Active => step.Timeline.Active.StartTickInclusive,
            AttackRuntimePhase.Recovery => step.Timeline.Recovery.StartTickInclusive,
            _ => -1
        };
        if (phaseStart < 0)
        {
            return false;
        }

        var absolutePhaseStart = AddSaturating(_attackStartTick, (ulong)phaseStart);
        var phaseTick = SaturateToInt(_currentTick - absolutePhaseStart);
        for (var index = 0; index < step.CancelRules.Count; index++)
        {
            var rule = step.CancelRules[index];
            if (rule.Phase == Phase && rule.Window.Contains(phaseTick))
            {
                return true;
            }
        }

        return false;
    }

    private void BufferOrReject(
        AttackInputCommand command,
        ulong activationTick,
        AttackInputFailure failure,
        AttackEventBuffer events)
    {
        if (_hasBufferedInput)
        {
            RejectInput(command, AttackInputFailure.AlreadyBuffered, events);
            return;
        }

        var expiry = AddSaturating(command.Tick, (ulong)Definition.InputBufferTicks);
        if (Definition.InputBufferTicks == 0 || activationTick > expiry)
        {
            RejectInput(command, failure, events);
            return;
        }

        _hasBufferedInput = true;
        _bufferedInput = command;
        _bufferActivationTick = activationTick;
        _bufferExpiryTick = expiry;
        WriteEvent(events, AttackRuntimeEventKind.InputBuffered, command.Sequence);
    }

    private void ProcessBufferedInput(AttackEventBuffer events)
    {
        if (!_hasBufferedInput || _currentTick < _bufferActivationTick)
        {
            return;
        }

        var command = _bufferedInput;
        var expiryTick = _bufferExpiryTick;
        ClearBufferedInput();
        if (_currentTick > expiryTick)
        {
            RejectInput(command, AttackInputFailure.LockedOut, events);
            return;
        }

        ProcessAttackInput(command, events);
    }

    private void ProcessTransitions(AttackEventBuffer events)
    {
        while (_hasTransition && _nextTransitionTick <= _currentTick)
        {
            switch (Phase)
            {
                case AttackRuntimePhase.Startup:
                    ChangePhase(AttackRuntimePhase.Active, events);
                    WriteEvent(events, AttackRuntimeEventKind.ActiveWindowOpened);
                    _nextTransitionTick = AddSaturating(
                        _attackStartTick,
                        (ulong)_steps[_stepIndex].Timeline.Active.EndTickExclusive);
                    break;
                case AttackRuntimePhase.Active:
                    WriteEvent(events, AttackRuntimeEventKind.ActiveWindowClosed);
                    ChangePhase(AttackRuntimePhase.Recovery, events);
                    _nextTransitionTick = AddSaturating(
                        _attackStartTick,
                        (ulong)_steps[_stepIndex].Timeline.Recovery.EndTickExclusive);
                    break;
                case AttackRuntimePhase.Recovery:
                    CompleteCurrentStep(events);
                    break;
                case AttackRuntimePhase.Cooldown:
                    ChangePhase(AttackRuntimePhase.Idle, events);
                    _hasTransition = false;
                    _stepIndex = -1;
                    _comboIndex = 0;
                    break;
                default:
                    _hasTransition = false;
                    break;
            }
        }
    }

    private void CompleteCurrentStep(AttackEventBuffer events)
    {
        WriteEvent(events, AttackRuntimeEventKind.AttackCompleted);
        var nextStepIndex = _nextStepIndices[_stepIndex];
        if (_comboQueued && nextStepIndex >= 0)
        {
            var inputSequence = _queuedComboInputSequence;
            if (StartAttack(nextStepIndex, _comboIndex + 1, inputSequence, events))
            {
                WriteEvent(events, AttackRuntimeEventKind.ComboAdvanced, inputSequence);
                return;
            }

            WriteEvent(events, AttackRuntimeEventKind.ComboReset, inputSequence);
            EnterCooldown(AttackRuntimePhase.Recovery, events);
            return;
        }

        if (nextStepIndex >= 0)
        {
            WriteEvent(events, AttackRuntimeEventKind.ComboReset);
        }
        else
        {
            WriteEvent(events, AttackRuntimeEventKind.ComboCompleted);
        }

        EnterCooldown(AttackRuntimePhase.Recovery, events);
    }

    private bool StartAttack(int stepIndex, int comboIndex, ulong inputSequence, AttackEventBuffer events)
    {
        var step = _steps[stepIndex];
        var startRequest = new AttackStartRequest(
            _currentTick,
            inputSequence,
            Definition.Id,
            step.Id,
            comboIndex,
            step.Cost);
        var failure = _startGate?.TryAccept(startRequest) ?? AttackInputFailure.None;
        if (failure != AttackInputFailure.None)
        {
            WriteEvent(
                events,
                AttackRuntimeEventKind.InputRejected,
                inputSequence,
                failure: failure,
                cost: step.Cost,
                attackId: step.Id,
                comboIndex: comboIndex);
            return false;
        }

        _stepIndex = stepIndex;
        _comboIndex = comboIndex;
        _comboQueued = false;
        _queuedComboInputSequence = 0;
        _hitCount = 0;
        _attackStartTick = _currentTick;
        AttackInstanceId = _nextAttackInstanceId;
        _nextAttackInstanceId = _nextAttackInstanceId == ulong.MaxValue ? 1 : _nextAttackInstanceId + 1;
        var timeline = _steps[stepIndex].Timeline;
        Phase = timeline.Startup.DurationTicks > 0
            ? AttackRuntimePhase.Startup
            : AttackRuntimePhase.Active;
        _nextTransitionTick = AddSaturating(
            _attackStartTick,
            (ulong)(Phase == AttackRuntimePhase.Startup
                ? timeline.Startup.EndTickExclusive
                : timeline.Active.EndTickExclusive));
        _hasTransition = true;
        WriteEvent(
            events,
            AttackRuntimeEventKind.AttackStarted,
            inputSequence,
            AttackRuntimePhase.Idle,
            Phase,
            cost: _steps[stepIndex].Cost);
        if (Phase == AttackRuntimePhase.Active)
        {
            WriteEvent(events, AttackRuntimeEventKind.ActiveWindowOpened);
        }

        return true;
    }

    private void EnterCooldown(AttackRuntimePhase previous, AttackEventBuffer events)
    {
        ClearBufferedInput();
        _comboQueued = false;
        _queuedComboInputSequence = 0;
        _hitCount = 0;
        var cooldownTicks = _steps[_stepIndex].Timeline.CooldownTicks;
        if (cooldownTicks <= 0)
        {
            ChangePhase(AttackRuntimePhase.Idle, events, previous);
            _hasTransition = false;
            _stepIndex = -1;
            _comboIndex = 0;
            return;
        }

        ChangePhase(AttackRuntimePhase.Cooldown, events, previous);
        _cooldownUntilTick = AddSaturating(_currentTick, (ulong)cooldownTicks);
        _nextTransitionTick = _cooldownUntilTick;
        _hasTransition = true;
    }

    private void ChangePhase(AttackRuntimePhase next, AttackEventBuffer events, AttackRuntimePhase? previousOverride = null)
    {
        var previous = previousOverride ?? Phase;
        Phase = next;
        WriteEvent(events, AttackRuntimeEventKind.PhaseChanged, previousPhase: previous, phase: next);
    }

    private void RejectInput(AttackInputCommand command, AttackInputFailure failure, AttackEventBuffer events)
    {
        WriteEvent(events, AttackRuntimeEventKind.InputRejected, command.Sequence, failure: failure);
    }

    private void ClearBufferedInput()
    {
        _hasBufferedInput = false;
        _bufferedInput = default;
        _bufferActivationTick = 0;
        _bufferExpiryTick = 0;
    }

    private bool TryGetNextMilestone(ulong targetTick, out ulong milestone)
    {
        var found = false;
        milestone = ulong.MaxValue;
        if (_hasTransition && _nextTransitionTick <= targetTick)
        {
            milestone = _nextTransitionTick;
            found = true;
        }

        if (_hasBufferedInput && _bufferActivationTick <= targetTick && _bufferActivationTick < milestone)
        {
            milestone = _bufferActivationTick;
            found = true;
        }

        if (_commands.TryPeek(out var command) && command.Tick <= targetTick && command.Tick < milestone)
        {
            milestone = command.Tick;
            found = true;
        }

        return found;
    }

    private void WriteEvent(
        AttackEventBuffer events,
        AttackRuntimeEventKind kind,
        ulong inputSequence = 0,
        AttackRuntimePhase? previousPhase = null,
        AttackRuntimePhase? phase = null,
        AttackInputFailure failure = AttackInputFailure.None,
        int targetEntityId = 0,
        AttackResourceCost cost = default,
        string? attackId = null,
        int? comboIndex = null)
    {
        events.TryWrite(new AttackRuntimeEvent(
            kind,
            _currentTick,
            AttackInstanceId,
            inputSequence,
            attackId ?? (_stepIndex < 0 ? null : _steps[_stepIndex].Id),
            comboIndex ?? _comboIndex,
            previousPhase ?? Phase,
            phase ?? Phase,
            failure,
            targetEntityId,
            cost));
    }

    private int FindStepIndex(string? id)
    {
        if (id is null)
        {
            return -1;
        }

        for (var index = 0; index < _steps.Length; index++)
        {
            if (string.Equals(_steps[index].Id, id, StringComparison.Ordinal))
            {
                return index;
            }
        }

        throw new InvalidOperationException($"Unknown combo step '{id}'.");
    }

    private static ulong AddSaturating(ulong value, ulong offset) =>
        ulong.MaxValue - value < offset ? ulong.MaxValue : value + offset;

    private static int SaturateToInt(ulong value) => value > int.MaxValue ? int.MaxValue : (int)value;
}
