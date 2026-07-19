namespace Game.Client.Rendering.Performance;

[Flags]
public enum PresentationWorkTrigger
{
    None = 0,
    Periodic = 1 << 0,
    Dirty = 1 << 1,
    Revision = 1 << 2,
    CameraTranslation = 1 << 3,
    CameraZoom = 1 << 4
}

[Flags]
public enum PresentationWorkReason
{
    None = 0,
    Initial = 1 << 0,
    ImmediateRequest = 1 << 1,
    Periodic = 1 << 2,
    Dirty = 1 << 3,
    Revision = 1 << 4,
    CameraTranslation = 1 << 5,
    CameraZoom = 1 << 6,
    TimeStarvation = 1 << 7,
    FrameStarvation = 1 << 8
}

public readonly record struct PresentationWorkSchedule(
    double TargetHz,
    double MaximumStalenessSeconds,
    int MaximumDeferredFrames,
    PresentationWorkTrigger Triggers,
    long MinimumRevisionDelta = 1,
    double CameraTranslationThreshold = 0,
    double CameraZoomThreshold = 0)
{
    private const PresentationWorkTrigger AllTriggers =
        PresentationWorkTrigger.Periodic |
        PresentationWorkTrigger.Dirty |
        PresentationWorkTrigger.Revision |
        PresentationWorkTrigger.CameraTranslation |
        PresentationWorkTrigger.CameraZoom;

    public double MinimumIntervalSeconds => 1d / TargetHz;

    internal void Validate()
    {
        if (!double.IsFinite(TargetHz) || TargetHz <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(TargetHz), "Target frequency must be finite and positive.");
        }

        if (!double.IsFinite(MaximumStalenessSeconds) ||
            MaximumStalenessSeconds < MinimumIntervalSeconds)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaximumStalenessSeconds),
                "Maximum staleness must be finite and at least one target interval.");
        }

        if (MaximumDeferredFrames < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaximumDeferredFrames),
                "At least one deferred frame must be allowed.");
        }

        if ((Triggers & ~AllTriggers) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Triggers), "The trigger mask contains unsupported values.");
        }

        if (MinimumRevisionDelta < 0 ||
            (Triggers & PresentationWorkTrigger.Revision) != 0 && MinimumRevisionDelta < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MinimumRevisionDelta),
                "Revision-triggered work requires a positive revision delta.");
        }

        if (!double.IsFinite(CameraTranslationThreshold) || CameraTranslationThreshold < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(CameraTranslationThreshold),
                "Camera translation threshold must be finite and non-negative.");
        }

        if (!double.IsFinite(CameraZoomThreshold) || CameraZoomThreshold < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(CameraZoomThreshold),
                "Camera zoom threshold must be finite and non-negative.");
        }
    }
}

public readonly record struct PresentationWorkState(
    long Revision,
    double CameraX,
    double CameraY,
    double CameraZoom,
    bool IsDirty = false,
    bool IsEnabled = true);

public readonly record struct PresentationWorkHandle(int Value)
{
    public bool IsValid => Value > 0;

    internal int Index => Value - 1;
}

public readonly record struct PresentationWorkDecision(
    PresentationWorkReason Reasons,
    long FrameIndex,
    double SecondsSinceLastRun,
    long DeferredFrames)
{
    public bool ShouldRun => Reasons != PresentationWorkReason.None;
}

public readonly record struct PresentationWorkSchedulerTelemetry(
    long EvaluationCount,
    long ScheduledCount,
    long SkippedCount,
    long DisabledCount,
    long InitialScheduleCount,
    long ImmediateScheduleCount,
    long PeriodicScheduleCount,
    long DirtyScheduleCount,
    long RevisionScheduleCount,
    long CameraScheduleCount,
    long StarvationScheduleCount,
    long MaximumObservedDeferredFrames,
    double MaximumObservedStalenessSeconds,
    long LastScheduledFrame,
    double LastScheduledAtSeconds,
    PresentationWorkReason LastReasons);

