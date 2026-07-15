namespace Game.Core.WorldEvents;

public sealed class WorldEventJournal
{
    private readonly WorldEventDomainEvent[] _entries;
    private int _start;
    private int _count;
    private long _nextSequence;

    public WorldEventJournal(int capacity = 256)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        _entries = new WorldEventDomainEvent[capacity];
    }

    public int Count => _count;

    public long NextSequence => _nextSequence;

    public void Append(WorldEventExecutionResult result)
    {
        for (var index = 0; index < result.Events.Count; index++)
        {
            var source = result.Events[index];
            var entry = source with { Sequence = _nextSequence++ };
            if (_count < _entries.Length)
            {
                _entries[(_start + _count) % _entries.Length] = entry;
                _count++;
            }
            else
            {
                _entries[_start] = entry;
                _start = (_start + 1) % _entries.Length;
            }
        }
    }

    public WorldEventJournalSnapshot Capture()
    {
        var entries = new WorldEventDomainEvent[_count];
        for (var index = 0; index < _count; index++)
        {
            entries[index] = _entries[(_start + index) % _entries.Length];
        }

        return new WorldEventJournalSnapshot(
            WorldEventJournalSnapshot.CurrentFormatVersion,
            _nextSequence,
            entries);
    }

    public void Restore(WorldEventJournalSnapshot snapshot)
    {
        Validate(snapshot);
        _start = 0;
        _count = 0;
        _nextSequence = snapshot.NextSequence;
        var skip = Math.Max(0, snapshot.Entries.Count - _entries.Length);
        for (var index = skip; index < snapshot.Entries.Count; index++)
        {
            _entries[_count++] = snapshot.Entries[index];
        }
    }

    public static void Validate(WorldEventJournalSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.FormatVersion != WorldEventJournalSnapshot.CurrentFormatVersion ||
            snapshot.NextSequence < 0)
        {
            throw new InvalidDataException("World-event journal snapshot is invalid or unsupported.");
        }

        long previous = -1;
        for (var index = 0; index < snapshot.Entries.Count; index++)
        {
            var entry = snapshot.Entries[index];
            if (entry.Sequence <= previous || entry.Sequence >= snapshot.NextSequence ||
                entry.WorldTick < 0 || string.IsNullOrWhiteSpace(entry.EventId) ||
                !float.IsFinite(entry.Progress) || entry.Progress is < 0f or > 1f ||
                entry.CooldownUntilTickExclusive < 0 || entry.TriggerSequence < 0)
            {
                throw new InvalidDataException("World-event journal contains an invalid entry.");
            }

            previous = entry.Sequence;
        }
    }
}

public sealed record WorldEventJournalSnapshot(
    int FormatVersion,
    long NextSequence,
    IReadOnlyList<WorldEventDomainEvent> Entries)
{
    public const int CurrentFormatVersion = 1;
}
