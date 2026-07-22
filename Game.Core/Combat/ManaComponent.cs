namespace Game.Core.Combat;

public sealed class ManaComponent
{
    public const float DefaultRegenerationDelaySeconds = 1.2f;
    public const int DefaultReservationCapacity = 8;

    private readonly ulong[] _reservationIds;
    private readonly int[] _reservationAmounts;
    private readonly float[] _reservationPreviousDelays;
    private readonly double[] _reservationPreviousAccumulators;
    private double _regenAccumulator;
    private float _regenDelayRemaining;
    private ulong _nextReservationId = 1;
    private ulong _latestSpendId;
    private int _openReservationCount;

    public ManaComponent(
        int maxMana = 20,
        int? currentMana = null,
        int reservationCapacity = DefaultReservationCapacity)
    {
        if (reservationCapacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(reservationCapacity));
        }

        Max = Math.Max(0, maxMana);
        Current = Math.Clamp(currentMana ?? Max, 0, Max);
        _reservationIds = new ulong[reservationCapacity];
        _reservationAmounts = new int[reservationCapacity];
        _reservationPreviousDelays = new float[reservationCapacity];
        _reservationPreviousAccumulators = new double[reservationCapacity];
    }

    public int Current { get; private set; }

    public int Max { get; private set; }

    public float RegenDelayRemaining => _regenDelayRemaining;

    public int OpenReservationCount => _openReservationCount;

    public int ReservationCapacity => _reservationIds.Length;

    public bool TrySpend(int amount)
    {
        return TrySpendDetailed(amount).Succeeded;
    }

    public ManaSpendResult TrySpendDetailed(
        int amount,
        float regenerationDelaySeconds = DefaultRegenerationDelaySeconds)
    {
        var result = TryReserve(amount, regenerationDelaySeconds);
        if (result.Status != ManaSpendStatus.Reserved)
        {
            return result;
        }

        var finalization = Commit(result.Reservation);
        if (finalization.Status != ManaReservationFinalizationStatus.Committed)
        {
            throw new InvalidOperationException("A newly reserved mana transaction could not be committed.");
        }

        return result with
        {
            Status = ManaSpendStatus.Spent,
            Reservation = default,
            CurrentMana = Current
        };
    }

    public ManaSpendResult TryReserve(
        int amount,
        float regenerationDelaySeconds = DefaultRegenerationDelaySeconds)
    {
        if (amount < 0)
        {
            return CreateRejected(ManaSpendStatus.InvalidCost, amount, regenerationDelaySeconds);
        }

        if (!float.IsFinite(regenerationDelaySeconds) || regenerationDelaySeconds < 0)
        {
            return CreateRejected(ManaSpendStatus.InvalidRegenerationDelay, amount, regenerationDelaySeconds);
        }

        if (amount == 0)
        {
            return new ManaSpendResult(
                ManaSpendStatus.NoCost,
                0,
                0,
                Current,
                Max,
                0,
                default);
        }

        if (Current < amount)
        {
            return CreateRejected(ManaSpendStatus.InsufficientMana, amount, regenerationDelaySeconds);
        }

        var slot = FindFreeReservationSlot();
        if (slot < 0)
        {
            return CreateRejected(ManaSpendStatus.ReservationCapacityReached, amount, regenerationDelaySeconds);
        }

        var reservationId = AllocateReservationId();
        _reservationIds[slot] = reservationId;
        _reservationAmounts[slot] = amount;
        _reservationPreviousDelays[slot] = _regenDelayRemaining;
        _reservationPreviousAccumulators[slot] = _regenAccumulator;
        _openReservationCount++;
        _latestSpendId = reservationId;

        Current -= amount;
        _regenDelayRemaining = regenerationDelaySeconds;
        _regenAccumulator = 0;

        return new ManaSpendResult(
            ManaSpendStatus.Reserved,
            amount,
            amount,
            Current,
            Max,
            regenerationDelaySeconds,
            new ManaReservationHandle(reservationId, amount));
    }

    public ManaReservationFinalizationResult Commit(ManaReservationHandle reservation)
    {
        if (!reservation.IsValid)
        {
            return new ManaReservationFinalizationResult(
                ManaReservationFinalizationStatus.NoCost,
                reservation,
                0,
                Current,
                ManaRefundReason.None);
        }

        var slot = FindReservationSlot(reservation);
        if (slot < 0)
        {
            return InvalidFinalization(reservation, ManaRefundReason.None);
        }

        ClearReservation(slot);
        return new ManaReservationFinalizationResult(
            ManaReservationFinalizationStatus.Committed,
            reservation,
            0,
            Current,
            ManaRefundReason.None);
    }

    public ManaReservationFinalizationResult FinalizeWithRefund(
        ManaReservationHandle reservation,
        ManaRefundPolicy policy,
        ManaRefundReason reason)
    {
        if (!reservation.IsValid)
        {
            return new ManaReservationFinalizationResult(
                ManaReservationFinalizationStatus.NoCost,
                reservation,
                0,
                Current,
                reason);
        }

        var slot = FindReservationSlot(reservation);
        if (slot < 0)
        {
            return InvalidFinalization(reservation, reason);
        }

        if (!ManaRefundRules.Allows(policy, reason))
        {
            ClearReservation(slot);
            return new ManaReservationFinalizationResult(
                ManaReservationFinalizationStatus.RefundDenied,
                reservation,
                0,
                Current,
                reason);
        }

        var previousCurrent = Current;
        Current = Math.Min(Max, Current + _reservationAmounts[slot]);
        if (_latestSpendId == reservation.Id)
        {
            _regenDelayRemaining = _reservationPreviousDelays[slot];
            _regenAccumulator = _reservationPreviousAccumulators[slot];
        }

        ClearReservation(slot);
        return new ManaReservationFinalizationResult(
            ManaReservationFinalizationStatus.Refunded,
            reservation,
            Current - previousCurrent,
            Current,
            reason);
    }

    public void Restore(int amount)
    {
        if (amount <= 0 || Max <= 0)
        {
            return;
        }

        Current = Math.Min(Max, Current + amount);
    }

    public void SetCurrent(int currentMana)
    {
        Current = Math.Clamp(currentMana, 0, Max);
        _regenAccumulator = 0;
        _regenDelayRemaining = 0;
        ClearAllReservations();
    }
    public void RestoreFull()
    {
        Current = Max;
        _regenAccumulator = 0;
        _regenDelayRemaining = 0;
        ClearAllReservations();
    }

    public void SetMax(int maxMana, bool preserveRatio = true)
    {
        var oldMax = Max;
        var oldCurrent = Current;
        Max = Math.Max(0, maxMana);

        if (Max == 0)
        {
            Current = 0;
            _regenAccumulator = 0;
            _regenDelayRemaining = 0;
            ClearAllReservations();
            return;
        }

        Current = preserveRatio && oldMax > 0
            ? Math.Clamp((int)MathF.Round(Max * (oldCurrent / (float)oldMax)), 0, Max)
            : Math.Clamp(oldCurrent, 0, Max);
    }

    public void Update(float deltaSeconds, float regenPerSecond = 6f, float regenMultiplier = 1f)
    {
        if (!float.IsFinite(deltaSeconds) || deltaSeconds <= 0 || Max <= 0)
        {
            return;
        }

        var remainingSeconds = deltaSeconds;
        if (_regenDelayRemaining > 0)
        {
            var delayedSeconds = Math.Min(_regenDelayRemaining, remainingSeconds);
            _regenDelayRemaining -= delayedSeconds;
            remainingSeconds -= delayedSeconds;
            if (remainingSeconds <= 0)
            {
                return;
            }
        }

        if (Current >= Max)
        {
            _regenAccumulator = 0;
            return;
        }

        var normalizedRate = float.IsFinite(regenPerSecond) ? Math.Max(0, regenPerSecond) : 0;
        var normalizedMultiplier = float.IsFinite(regenMultiplier) ? Math.Max(0, regenMultiplier) : 0;
        var regen = normalizedRate * normalizedMultiplier;
        if (!float.IsFinite(regen) || regen <= 0)
        {
            return;
        }

        _regenAccumulator += regen * remainingSeconds;
        var restore = (int)Math.Floor(_regenAccumulator + 0.000001d);
        if (restore <= 0)
        {
            return;
        }

        _regenAccumulator = Math.Max(0d, _regenAccumulator - restore);
        Restore(restore);
    }

    private ManaSpendResult CreateRejected(
        ManaSpendStatus status,
        int requestedAmount,
        float regenerationDelaySeconds)
    {
        return new ManaSpendResult(
            status,
            requestedAmount,
            0,
            Current,
            Max,
            regenerationDelaySeconds,
            default);
    }

    private ManaReservationFinalizationResult InvalidFinalization(
        ManaReservationHandle reservation,
        ManaRefundReason reason)
    {
        return new ManaReservationFinalizationResult(
            ManaReservationFinalizationStatus.InvalidReservation,
            reservation,
            0,
            Current,
            reason);
    }

    private int FindFreeReservationSlot()
    {
        for (var index = 0; index < _reservationIds.Length; index++)
        {
            if (_reservationIds[index] == 0)
            {
                return index;
            }
        }

        return -1;
    }

    private int FindReservationSlot(ManaReservationHandle reservation)
    {
        for (var index = 0; index < _reservationIds.Length; index++)
        {
            if (_reservationIds[index] == reservation.Id &&
                _reservationAmounts[index] == reservation.Amount)
            {
                return index;
            }
        }

        return -1;
    }

    private ulong AllocateReservationId()
    {
        for (var attempts = 0; attempts <= _reservationIds.Length; attempts++)
        {
            var candidate = _nextReservationId;
            _nextReservationId = _nextReservationId == ulong.MaxValue ? 1 : _nextReservationId + 1;
            if (candidate != 0 && !ContainsReservationId(candidate))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("Unable to allocate a unique mana reservation id.");
    }

    private bool ContainsReservationId(ulong id)
    {
        for (var index = 0; index < _reservationIds.Length; index++)
        {
            if (_reservationIds[index] == id)
            {
                return true;
            }
        }

        return false;
    }

    private void ClearReservation(int slot)
    {
        _reservationIds[slot] = 0;
        _reservationAmounts[slot] = 0;
        _reservationPreviousDelays[slot] = 0;
        _reservationPreviousAccumulators[slot] = 0;
        _openReservationCount--;
    }

    private void ClearAllReservations()
    {
        Array.Clear(_reservationIds);
        Array.Clear(_reservationAmounts);
        Array.Clear(_reservationPreviousDelays);
        Array.Clear(_reservationPreviousAccumulators);
        _openReservationCount = 0;
    }
}