namespace Game.Core.Combat;

public sealed class AttackCommandBuffer
{
    private readonly AttackInputCommand[] _commands;
    private int _head;
    private int _count;
    private ulong _nextSequence = 1;
    private ulong _lastQueuedTick;

    public AttackCommandBuffer(int capacity, ulong nextSequence = 1)
    {
        if (capacity is <= 0 or > AttackSequenceDefinition.MaximumCommandCapacity)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        if (nextSequence == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(nextSequence));
        }

        _commands = new AttackInputCommand[capacity];
        _nextSequence = nextSequence;
    }

    public int Capacity => _commands.Length;

    public int Count => _count;

    public ulong NextSequence => _nextSequence;

    public AttackInputResult TryEnqueue(ulong tick, AttackInputKind kind)
    {
        if (_count == Capacity)
        {
            return AttackInputResult.Rejected(AttackInputFailure.BufferFull);
        }

        if (_count > 0 && tick < _lastQueuedTick)
        {
            return AttackInputResult.Rejected(AttackInputFailure.OutOfOrder);
        }

        if (_nextSequence == 0)
        {
            return AttackInputResult.Rejected(AttackInputFailure.SequenceExhausted);
        }

        var command = new AttackInputCommand(tick, _nextSequence, kind);
        _nextSequence = _nextSequence == ulong.MaxValue ? 0 : _nextSequence + 1;
        var tail = (_head + _count) % Capacity;
        _commands[tail] = command;
        _count++;
        _lastQueuedTick = tick;
        return new AttackInputResult(true, AttackInputFailure.None, command);
    }

    public bool TryPeek(out AttackInputCommand command)
    {
        if (_count == 0)
        {
            command = default;
            return false;
        }

        command = _commands[_head];
        return true;
    }

    public bool TryDequeue(out AttackInputCommand command)
    {
        if (!TryPeek(out command))
        {
            return false;
        }

        _commands[_head] = default;
        _head = (_head + 1) % Capacity;
        _count--;
        if (_count == 0)
        {
            _head = 0;
            _lastQueuedTick = 0;
        }

        return true;
    }

    public void Clear()
    {
        Array.Clear(_commands);
        _head = 0;
        _count = 0;
        _lastQueuedTick = 0;
    }
}

public sealed class AttackEventBuffer
{
    private readonly AttackRuntimeEvent[] _events;

    public AttackEventBuffer(int capacity)
    {
        if (capacity is <= 0 or > AttackSequenceDefinition.MaximumEventCapacity)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        _events = new AttackRuntimeEvent[capacity];
    }

    public int Capacity => _events.Length;

    public int Count { get; private set; }

    public int DroppedCount { get; private set; }

    public AttackRuntimeEvent this[int index]
    {
        get
        {
            if ((uint)index >= (uint)Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return _events[index];
        }
    }

    public bool TryWrite(AttackRuntimeEvent runtimeEvent)
    {
        if (Count == Capacity)
        {
            DroppedCount++;
            return false;
        }

        _events[Count++] = runtimeEvent;
        return true;
    }

    public ReadOnlySpan<AttackRuntimeEvent> AsSpan() => _events.AsSpan(0, Count);

    public void Clear()
    {
        Array.Clear(_events, 0, Count);
        Count = 0;
        DroppedCount = 0;
    }
}
