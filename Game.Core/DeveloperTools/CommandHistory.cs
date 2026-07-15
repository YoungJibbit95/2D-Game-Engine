using Game.Core.Commands;

namespace Game.Core.DeveloperTools;

public sealed class CommandHistory
{
    private readonly List<CommandHistoryEntry> _entries;
    private readonly Func<DateTimeOffset> _clock;
    private long _nextSequence = 1;
    private int _navigationIndex;

    public CommandHistory(int capacity = 100, Func<DateTimeOffset>? clock = null)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "History capacity must be greater than zero.");
        }

        Capacity = capacity;
        _entries = new List<CommandHistoryEntry>(capacity);
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public int Capacity { get; }

    public IReadOnlyList<CommandHistoryEntry> Entries => _entries;

    public void Record(string input, CommandResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (string.IsNullOrWhiteSpace(input))
        {
            return;
        }

        if (_entries.Count == Capacity)
        {
            _entries.RemoveAt(0);
        }

        _entries.Add(new CommandHistoryEntry(_nextSequence++, _clock(), input, result));
        ResetNavigation();
    }

    public CommandHistoryEntry? Previous()
    {
        if (_entries.Count == 0)
        {
            return null;
        }

        _navigationIndex = Math.Max(0, _navigationIndex - 1);
        return _entries[_navigationIndex];
    }

    public CommandHistoryEntry? Next()
    {
        if (_navigationIndex >= _entries.Count - 1)
        {
            _navigationIndex = _entries.Count;
            return null;
        }

        _navigationIndex++;
        return _entries[_navigationIndex];
    }

    public void ResetNavigation()
    {
        _navigationIndex = _entries.Count;
    }

    public void Clear()
    {
        _entries.Clear();
        ResetNavigation();
    }
}

public sealed record CommandHistoryEntry(
    long Sequence,
    DateTimeOffset ExecutedAtUtc,
    string Input,
    CommandResult Result);
