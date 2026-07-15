namespace Game.Core.Feedback;

internal sealed class BoundedCommandQueue<T>
    where T : struct
{
    private readonly T[] _items;
    private int _head;
    private int _count;

    public BoundedCommandQueue(int capacity)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        _items = new T[capacity];
    }

    public int Capacity => _items.Length;

    public int Count => _count;

    public long Enqueued { get; private set; }

    public long Dropped { get; private set; }

    public long Drained { get; private set; }

    public void Enqueue(in T item)
    {
        Enqueued++;
        if (_count == _items.Length)
        {
            _items[_head] = item;
            _head = Next(_head);
            Dropped++;
            return;
        }

        _items[PhysicalIndex(_count)] = item;
        _count++;
    }

    public int DrainTo(Span<T> destination)
    {
        var count = Math.Min(_count, destination.Length);
        for (var index = 0; index < count; index++)
        {
            destination[index] = _items[_head];
            _items[_head] = default;
            _head = Next(_head);
        }

        _count -= count;
        Drained += count;
        if (_count == 0)
        {
            _head = 0;
        }

        return count;
    }

    public T[] DrainToArray()
    {
        if (_count == 0)
        {
            return Array.Empty<T>();
        }

        var result = new T[_count];
        DrainTo(result);
        return result;
    }

    public void Clear()
    {
        Array.Clear(_items);
        _head = 0;
        _count = 0;
    }

    private int PhysicalIndex(int logicalIndex)
    {
        var index = _head + logicalIndex;
        return index >= _items.Length ? index - _items.Length : index;
    }

    private int Next(int index)
    {
        index++;
        return index == _items.Length ? 0 : index;
    }
}
