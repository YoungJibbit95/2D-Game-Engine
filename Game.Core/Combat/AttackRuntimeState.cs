namespace Game.Core.Combat;

public enum AttackCommandFailure
{
    None,
    AlreadyAttacking,
    NotAttacking,
    OutsideComboWindow,
    ComboAlreadyQueued,
    InvalidDefinition
}

public readonly record struct AttackCommandResult(
    bool Accepted,
    AttackCommandFailure Failure,
    ICombatEvent? Event = null)
{
    public static AttackCommandResult Rejected(AttackCommandFailure failure) => new(false, failure);
}

public readonly record struct AttackAdvanceResult(
    AttackPhase Phase,
    bool Completed,
    IReadOnlyList<ICombatEvent> Events)
{
    public static AttackAdvanceResult Idle { get; } = new(
        AttackPhase.Idle,
        false,
        Array.Empty<ICombatEvent>());
}

public sealed class AttackRuntimeState
{
    private readonly int[] _hitEntityIds;
    private int _hitEntityCount;
    private AttackDefinition? _definition;
    private AttackDefinition? _queuedCombo;
    private ulong _nextInstanceId;

    public AttackRuntimeState(ulong nextInstanceId = 1, int hitCapacity = 64)
    {
        if (nextInstanceId == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(nextInstanceId));
        }

        if (hitCapacity is <= 0 or > AttackSequenceDefinition.MaximumHitCapacity)
        {
            throw new ArgumentOutOfRangeException(nameof(hitCapacity));
        }

