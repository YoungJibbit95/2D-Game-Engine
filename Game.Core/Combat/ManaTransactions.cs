namespace Game.Core.Combat;

public enum ManaSpendStatus
{
    None,
    NoCost,
    Reserved,
    Spent,
    InsufficientMana,
    InvalidCost,
    InvalidRegenerationDelay,
    ReservationCapacityReached
}

public enum ManaReservationFinalizationStatus
{
    None,
    NoCost,
    Committed,
    Refunded,
    RefundDenied,
    InvalidReservation
}

public enum ManaRefundPolicy
{
    None,
    BeforeEffect,
    Always
}

public enum ManaRefundReason
{
    None,
    MaterializationFailed,
    CancelledBeforeEffect,
    CancelledAfterEffect,
    ResourceCommitFailed,
    RuntimeReset
}

public readonly record struct ManaReservationHandle(ulong Id, int Amount)
{
    public bool IsValid => Id != 0 && Amount > 0;
}

public readonly record struct ManaSpendResult(
    ManaSpendStatus Status,
    int RequestedAmount,
    int SpentAmount,
    int CurrentMana,
    int MaximumMana,
    float RegenerationDelaySeconds,
    ManaReservationHandle Reservation)
{
    public static ManaSpendResult None => default;

    public bool Succeeded => Status is ManaSpendStatus.NoCost or ManaSpendStatus.Reserved or ManaSpendStatus.Spent;

    public bool Rejected => Status is
        ManaSpendStatus.InsufficientMana or
        ManaSpendStatus.InvalidCost or
        ManaSpendStatus.InvalidRegenerationDelay or
        ManaSpendStatus.ReservationCapacityReached;
}

public readonly record struct ManaReservationFinalizationResult(
    ManaReservationFinalizationStatus Status,
    ManaReservationHandle Reservation,
    int ManaRestored,
    int CurrentMana,
    ManaRefundReason RefundReason)
{
    public static ManaReservationFinalizationResult None => default;

    public bool Finalized => Status is
        ManaReservationFinalizationStatus.NoCost or
        ManaReservationFinalizationStatus.Committed or
        ManaReservationFinalizationStatus.Refunded or
        ManaReservationFinalizationStatus.RefundDenied;
}

public static class ManaRefundRules
{
    public static bool Allows(ManaRefundPolicy policy, ManaRefundReason reason)
    {
        return policy switch
        {
            ManaRefundPolicy.Always => reason != ManaRefundReason.None,
            ManaRefundPolicy.BeforeEffect => reason is
                ManaRefundReason.MaterializationFailed or
                ManaRefundReason.CancelledBeforeEffect or
                ManaRefundReason.ResourceCommitFailed or
                ManaRefundReason.RuntimeReset,
            _ => false
        };
    }
}