public readonly record struct PresentationFrameBudgetTelemetry(
    int MaximumUnits,
    int ConsumedUnits,
    int AdmittedWorkCount,
    int DeferredWorkCount,
    int ForcedOverBudgetCount)
{
    public int RemainingUnits => Math.Max(0, MaximumUnits - ConsumedUnits);
}

/// <summary>
/// Reusable admission budget for expensive presentation preparation. The scheduler may exceed the
/// budget only for initial, explicitly requested or starvation-protected work so visual state can
/// never remain stale indefinitely.
/// </summary>
public sealed class PresentationFrameBudget
{
    private int _maximumUnits;
    private int _consumedUnits;
    private int _admittedWorkCount;
    private int _deferredWorkCount;
    private int _forcedOverBudgetCount;

    public PresentationFrameBudget(int maximumUnits)
    {
        Reset(maximumUnits);
    }

    public void Reset(int maximumUnits)
    {
        if (maximumUnits < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumUnits));
        }

        _maximumUnits = maximumUnits;
        _consumedUnits = 0;
        _admittedWorkCount = 0;
        _deferredWorkCount = 0;
        _forcedOverBudgetCount = 0;
    }

    public PresentationFrameBudgetTelemetry CaptureTelemetry()
    {
        return new PresentationFrameBudgetTelemetry(
            _maximumUnits,
            _consumedUnits,
            _admittedWorkCount,
            _deferredWorkCount,
            _forcedOverBudgetCount);
    }

    internal bool TryAdmit(int workUnits, bool force)
    {
        if (workUnits < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(workUnits));
        }

        var fits = workUnits <= Math.Max(0, _maximumUnits - _consumedUnits);
        if (!fits && !force)
        {
            _deferredWorkCount++;
            return false;
        }

        if (!fits)
        {
            _forcedOverBudgetCount++;
        }

        _consumedUnits = SaturatingAdd(_consumedUnits, workUnits);
        _admittedWorkCount++;
        return true;
    }

    private static int SaturatingAdd(int left, int right)
    {
        return left > int.MaxValue - right ? int.MaxValue : left + right;
    }
}

/// <summary>
/// Selects stale-tolerant presentation work without sleeping, blocking, or changing simulation cadence.
/// Registering and configuration are setup operations; AdvanceFrame and TrySchedule allocate no managed memory.
/// </summary>
public sealed class PresentationWorkScheduler
{
    private const double CadenceToleranceSeconds = 1e-12;
    private readonly WorkSlot[] _slots;
    private int _registeredCount;
    private long _frameIndex;
    private double _elapsedSeconds;

    public PresentationWorkScheduler(int capacity = 8)
    {
        if (capacity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        _slots = new WorkSlot[capacity];
    }

    public int Capacity => _slots.Length;

    public int RegisteredCount => _registeredCount;

    public long FrameIndex => _frameIndex;

    public double ElapsedSeconds => _elapsedSeconds;

    public PresentationWorkHandle Register(PresentationWorkSchedule schedule)
    {
        schedule.Validate();
        if (_registeredCount == _slots.Length)
        {
            throw new InvalidOperationException("The presentation scheduler has no free work slots.");
        }

        var index = _registeredCount++;
        _slots[index].Register(schedule, _elapsedSeconds);
        return new PresentationWorkHandle(index + 1);
    }

    public void Configure(
        PresentationWorkHandle handle,
        PresentationWorkSchedule schedule,
        bool requestImmediate = false)
    {
        schedule.Validate();
        ref var slot = ref GetSlot(handle);
        slot.Schedule = schedule;
        slot.NextEligibleSeconds = _elapsedSeconds + schedule.MinimumIntervalSeconds;
        slot.ImmediateRequested |= requestImmediate;
    }

    public void AdvanceFrame(double deltaSeconds)
    {
        if (!double.IsFinite(deltaSeconds) || deltaSeconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(deltaSeconds), "Frame delta must be finite and non-negative.");
        }

        _elapsedSeconds += deltaSeconds;
        if (_frameIndex < long.MaxValue)
        {
            _frameIndex++;
        }
    }

    public void Invalidate(PresentationWorkHandle handle)
    {
        GetSlot(handle).DirtyPending = true;
    }

