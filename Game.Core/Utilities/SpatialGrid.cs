using Game.Core.World;

namespace Game.Core.Utilities;

public sealed class SpatialGrid<T>
    where T : notnull
{
    private readonly Dictionary<CellPos, List<Entry>> _cells = new();
    private readonly Dictionary<T, Entry> _entries = new();

    public SpatialGrid(int cellSize)
    {
        if (cellSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(cellSize), "Cell size must be greater than zero.");
        }

        CellSize = cellSize;
    }

    public int CellSize { get; }

    public int Count => _entries.Count;

    public void Clear()
    {
        _cells.Clear();
        _entries.Clear();
    }

    public void Insert(T item, RectI bounds)
    {
        if (bounds.IsEmpty)
        {
            throw new ArgumentException("Spatial bounds must not be empty.", nameof(bounds));
        }

        Remove(item);

        var entry = new Entry(item, bounds);
        _entries.Add(item, entry);

        GetCellBounds(bounds, out var minX, out var minY, out var maxX, out var maxY);
        for (var y = minY; y <= maxY; y++)
        {
            for (var x = minX; x <= maxX; x++)
            {
                var cell = new CellPos(x, y);
                if (!_cells.TryGetValue(cell, out var bucket))
                {
                    bucket = new List<Entry>();
                    _cells.Add(cell, bucket);
                }

                bucket.Add(entry);
            }
        }
    }

    public bool Remove(T item)
    {
        if (!_entries.Remove(item, out var entry))
        {
            return false;
        }

        GetCellBounds(entry.Bounds, out var minX, out var minY, out var maxX, out var maxY);
        for (var y = minY; y <= maxY; y++)
        {
            for (var x = minX; x <= maxX; x++)
            {
                var cell = new CellPos(x, y);
                if (!_cells.TryGetValue(cell, out var bucket))
                {
                    continue;
                }

                bucket.Remove(entry);
                if (bucket.Count == 0)
                {
                    _cells.Remove(cell);
                }
            }
        }

        return true;
    }

    public IReadOnlyList<T> Query(RectI area)
    {
        if (area.IsEmpty)
        {
            return Array.Empty<T>();
        }

        var result = new List<T>();
        QueryInto(area, result, new HashSet<T>());
        return result;
    }

    public void QueryInto(RectI area, List<T> result, HashSet<T> seen)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(seen);
        result.Clear();
        seen.Clear();
        if (area.IsEmpty)
        {
            return;
        }

        GetCellBounds(area, out var minX, out var minY, out var maxX, out var maxY);
        for (var y = minY; y <= maxY; y++)
        {
            for (var x = minX; x <= maxX; x++)
            {
                if (!_cells.TryGetValue(new CellPos(x, y), out var bucket))
                {
                    continue;
                }

                foreach (var entry in bucket)
                {
                    if (entry.Bounds.Intersects(area) && seen.Add(entry.Item))
                    {
                        result.Add(entry.Item);
                    }
                }
            }
        }
    }

    private void GetCellBounds(RectI bounds, out int minX, out int minY, out int maxX, out int maxY)
    {
        minX = FloorDiv(bounds.Left, CellSize);
        maxX = FloorDiv(bounds.Right - 1, CellSize);
        minY = FloorDiv(bounds.Top, CellSize);
        maxY = FloorDiv(bounds.Bottom - 1, CellSize);
    }

    private static int FloorDiv(int value, int divisor)
    {
        var quotient = value / divisor;
        var remainder = value % divisor;

        if (remainder != 0 && ((remainder < 0) != (divisor < 0)))
        {
            quotient--;
        }

        return quotient;
    }

    private readonly record struct CellPos(int X, int Y);

    private sealed record Entry(T Item, RectI Bounds);
}
