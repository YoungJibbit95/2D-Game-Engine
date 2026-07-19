using System.Numerics;

namespace Game.Core.Combat;

public enum AttackRuntimePhase
{
    Idle,
    Startup,
    Active,
    Recovery,
    Cooldown
}

public enum AttackInputKind
{
    Attack,
    Cancel
}

public enum AttackInputFailure
{
    None,
    BufferFull,
    OutOfOrder,
    SequenceExhausted,
    LockedOut,
    AlreadyBuffered,
    OutsideComboWindow,
    CancelNotAllowed,
    InsufficientStamina,
    InsufficientMana,
    InsufficientAmmo,
    HitWindowClosed,
    DuplicateTarget,
    HitCapacityReached
}

public enum AttackRuntimeEventKind
{
    InputBuffered,
    InputRejected,
    AttackStarted,
    PhaseChanged,
    ActiveWindowOpened,
    ActiveWindowClosed,
    HitAccepted,
    HitRejectedDuplicate,
    HitRejected,
    ComboQueued,
    ComboAdvanced,
    ComboReset,
    ComboCompleted,
    AttackCompleted,
    AttackCancelled
}

public readonly record struct AttackResourceCost(
    float Stamina = 0,
    int Mana = 0,
    int Ammo = 0,
    string? AmmoItemId = null)
{
    public bool IsFree => Stamina == 0 && Mana == 0 && Ammo == 0;

    public void Validate()
    {
        if (!float.IsFinite(Stamina) || Stamina < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Stamina));
        }

        if (Mana < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Mana));
        }

        if (Ammo < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Ammo));
        }

        if (Ammo > 0 && string.IsNullOrWhiteSpace(AmmoItemId))
        {
            throw new InvalidOperationException("An ammo item id is required when an attack consumes ammo.");
        }

        if (Ammo == 0 && AmmoItemId is not null)
        {
            throw new InvalidOperationException("An ammo item id cannot be specified for a zero-ammo attack.");
        }
    }
}

public readonly record struct AttackPhaseWindow(int StartTickInclusive, int EndTickExclusive)
{
    public int DurationTicks => EndTickExclusive - StartTickInclusive;

    public bool Contains(int relativeTick) =>
        relativeTick >= StartTickInclusive && relativeTick < EndTickExclusive;

    public void Validate(string parameterName, bool allowEmpty = true)
    {
        if (StartTickInclusive < 0 || EndTickExclusive < StartTickInclusive ||
            (!allowEmpty && EndTickExclusive == StartTickInclusive))
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    public bool Overlaps(AttackPhaseWindow other) =>
        StartTickInclusive < other.EndTickExclusive && other.StartTickInclusive < EndTickExclusive;
}

public sealed record AttackTimelineDefinition
{
    public required AttackPhaseWindow Startup { get; init; }

    public required AttackPhaseWindow Active { get; init; }

    public required AttackPhaseWindow Recovery { get; init; }

    public int CooldownTicks { get; init; }

    public AttackPhaseWindow? ComboWindow { get; init; }

    public int TotalActionTicks => Recovery.EndTickExclusive;

    public static AttackTimelineDefinition Create(
        int startupTicks,
        int activeTicks,
        int recoveryTicks,
        int cooldownTicks = 0,
        AttackPhaseWindow? comboWindow = null)
    {
        if (startupTicks < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(startupTicks));
        }

        if (activeTicks <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(activeTicks));
        }

        if (recoveryTicks < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(recoveryTicks));
        }

        return new AttackTimelineDefinition
        {
            Startup = new AttackPhaseWindow(0, startupTicks),
            Active = new AttackPhaseWindow(startupTicks, checked(startupTicks + activeTicks)),
            Recovery = new AttackPhaseWindow(
                checked(startupTicks + activeTicks),
                checked(startupTicks + activeTicks + recoveryTicks)),
            CooldownTicks = cooldownTicks,
            ComboWindow = comboWindow
        };
    }

    public void Validate()
    {
        Startup.Validate(nameof(Startup));
        Active.Validate(nameof(Active), allowEmpty: false);
        Recovery.Validate(nameof(Recovery));
        if (CooldownTicks < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(CooldownTicks));
        }

        if (Startup.StartTickInclusive != 0 || Startup.EndTickExclusive != Active.StartTickInclusive ||
            Active.EndTickExclusive != Recovery.StartTickInclusive)
        {
            throw new InvalidOperationException("Startup, active and recovery phases must be contiguous and ordered.");
        }

        if (Startup.Overlaps(Active) || Startup.Overlaps(Recovery) || Active.Overlaps(Recovery))
        {
            throw new InvalidOperationException("Attack phase windows cannot overlap.");
        }

        if (ComboWindow is not { } comboWindow)
        {
            return;
        }

        comboWindow.Validate(nameof(ComboWindow), allowEmpty: false);
        if (comboWindow.EndTickExclusive > TotalActionTicks)
        {
            throw new InvalidOperationException("The combo window must be contained in the action timeline.");
        }
    }
}

