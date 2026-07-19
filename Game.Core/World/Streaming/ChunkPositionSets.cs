using System.Collections;

namespace Game.Core.World.Streaming;

/// <summary>
/// Immutable rectangular set of chunk coordinates. Streaming load/retain windows are
/// always rectangles, so representing them as hash tables wastes memory and rebuild
/// time on every camera update.
/// </summary>
public sealed class ChunkWindowSet : IReadOnlySet<ChunkPos>
{
    private readonly int _minimumX;
    private readonly int _minimumY;
    private readonly int _maximumX;
    private readonly int _maximumY;

    internal ChunkWindowSet(int minimumX, int minimumY, int maximumX, int maximumY)
    {
        if (maximumX < minimumX || maximumY < minimumY)
        {
            _minimumX = 0;
            _minimumY = 0;
            _maximumX = -1;
            _maximumY = -1;
            Count = 0;
            return;
        }

        var width = (long)maximumX - minimumX + 1;
        var height = (long)maximumY - minimumY + 1;
        Count = checked((int)(width * height));
        _minimumX = minimumX;
        _minimumY = minimumY;
        _maximumX = maximumX;
        _maximumY = maximumY;
    }

    public int Count { get; }

    public bool Contains(ChunkPos item)
    {
        return item.X >= _minimumX &&
               item.X <= _maximumX &&
               item.Y >= _minimumY &&
               item.Y <= _maximumY;
    }

    public Enumerator GetEnumerator()
    {
        return new Enumerator(_minimumX, _minimumY, _maximumX, _maximumY, Count > 0);
    }

    IEnumerator<ChunkPos> IEnumerable<ChunkPos>.GetEnumerator()
    {
        return GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public bool IsProperSubsetOf(IEnumerable<ChunkPos> other)
    {
        var otherSet = MaterializeOther(other);
        return Count < otherSet.Count && IsSubsetOf(otherSet);
    }

    public bool IsProperSupersetOf(IEnumerable<ChunkPos> other)
    {
        var otherSet = MaterializeOther(other);
        return Count > otherSet.Count && IsSupersetOf(otherSet);
    }

    public bool IsSubsetOf(IEnumerable<ChunkPos> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (other is IReadOnlySet<ChunkPos> otherSet)
        {
            if (Count > otherSet.Count)
            {
                return false;
            }

            foreach (var position in this)
            {
                if (!otherSet.Contains(position))
                {
                    return false;
                }
            }

            return true;
        }

        return IsSubsetOf(MaterializeOther(other));
    }

    public bool IsSupersetOf(IEnumerable<ChunkPos> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        foreach (var position in other)
        {
            if (!Contains(position))
            {
                return false;
            }
        }

        return true;
    }

    public bool Overlaps(IEnumerable<ChunkPos> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        foreach (var position in other)
        {
            if (Contains(position))
            {
                return true;
            }
        }

        return false;
    }

    public bool SetEquals(IEnumerable<ChunkPos> other)
    {
        var otherSet = MaterializeOther(other);
        return Count == otherSet.Count && IsSupersetOf(otherSet);
    }

    private static HashSet<ChunkPos> MaterializeOther(IEnumerable<ChunkPos> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return other as HashSet<ChunkPos> ?? new HashSet<ChunkPos>(other);
    }

    public struct Enumerator : IEnumerator<ChunkPos>
    {
        private readonly int _minimumX;
        private readonly int _maximumX;
        private readonly int _maximumY;
        private readonly bool _hasValues;
        private int _x;
        private int _y;
        private bool _started;

        internal Enumerator(int minimumX, int minimumY, int maximumX, int maximumY, bool hasValues)
        {
            _minimumX = minimumX;
            _maximumX = maximumX;
            _maximumY = maximumY;
            _hasValues = hasValues;
            _x = minimumX;
            _y = minimumY;
            _started = false;
            Current = default;
        }

        public ChunkPos Current { get; private set; }

        object IEnumerator.Current => Current;

        public bool MoveNext()
        {
            if (!_hasValues)
            {
                return false;
            }

            if (!_started)
            {
                _started = true;
            }
            else if (_x < _maximumX)
            {
                _x++;
            }
            else
            {
                _x = _minimumX;
                _y++;
            }

            if (_y > _maximumY)
            {
                return false;
            }

            Current = new ChunkPos(_x, _y);
            return true;
        }

        public void Reset()
        {
            throw new NotSupportedException();
        }

        public void Dispose()
        {
        }
    }
}

internal sealed class ChunkPositionListSet : IReadOnlySet<ChunkPos>
{
    private readonly IReadOnlyList<ChunkPos> _positions;

    public ChunkPositionListSet(IReadOnlyList<ChunkPos> positions)
    {
        _positions = positions ?? throw new ArgumentNullException(nameof(positions));
    }

    public int Count => _positions.Count;

    public bool Contains(ChunkPos item)
    {
        for (var index = 0; index < _positions.Count; index++)
        {
            if (_positions[index] == item)
            {
                return true;
            }
        }

        return false;
    }

    public IEnumerator<ChunkPos> GetEnumerator()
    {
        return _positions.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public bool IsProperSubsetOf(IEnumerable<ChunkPos> other)
    {
        var otherSet = MaterializeOther(other);
        return Count < otherSet.Count && IsSubsetOf(otherSet);
    }

    public bool IsProperSupersetOf(IEnumerable<ChunkPos> other)
    {
        var otherSet = MaterializeOther(other);
        return Count > otherSet.Count && IsSupersetOf(otherSet);
    }

    public bool IsSubsetOf(IEnumerable<ChunkPos> other)
    {
        var otherSet = MaterializeOther(other);
        if (Count > otherSet.Count)
        {
            return false;
        }

        for (var index = 0; index < _positions.Count; index++)
        {
            if (!otherSet.Contains(_positions[index]))
            {
                return false;
            }
        }

        return true;
    }

    public bool IsSupersetOf(IEnumerable<ChunkPos> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        foreach (var position in other)
        {
            if (!Contains(position))
            {
                return false;
            }
        }

        return true;
    }

    public bool Overlaps(IEnumerable<ChunkPos> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        foreach (var position in other)
        {
            if (Contains(position))
            {
                return true;
            }
        }

        return false;
    }

    public bool SetEquals(IEnumerable<ChunkPos> other)
    {
        var otherSet = MaterializeOther(other);
        return Count == otherSet.Count && IsSupersetOf(otherSet);
    }

    private static HashSet<ChunkPos> MaterializeOther(IEnumerable<ChunkPos> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return other as HashSet<ChunkPos> ?? new HashSet<ChunkPos>(other);
    }
}
