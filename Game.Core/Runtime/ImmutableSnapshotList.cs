using System.Collections;

namespace Game.Core.Runtime;

public sealed class ImmutableSnapshotList<T> : IReadOnlyList<T>
{
    private readonly T[]? _items;
    private readonly IReadOnlyList<T>? _ownedItems;

    public ImmutableSnapshotList(IEnumerable<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        _items = items.ToArray();
    }

    private ImmutableSnapshotList(IReadOnlyList<T> ownedItems)
    {
        ArgumentNullException.ThrowIfNull(ownedItems);
        _ownedItems = ownedItems;
    }

    public static ImmutableSnapshotList<T> Empty { get; } = FromOwned(Array.Empty<T>());

    // Core snapshot builders hand off fresh storage that is never retained or mutated.
    // Keeping this path explicit prevents friend assemblies from accidentally selecting
    // an ownership-taking constructor for mutable arrays or lists.
    internal static ImmutableSnapshotList<T> FromOwned(IReadOnlyList<T> ownedItems)
    {
        return new ImmutableSnapshotList<T>(ownedItems);
    }

    public int Count => _items?.Length ?? _ownedItems!.Count;

    public T this[int index] => _items is not null ? _items[index] : _ownedItems![index];

    public Enumerator GetEnumerator()
    {
        return new Enumerator(_items, _ownedItems);
    }

    IEnumerator<T> IEnumerable<T>.GetEnumerator()
    {
        return GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public struct Enumerator : IEnumerator<T>
    {
        private readonly T[]? _items;
        private readonly IReadOnlyList<T>? _ownedItems;
        private int _index;

        internal Enumerator(T[]? items, IReadOnlyList<T>? ownedItems)
        {
            _items = items;
            _ownedItems = ownedItems;
            _index = -1;
        }

        public readonly T Current
        {
            get
            {
                var count = _items?.Length ?? _ownedItems!.Count;
                if ((uint)_index >= (uint)count)
                {
                    throw new InvalidOperationException();
                }

                return _items is not null ? _items[_index] : _ownedItems![_index];
            }
        }

        readonly object? IEnumerator.Current => Current;

        public bool MoveNext()
        {
            var nextIndex = _index + 1;
            var count = _items?.Length ?? _ownedItems!.Count;
            if ((uint)nextIndex < (uint)count)
            {
                _index = nextIndex;
                return true;
            }

            _index = count;
            return false;
        }

        public void Reset()
        {
            _index = -1;
        }

        public readonly void Dispose()
        {
        }
    }
}