public sealed record AttackCancelRuleDefinition
{
    public AttackRuntimePhase Phase { get; init; }

    public required AttackPhaseWindow Window { get; init; }

    public void Validate(AttackTimelineDefinition timeline)
    {
        ArgumentNullException.ThrowIfNull(timeline);
        if (Phase is not (AttackRuntimePhase.Startup or AttackRuntimePhase.Active or AttackRuntimePhase.Recovery))
        {
            throw new InvalidOperationException("Cancel rules can only target startup, active or recovery.");
        }

        Window.Validate(nameof(Window), allowEmpty: false);
        var phaseDuration = Phase switch
        {
            AttackRuntimePhase.Startup => timeline.Startup.DurationTicks,
            AttackRuntimePhase.Active => timeline.Active.DurationTicks,
            _ => timeline.Recovery.DurationTicks
        };
        if (Window.EndTickExclusive > phaseDuration)
        {
            throw new InvalidOperationException("A cancel window must be contained in its phase.");
        }
    }
}

public sealed record SweptMeleeShapeDefinition
{
    public required string Id { get; init; }

    public required MeleeSweepDefinition Sweep { get; init; }

    public int ActiveStartTickInclusive { get; init; }

    public required int ActiveEndTickExclusive { get; init; }

    public Vector2 OriginOffset { get; init; }

    public void Validate(int activeDurationTicks)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Id);
        ArgumentNullException.ThrowIfNull(Sweep);
        Sweep.Validate();
        if (ActiveStartTickInclusive < 0 || ActiveEndTickExclusive <= ActiveStartTickInclusive ||
            ActiveEndTickExclusive > activeDurationTicks)
        {
            throw new InvalidOperationException("Swept melee shapes must use a non-empty window inside the active phase.");
        }

        if (!float.IsFinite(OriginOffset.X) || !float.IsFinite(OriginOffset.Y))
        {
            throw new ArgumentOutOfRangeException(nameof(OriginOffset));
        }
    }
}

public sealed record AttackComboStepDefinition
{
    public required string Id { get; init; }

    public required AttackTimelineDefinition Timeline { get; init; }

    public string? NextStepId { get; init; }

    public AttackResourceCost Cost { get; init; }

    public int MaxTargetsPerSwing { get; init; } = 32;

    public IReadOnlyList<AttackCancelRuleDefinition> CancelRules { get; init; } =
        Array.Empty<AttackCancelRuleDefinition>();

    public IReadOnlyList<SweptMeleeShapeDefinition> MeleeShapes { get; init; } =
        Array.Empty<SweptMeleeShapeDefinition>();
}

public sealed record AttackSequenceDefinition
{
    public const int MaximumCommandCapacity = 256;
    public const int MaximumEventCapacity = 1024;
    public const int MaximumHitCapacity = 1024;
    public const int MaximumComboSteps = 64;

    public required string Id { get; init; }

    public required IReadOnlyList<AttackComboStepDefinition> Steps { get; init; }

    public int InputBufferTicks { get; init; } = 6;

    public int CommandCapacity { get; init; } = 16;