    public void RequestImmediate(PresentationWorkHandle handle)
    {
        GetSlot(handle).ImmediateRequested = true;
    }

    public bool TrySchedule(
        PresentationWorkHandle handle,
        in PresentationWorkState state,
        out PresentationWorkDecision decision)
    {
        return TryScheduleCore(handle, state, 0, null, out decision);
    }

    public bool TrySchedule(
        PresentationWorkHandle handle,
        in PresentationWorkState state,
        int estimatedWorkUnits,
        PresentationFrameBudget frameBudget,
        out PresentationWorkDecision decision)
    {
        ArgumentNullException.ThrowIfNull(frameBudget);
        if (estimatedWorkUnits < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(estimatedWorkUnits));
        }

        return TryScheduleCore(handle, state, estimatedWorkUnits, frameBudget, out decision);
    }

    private bool TryScheduleCore(
        PresentationWorkHandle handle,
        in PresentationWorkState state,
        int estimatedWorkUnits,
        PresentationFrameBudget? frameBudget,
        out PresentationWorkDecision decision)
    {
        ref var slot = ref GetSlot(handle);
        slot.EvaluationCount++;
        slot.DirtyPending |= state.IsDirty;

        var secondsSinceLastRun = slot.HasRun
            ? Math.Max(0, _elapsedSeconds - slot.LastScheduledAtSeconds)
            : 0;
        var deferredFrames = slot.HasRun
            ? Math.Max(0, _frameIndex - slot.LastScheduledFrame)
            : 0;
        slot.ObserveDeferral(secondsSinceLastRun, deferredFrames);

        if (!state.IsEnabled)
        {
            slot.SkippedCount++;
            slot.DisabledCount++;
            decision = new PresentationWorkDecision(
                PresentationWorkReason.None,
                _frameIndex,
                secondsSinceLastRun,
                deferredFrames);
            return false;
        }

        ValidateState(slot.Schedule, state);

        var reasons = PresentationWorkReason.None;
        if (!slot.HasRun)
        {
            reasons = PresentationWorkReason.Initial;
        }
        else if (slot.ImmediateRequested)
        {
            reasons = PresentationWorkReason.ImmediateRequest;
        }
        else
        {
            if (secondsSinceLastRun + CadenceToleranceSeconds >= slot.Schedule.MaximumStalenessSeconds)
            {
                reasons |= PresentationWorkReason.TimeStarvation;
            }

            if (deferredFrames >= slot.Schedule.MaximumDeferredFrames)
            {
                reasons |= PresentationWorkReason.FrameStarvation;
            }

            if (reasons == PresentationWorkReason.None && IsCadenceReady(slot))
            {
                reasons = EvaluateTriggers(slot, state);
            }
        }

        if (reasons == PresentationWorkReason.None)
        {
            slot.SkippedCount++;
            decision = new PresentationWorkDecision(
                PresentationWorkReason.None,
                _frameIndex,
                secondsSinceLastRun,
                deferredFrames);
            return false;
        }

        var forceAdmission =
            (reasons & (
                PresentationWorkReason.Initial |
                PresentationWorkReason.ImmediateRequest |
                PresentationWorkReason.TimeStarvation |
                PresentationWorkReason.FrameStarvation)) != 0;
        if (frameBudget is not null &&
            !frameBudget.TryAdmit(estimatedWorkUnits, forceAdmission))
        {
            slot.SkippedCount++;
            decision = new PresentationWorkDecision(
                PresentationWorkReason.None,
                _frameIndex,
                secondsSinceLastRun,
                deferredFrames);
            return false;
        }

        var preserveCadence =
            slot.HasRun &&
            !slot.ImmediateRequested &&
            (reasons & (PresentationWorkReason.TimeStarvation | PresentationWorkReason.FrameStarvation)) == 0 &&
            IsCadenceReady(slot);
        CommitSchedule(ref slot, state, reasons, preserveCadence);
        decision = new PresentationWorkDecision(reasons, _frameIndex, secondsSinceLastRun, deferredFrames);
        return true;
    }

    public PresentationWorkSchedulerTelemetry CaptureTelemetry(PresentationWorkHandle handle)
    {
        ref var slot = ref GetSlot(handle);
        return slot.CaptureTelemetry();
    }

    public void Reset()
    {
        _frameIndex = 0;
        _elapsedSeconds = 0;
        for (var index = 0; index < _registeredCount; index++)
        {
            _slots[index].ResetRuntime();
        }
    }

    private static void ValidateState(PresentationWorkSchedule schedule, in PresentationWorkState state)
    {
        if ((schedule.Triggers & PresentationWorkTrigger.CameraTranslation) != 0 &&
            (!double.IsFinite(state.CameraX) || !double.IsFinite(state.CameraY)))
        {
            throw new ArgumentOutOfRangeException(nameof(state), "Camera position must be finite.");
        }

        if ((schedule.Triggers & PresentationWorkTrigger.CameraZoom) != 0 &&
            (!double.IsFinite(state.CameraZoom) || state.CameraZoom <= 0))
        {
            throw new ArgumentOutOfRangeException(nameof(state), "Camera zoom must be finite and positive.");
        }
    }

    private bool IsCadenceReady(in WorkSlot slot)
    {
        return _elapsedSeconds + CadenceToleranceSeconds >= slot.NextEligibleSeconds;
    }

    private static PresentationWorkReason EvaluateTriggers(in WorkSlot slot, in PresentationWorkState state)
    {
        var reasons = PresentationWorkReason.None;
        var triggers = slot.Schedule.Triggers;
        if ((triggers & PresentationWorkTrigger.Periodic) != 0)
        {
            reasons |= PresentationWorkReason.Periodic;
        }

        if ((triggers & PresentationWorkTrigger.Dirty) != 0 && slot.DirtyPending)
        {
            reasons |= PresentationWorkReason.Dirty;
        }

        if ((triggers & PresentationWorkTrigger.Revision) != 0 &&
            RevisionDelta(state.Revision, slot.LastState.Revision) >= (ulong)slot.Schedule.MinimumRevisionDelta)
        {
            reasons |= PresentationWorkReason.Revision;
        }

        if ((triggers & PresentationWorkTrigger.CameraTranslation) != 0 &&
            CameraMoved(slot.LastState, state, slot.Schedule.CameraTranslationThreshold))
        {
            reasons |= PresentationWorkReason.CameraTranslation;
        }

        if ((triggers & PresentationWorkTrigger.CameraZoom) != 0 &&
            Math.Abs(state.CameraZoom - slot.LastState.CameraZoom) >= slot.Schedule.CameraZoomThreshold &&
            state.CameraZoom != slot.LastState.CameraZoom)
        {
            reasons |= PresentationWorkReason.CameraZoom;
        }

        return reasons;
    }

    private void CommitSchedule(
        ref WorkSlot slot,
        in PresentationWorkState state,
        PresentationWorkReason reasons,
        bool preserveCadence)
    {
        var interval = slot.Schedule.MinimumIntervalSeconds;
        if (preserveCadence && slot.NextEligibleSeconds + interval > _elapsedSeconds + CadenceToleranceSeconds)
        {
            slot.NextEligibleSeconds += interval;
        }
        else
        {
            slot.NextEligibleSeconds = _elapsedSeconds + interval;
        }

        slot.HasRun = true;
        slot.DirtyPending = false;
        slot.ImmediateRequested = false;
        slot.LastState = state;
        slot.LastScheduledFrame = _frameIndex;
        slot.LastScheduledAtSeconds = _elapsedSeconds;
        slot.LastReasons = reasons;
        slot.RecordSchedule(reasons);
    }

    private static ulong RevisionDelta(long current, long previous)
    {
        return current >= previous
            ? unchecked((ulong)current - (ulong)previous)
            : unchecked((ulong)previous - (ulong)current);
    }

    private static bool CameraMoved(
        in PresentationWorkState previous,
        in PresentationWorkState current,
        double threshold)
    {
        var deltaX = current.CameraX - previous.CameraX;
        var deltaY = current.CameraY - previous.CameraY;
        if (deltaX == 0 && deltaY == 0)
        {
            return false;
        }

        if (threshold == 0)
        {
            return true;
        }

        var absoluteX = Math.Abs(deltaX);
        var absoluteY = Math.Abs(deltaY);
        var maximum = Math.Max(absoluteX, absoluteY);
        var minimum = Math.Min(absoluteX, absoluteY);
        if (double.IsInfinity(maximum))
        {
            return true;
        }

        var ratio = minimum / maximum;
        var distance = maximum * Math.Sqrt(1 + (ratio * ratio));
        return distance >= threshold;
    }

    private ref WorkSlot GetSlot(PresentationWorkHandle handle)
    {
        if (!handle.IsValid || handle.Index >= _registeredCount)
        {
            throw new ArgumentOutOfRangeException(nameof(handle), "The work handle is not registered with this scheduler.");
        }

        return ref _slots[handle.Index];
    }

    private struct WorkSlot
    {
        public bool HasRun;
        public bool DirtyPending;
        public bool ImmediateRequested;
        public PresentationWorkSchedule Schedule;
        public PresentationWorkState LastState;
        public double NextEligibleSeconds;
        public long LastScheduledFrame;
        public double LastScheduledAtSeconds;
        public PresentationWorkReason LastReasons;
        public long EvaluationCount;
        public long ScheduledCount;
        public long SkippedCount;
        public long DisabledCount;
        public long InitialScheduleCount;
        public long ImmediateScheduleCount;
        public long PeriodicScheduleCount;
        public long DirtyScheduleCount;
        public long RevisionScheduleCount;
        public long CameraScheduleCount;
        public long StarvationScheduleCount;
        public long MaximumObservedDeferredFrames;
        public double MaximumObservedStalenessSeconds;

        public void Register(PresentationWorkSchedule schedule, double elapsedSeconds)
        {
            this = default;
            Schedule = schedule;
            NextEligibleSeconds = elapsedSeconds;
        }

        public void ObserveDeferral(double stalenessSeconds, long deferredFrames)
        {
            MaximumObservedStalenessSeconds = Math.Max(MaximumObservedStalenessSeconds, stalenessSeconds);
            MaximumObservedDeferredFrames = Math.Max(MaximumObservedDeferredFrames, deferredFrames);
        }

        public void RecordSchedule(PresentationWorkReason reasons)
        {
            ScheduledCount++;
            if ((reasons & PresentationWorkReason.Initial) != 0)
            {
                InitialScheduleCount++;
            }

            if ((reasons & PresentationWorkReason.ImmediateRequest) != 0)
            {
                ImmediateScheduleCount++;
            }

            if ((reasons & PresentationWorkReason.Periodic) != 0)
            {
                PeriodicScheduleCount++;
            }

            if ((reasons & PresentationWorkReason.Dirty) != 0)
            {
                DirtyScheduleCount++;
            }

            if ((reasons & PresentationWorkReason.Revision) != 0)
            {
                RevisionScheduleCount++;
            }

            if ((reasons & (PresentationWorkReason.CameraTranslation | PresentationWorkReason.CameraZoom)) != 0)
            {
                CameraScheduleCount++;
            }

            if ((reasons & (PresentationWorkReason.TimeStarvation | PresentationWorkReason.FrameStarvation)) != 0)
            {
                StarvationScheduleCount++;
            }
        }

        public PresentationWorkSchedulerTelemetry CaptureTelemetry()
        {
            return new PresentationWorkSchedulerTelemetry(
                EvaluationCount,
                ScheduledCount,
                SkippedCount,
                DisabledCount,
                InitialScheduleCount,
                ImmediateScheduleCount,
                PeriodicScheduleCount,
                DirtyScheduleCount,
                RevisionScheduleCount,
                CameraScheduleCount,
                StarvationScheduleCount,
                MaximumObservedDeferredFrames,
                MaximumObservedStalenessSeconds,
                LastScheduledFrame,
                LastScheduledAtSeconds,
                LastReasons);
        }

        public void ResetRuntime()
        {
            var schedule = Schedule;
            Register(schedule, 0);
        }
    }
}