        _nextInstanceId = nextInstanceId;
        _hitEntityIds = new int[hitCapacity];
    }

    public AttackPhase Phase { get; private set; }

    public AttackDefinition? Definition => _definition;

    public ulong AttackInstanceId { get; private set; }

    public ulong NextInstanceId => _nextInstanceId;

    public int ComboIndex { get; private set; }

    public float ElapsedSeconds { get; private set; }

    public float PhaseElapsedSeconds { get; private set; }

    public bool IsAttacking => Phase != AttackPhase.Idle;

    public bool IsHitWindowOpen => Phase == AttackPhase.Active;

    public bool CanQueueCombo =>
        _definition?.ComboWindowStartSeconds is { } start &&
        _definition.ComboWindowEndSeconds is { } end &&
        ElapsedSeconds >= start &&
        ElapsedSeconds <= end;

    public AttackCommandResult TryStart(AttackDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (IsAttacking)
        {
            return AttackCommandResult.Rejected(AttackCommandFailure.AlreadyAttacking);
        }

        if (!TryValidate(definition))
        {
            return AttackCommandResult.Rejected(AttackCommandFailure.InvalidDefinition);
        }

        StartInternal(definition, comboIndex: 0);
        var started = new AttackStartedCombatEvent(
            AttackInstanceId,
            definition.Id,
            ComboIndex,
            Phase);
        return new AttackCommandResult(true, AttackCommandFailure.None, started);
    }

    public AttackCommandResult TryQueueCombo(AttackDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (!IsAttacking)
        {
            return AttackCommandResult.Rejected(AttackCommandFailure.NotAttacking);
        }

        if (_queuedCombo is not null)
        {
            return AttackCommandResult.Rejected(AttackCommandFailure.ComboAlreadyQueued);
        }

        if (!CanQueueCombo)
        {
            return AttackCommandResult.Rejected(AttackCommandFailure.OutsideComboWindow);
        }

        if (!TryValidate(definition))
        {
            return AttackCommandResult.Rejected(AttackCommandFailure.InvalidDefinition);
        }

        _queuedCombo = definition;
        var queued = new AttackComboQueuedCombatEvent(
            AttackInstanceId,
            _definition!.Id,
            definition.Id,
            ComboIndex);
        return new AttackCommandResult(true, AttackCommandFailure.None, queued);
    }

    public AttackAdvanceResult Advance(float deltaSeconds)
    {
        if (!float.IsFinite(deltaSeconds) || deltaSeconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
        }

        if (!IsAttacking || deltaSeconds == 0)
        {
            return IsAttacking
                ? new AttackAdvanceResult(Phase, false, Array.Empty<ICombatEvent>())
                : AttackAdvanceResult.Idle;
        }

        var events = new List<ICombatEvent>(4);
        var remaining = deltaSeconds;
        var completed = false;
        while (remaining > 0 && IsAttacking)
        {
            var phaseRemaining = GetCurrentPhaseDuration() - PhaseElapsedSeconds;
            var consumed = Math.Min(remaining, Math.Max(0, phaseRemaining));
            PhaseElapsedSeconds += consumed;
            ElapsedSeconds += consumed;
            remaining -= consumed;

            if (phaseRemaining > consumed)
            {
                break;
            }

            completed |= TransitionPhase(events);
        }

        return new AttackAdvanceResult(Phase, completed, events);
    }

    public bool TryRegisterHit(int targetEntityId)
    {
        if (targetEntityId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(targetEntityId));
        }

        if (!IsHitWindowOpen)
        {
            return false;
        }

        for (var index = 0; index < _hitEntityCount; index++)
        {
            if (_hitEntityIds[index] == targetEntityId)
            {
                return false;
            }
        }

        if (_hitEntityCount == _hitEntityIds.Length)
        {
            return false;
        }

        _hitEntityIds[_hitEntityCount++] = targetEntityId;
        return true;
    }

    private void StartInternal(AttackDefinition definition, int comboIndex)
    {
        _definition = definition;
        _queuedCombo = null;
        AttackInstanceId = _nextInstanceId++;
        ComboIndex = comboIndex;
        ElapsedSeconds = 0;
        PhaseElapsedSeconds = 0;
        _hitEntityCount = 0;
        Phase = definition.WindupSeconds > 0 ? AttackPhase.Windup : AttackPhase.Active;
    }

    private bool TransitionPhase(List<ICombatEvent> events)
    {
        var previous = Phase;
        PhaseElapsedSeconds = 0;
        switch (Phase)
        {
            case AttackPhase.Windup:
                Phase = AttackPhase.Active;
                AddPhaseChanged(events, previous);
                return false;
            case AttackPhase.Active:
                Phase = AttackPhase.Recovery;
                AddPhaseChanged(events, previous);
                if (_definition!.RecoverySeconds <= 0)
                {
                    return TransitionPhase(events);
                }

                return false;
            case AttackPhase.Recovery:
                return CompleteOrStartCombo(events);
            default:
                return false;
        }
    }

    private bool CompleteOrStartCombo(List<ICombatEvent> events)
    {
        var completedDefinition = _definition!;
        var completedInstanceId = AttackInstanceId;
        var completedComboIndex = ComboIndex;
        events.Add(new AttackCompletedCombatEvent(
            completedInstanceId,
            completedDefinition.Id,
            completedComboIndex));

        if (_queuedCombo is { } queued)
        {
            StartInternal(queued, completedComboIndex + 1);
            events.Add(new AttackStartedCombatEvent(
                AttackInstanceId,
                queued.Id,
                ComboIndex,
                Phase));
            return true;
        }

        Phase = AttackPhase.Idle;
        _definition = null;
        _hitEntityCount = 0;
        return true;
    }

    private void AddPhaseChanged(List<ICombatEvent> events, AttackPhase previous)
    {
        events.Add(new AttackPhaseChangedCombatEvent(
            AttackInstanceId,
            _definition!.Id,
            ComboIndex,
            previous,
            Phase));
    }

    private float GetCurrentPhaseDuration()
    {
        return Phase switch
        {
            AttackPhase.Windup => _definition!.WindupSeconds,
            AttackPhase.Active => _definition!.ActiveSeconds,
            AttackPhase.Recovery => _definition!.RecoverySeconds,
            _ => 0
        };
    }

    private static bool TryValidate(AttackDefinition definition)
    {
        try
        {
            definition.Validate();
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return false;
        }
    }
}