    public int EventCapacity { get; init; } = 64;

    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Id);
        ArgumentNullException.ThrowIfNull(Steps);
        if (Steps.Count is <= 0 or > MaximumComboSteps)
        {
            throw new ArgumentOutOfRangeException(nameof(Steps));
        }

        ValidateCapacity(InputBufferTicks, 0, int.MaxValue, nameof(InputBufferTicks));
        ValidateCapacity(CommandCapacity, 1, MaximumCommandCapacity, nameof(CommandCapacity));
        ValidateCapacity(EventCapacity, 1, MaximumEventCapacity, nameof(EventCapacity));

        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < Steps.Count; index++)
        {
            var step = Steps[index] ?? throw new InvalidOperationException("Attack combo steps cannot contain null entries.");
            ArgumentException.ThrowIfNullOrWhiteSpace(step.Id);
            if (!ids.Add(step.Id))
            {
                throw new InvalidOperationException($"Duplicate attack combo step id '{step.Id}'.");
            }

            ValidateStep(step);
        }

        ValidateChain(ids);
    }

    private static void ValidateStep(AttackComboStepDefinition step)
    {
        ArgumentNullException.ThrowIfNull(step.Timeline);
        step.Timeline.Validate();
        step.Cost.Validate();
        ValidateCapacity(step.MaxTargetsPerSwing, 1, MaximumHitCapacity, nameof(step.MaxTargetsPerSwing));
        ArgumentNullException.ThrowIfNull(step.CancelRules);
        ArgumentNullException.ThrowIfNull(step.MeleeShapes);

        for (var index = 0; index < step.CancelRules.Count; index++)
        {
            var rule = step.CancelRules[index] ?? throw new InvalidOperationException("Cancel rules cannot contain null entries.");
            rule.Validate(step.Timeline);
            for (var previous = 0; previous < index; previous++)
            {
                var other = step.CancelRules[previous];
                if (other.Phase == rule.Phase && other.Window.Overlaps(rule.Window))
                {
                    throw new InvalidOperationException("Cancel windows for the same phase cannot overlap.");
                }
            }
        }

        var shapeIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var shape in step.MeleeShapes)
        {
            if (shape is null)
            {
                throw new InvalidOperationException("Melee shapes cannot contain null entries.");
            }

            shape.Validate(step.Timeline.Active.DurationTicks);
            if (!shapeIds.Add(shape.Id))
            {
                throw new InvalidOperationException($"Duplicate melee shape id '{shape.Id}'.");
            }
        }
    }

    private void ValidateChain(HashSet<string> ids)
    {
        foreach (var step in Steps)
        {
            if (step.NextStepId is not null && !ids.Contains(step.NextStepId))
            {
                throw new InvalidOperationException($"Unknown next combo step '{step.NextStepId}'.");
            }

            if (step.NextStepId is not null && step.Timeline.ComboWindow is null)
            {
                throw new InvalidOperationException("A combo step with a successor requires a combo window.");
            }

            if (step.NextStepId is null && step.Timeline.ComboWindow is not null)
            {
                throw new InvalidOperationException("A terminal combo step cannot define a combo window.");
            }
        }

        var visited = new HashSet<string>(StringComparer.Ordinal);
        var current = Steps[0];
        while (visited.Add(current.Id) && current.NextStepId is { } nextId)
        {
            current = FindStep(nextId);
        }

        if (current.NextStepId is not null)
        {
            throw new InvalidOperationException("Attack combo chains cannot contain cycles.");
        }

        if (visited.Count != Steps.Count)
        {
            throw new InvalidOperationException("Every combo step must be reachable from the first step.");
        }
    }

    private AttackComboStepDefinition FindStep(string id)
    {
        for (var index = 0; index < Steps.Count; index++)
        {
            if (string.Equals(Steps[index].Id, id, StringComparison.Ordinal))
            {
                return Steps[index];
            }
        }

        throw new InvalidOperationException($"Unknown attack step '{id}'.");
    }

    private static void ValidateCapacity(int value, int minimum, int maximum, string name)
    {
        if (value < minimum || value > maximum)
        {
            throw new ArgumentOutOfRangeException(name);
        }
    }
}

public readonly record struct AttackInputCommand(
    ulong Tick,
    ulong Sequence,
    AttackInputKind Kind);

public readonly record struct AttackStartRequest(
    ulong Tick,
    ulong InputSequence,
    string SequenceId,
    string AttackId,
    int ComboIndex,
    AttackResourceCost Cost);

public interface IAttackStartGate
{
    AttackInputFailure TryAccept(in AttackStartRequest request);
}

public readonly record struct AttackRuntimeEvent(
    AttackRuntimeEventKind Kind,
    ulong Tick,
    ulong AttackInstanceId,
    ulong InputSequence,
    string? AttackId,
    int ComboIndex,
    AttackRuntimePhase PreviousPhase,
    AttackRuntimePhase Phase,
    AttackInputFailure Failure = AttackInputFailure.None,
    int TargetEntityId = 0,
    AttackResourceCost Cost = default);

public readonly record struct AttackInputResult(
    bool Accepted,
    AttackInputFailure Failure,
    AttackInputCommand Command)
{
    public static AttackInputResult Rejected(AttackInputFailure failure) => new(false, failure, default);
}

public readonly record struct AttackHitRegistrationResult(
    bool Accepted,
    AttackInputFailure Failure,
    ulong AttackInstanceId,
    int TargetEntityId);

public readonly record struct AttackSequencerAdvanceResult(
    ulong Tick,
    AttackRuntimePhase Phase,
    int EventsWritten,
    int EventsDropped,
    bool StateChanged);
