namespace Game.Core.Interaction;

[Flags]
public enum InteractionCancelPolicy
{
    None = 0,
    CancelOnRelease = 1 << 0,
    PauseOnRelease = 1 << 1,
    DecayOnRelease = 1 << 2,
    CancelOnTargetChange = 1 << 3,
    RetainOnTemporaryMiss = 1 << 4,
    Default = CancelOnRelease | CancelOnTargetChange
}

public enum InteractionHoldStatus
{
    Idle,
    Started,
    InProgress,
    Paused,
    Decaying,
    Completed,
    Cancelled,
    Blocked
}

public sealed record InteractionHoldSnapshot
{
    public const int CurrentFormatVersion = 1;

    public int FormatVersion { get; init; } = CurrentFormatVersion;

    public long LastAdvancedTick { get; init; }

    public InteractionHoldStatus Status { get; init; }

    public InteractionTargetIdentity? Target { get; init; }

    public int AccumulatedTicks { get; init; }

    public int RequiredTicks { get; init; }

    public InteractionFailure Failure { get; init; }

    public float Progress => RequiredTicks <= 0
        ? 0f
        : Math.Clamp(AccumulatedTicks / (float)RequiredTicks, 0f, 1f);

    public static InteractionHoldSnapshot Empty { get; } = new();
}

public readonly record struct InteractionHoldInput(
    long WorldTick,
    bool IsHeld,
    InteractionResult Resolution,
    InteractionCancelPolicy CancelPolicy = InteractionCancelPolicy.Default,
    int DecayTicksPerTick = 2);

public sealed class InteractionHoldTracker
{
    public InteractionHoldSnapshot Advance(
        InteractionHoldSnapshot previous,
        in InteractionHoldInput input)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ValidateSnapshot(previous);
        ValidateInput(previous, input);
        var elapsed = previous.LastAdvancedTick == 0 && previous.Target is null
            ? 1
            : (int)Math.Clamp(input.WorldTick - previous.LastAdvancedTick, 1L, int.MaxValue);

        if (!input.Resolution.Success)
        {
            if (previous.Target is not null &&
                (input.CancelPolicy & InteractionCancelPolicy.RetainOnTemporaryMiss) != 0)
            {
                return previous with
                {
                    LastAdvancedTick = input.WorldTick,
                    Status = InteractionHoldStatus.Paused,
                    Failure = input.Resolution.Failure
                };
            }

            return new InteractionHoldSnapshot
            {
                LastAdvancedTick = input.WorldTick,
                Status = previous.Target is null
                    ? InteractionHoldStatus.Blocked
                    : InteractionHoldStatus.Cancelled,
                Failure = input.Resolution.Failure
            };
        }

        var candidate = input.Resolution.Candidate;
        var changedTarget = previous.Target is not null && previous.Target.Value != candidate.Identity;
        if (changedTarget && (input.CancelPolicy & InteractionCancelPolicy.CancelOnTargetChange) != 0)
        {
            previous = InteractionHoldSnapshot.Empty with
            {
                LastAdvancedTick = Math.Max(0, input.WorldTick - 1),
                Status = InteractionHoldStatus.Cancelled,
                Failure = InteractionFailure.TargetChanged
            };
            elapsed = 1;
        }

        if (!input.IsHeld)
        {
            return ResolveRelease(previous, candidate, input, elapsed);
        }

        var started = previous.Target is null || changedTarget ||
                      previous.Status is InteractionHoldStatus.Completed or InteractionHoldStatus.Cancelled;
        var accumulated = started ? elapsed : checked(previous.AccumulatedTicks + elapsed);
        var required = candidate.RequiredHoldTicks;
        var completed = accumulated >= required;
        return new InteractionHoldSnapshot
        {
            LastAdvancedTick = input.WorldTick,
            Status = completed
                ? InteractionHoldStatus.Completed
                : started
                    ? InteractionHoldStatus.Started
                    : InteractionHoldStatus.InProgress,
            Target = candidate.Identity,
            AccumulatedTicks = Math.Min(accumulated, required),
            RequiredTicks = required,
            Failure = InteractionFailure.None
        };
    }

    public static void ValidateSnapshot(InteractionHoldSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.FormatVersion != InteractionHoldSnapshot.CurrentFormatVersion ||
            snapshot.LastAdvancedTick < 0 || snapshot.AccumulatedTicks < 0 || snapshot.RequiredTicks < 0 ||
            snapshot.AccumulatedTicks > snapshot.RequiredTicks ||
            (snapshot.Target is null && snapshot.AccumulatedTicks != 0) ||
            (snapshot.Target is not null && snapshot.RequiredTicks <= 0))
        {
            throw new InvalidDataException("Interaction hold snapshot is invalid or unsupported.");
        }
    }

    private static InteractionHoldSnapshot ResolveRelease(
        InteractionHoldSnapshot previous,
        in InteractionCandidate candidate,
        in InteractionHoldInput input,
        int elapsed)
    {
        if (previous.Target is null)
        {
            return new InteractionHoldSnapshot
            {
                LastAdvancedTick = input.WorldTick,
                Status = InteractionHoldStatus.Idle
            };
        }

        if ((input.CancelPolicy & InteractionCancelPolicy.DecayOnRelease) != 0)
        {
            var decay = checked(input.DecayTicksPerTick * elapsed);
            var remaining = Math.Max(0, previous.AccumulatedTicks - decay);
            return remaining == 0
                ? new InteractionHoldSnapshot
                {
                    LastAdvancedTick = input.WorldTick,
                    Status = InteractionHoldStatus.Cancelled,
                    Failure = InteractionFailure.Released
                }
                : previous with
                {
                    LastAdvancedTick = input.WorldTick,
                    Status = InteractionHoldStatus.Decaying,
                    AccumulatedTicks = remaining,
                    Failure = InteractionFailure.Released
                };
        }

        if ((input.CancelPolicy & InteractionCancelPolicy.PauseOnRelease) != 0)
        {
            return previous with
            {
                LastAdvancedTick = input.WorldTick,
                Status = InteractionHoldStatus.Paused,
                Failure = InteractionFailure.Released
            };
        }

        return new InteractionHoldSnapshot
        {
            LastAdvancedTick = input.WorldTick,
            Status = InteractionHoldStatus.Cancelled,
            Failure = InteractionFailure.Released
        };
    }

    private static void ValidateInput(InteractionHoldSnapshot previous, in InteractionHoldInput input)
    {
        if (input.WorldTick < previous.LastAdvancedTick || input.DecayTicksPerTick <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(input));
        }

        var releasePolicies = 0;
        releasePolicies += (input.CancelPolicy & InteractionCancelPolicy.CancelOnRelease) != 0 ? 1 : 0;
        releasePolicies += (input.CancelPolicy & InteractionCancelPolicy.PauseOnRelease) != 0 ? 1 : 0;
        releasePolicies += (input.CancelPolicy & InteractionCancelPolicy.DecayOnRelease) != 0 ? 1 : 0;
        if (releasePolicies > 1)
        {
            throw new ArgumentException("Only one release policy may be active.", nameof(input));
        }
    }
}
