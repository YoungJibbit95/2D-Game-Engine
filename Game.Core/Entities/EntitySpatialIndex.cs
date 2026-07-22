using Game.Core.World;
using System.Numerics;

namespace Game.Core.Entities;

[Flags]
internal enum EntitySpatialQueryKinds : byte
{
    None = 0,
    Player = 1 << 0,
    Enemy = 1 << 1,
    Projectile = 1 << 2,
    DroppedItem = 1 << 3,
    Other = 1 << 4,
    All = byte.MaxValue
}

internal sealed class EntitySpatialIndex
{
    private const int PreparedBucketReserve = 32;
    private const int InitialBucketEntryCapacity = 8;
    private readonly Dictionary<CellPos, List<Entry>> _cells = new();
    private readonly Dictionary<Entity, Entry> _entries = new();
    private readonly Stack<List<Entry>> _bucketPool = new();
    private int _peakActiveBucketCount;
    private uint _queryStamp;

    public EntitySpatialIndex(int cellSize)
    {
        if (cellSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(cellSize), "Cell size must be greater than zero.");
        }

        CellSize = cellSize;
    }

    public int CellSize { get; }

    internal EntitySpatialIndexTelemetry Telemetry => new(
        _cells.Count,
        _bucketPool.Count,
        _peakActiveBucketCount,
        PreparedBucketReserve);

    public void Insert(Entity entity, RectI bounds)
    {
        ArgumentNullException.ThrowIfNull(entity);
        if (bounds.IsEmpty)
        {
            throw new ArgumentException("Spatial bounds must not be empty.", nameof(bounds));
        }

        Remove(entity);
        var entry = new Entry(entity, bounds);
        _entries.Add(entity, entry);
        AddToCells(entry);
        PrepareBucketReserve();
    }

    public void Update(Entity entity, RectI bounds)
    {
        ArgumentNullException.ThrowIfNull(entity);
        if (bounds.IsEmpty)
        {
            throw new ArgumentException("Spatial bounds must not be empty.", nameof(bounds));
        }

        if (!_entries.TryGetValue(entity, out var entry))
        {
            Insert(entity, bounds);
            return;
        }

        if (entry.Bounds == bounds)
        {
            return;
        }

        RemoveFromCells(entry);
        entry.Bounds = bounds;
        AddToCells(entry);
    }

    public bool Remove(Entity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        if (!_entries.Remove(entity, out var entry))
        {
            return false;
        }

        RemoveFromCells(entry);
        return true;
    }

    public IReadOnlyList<Entity> Query(RectI area)
    {
        if (area.IsEmpty)
        {
            return Array.Empty<Entity>();
        }

        var result = new List<Entity>();
        QueryInto(area, result, new HashSet<Entity>());
        return result;
    }

    public void QueryInto(RectI area, List<Entity> result, HashSet<Entity> seen)
    {
        QueryInto(area, result, seen, int.MaxValue);
    }

    public bool QueryInto(
        RectI area,
        List<Entity> result,
        HashSet<Entity> seen,
        int maximumResults)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(seen);
        if (maximumResults < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumResults));
        }

        result.Clear();
        seen.Clear();
        if (area.IsEmpty)
        {
            return false;
        }

        if (maximumResults == 0)
        {
            return true;
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

                for (var index = 0; index < bucket.Count; index++)
                {
                    var entry = bucket[index];
                    if (entry.Bounds.Intersects(area) && seen.Add(entry.Entity))
                    {
                        result.Add(entry.Entity);
                        if (result.Count >= maximumResults)
                        {
                            return true;
                        }
                    }
                }
            }
        }

        return false;
    }

    public bool QueryNearestInto(
        RectI area,
        Vector2 origin,
        List<Entity> result,
        List<float> scores,
        int maximumResults,
        int maximumEntryTests,
        EntitySpatialQueryKinds kinds)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(scores);
        if (maximumResults <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumResults));
        }

        if (maximumEntryTests <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumEntryTests));
        }

        result.Clear();
        scores.Clear();
        if (area.IsEmpty)
        {
            return false;
        }

        var queryStamp = NextQueryStamp();
        var entryTests = 0;
        var matchingEntries = 0;
        GetCellBounds(area, out var minX, out var minY, out var maxX, out var maxY);
        for (var y = minY; y <= maxY; y++)
        {
            for (var x = minX; x <= maxX; x++)
            {
                if (!_cells.TryGetValue(new CellPos(x, y), out var bucket))
                {
                    continue;
                }

                for (var index = 0; index < bucket.Count; index++)
                {
                    var entry = bucket[index];
                    if (entry.LastQueryStamp == queryStamp)
                    {
                        continue;
                    }

                    entry.LastQueryStamp = queryStamp;
                    if (entryTests >= maximumEntryTests)
                    {
                        return true;
                    }

                    entryTests++;
                    if (!entry.Bounds.Intersects(area) || !MatchesKinds(entry.Entity, kinds))
                    {
                        continue;
                    }

                    matchingEntries++;
                    var center = new Vector2(
                        entry.Bounds.Left + entry.Bounds.Width * 0.5f,
                        entry.Bounds.Top + entry.Bounds.Height * 0.5f);
                    var score = Vector2.DistanceSquared(origin, center);
                    AddNearestCandidate(entry.Entity, score, result, scores, maximumResults);
                }
            }
        }

        return matchingEntries > maximumResults;
    }

    private static bool MatchesKinds(Entity entity, EntitySpatialQueryKinds kinds)
    {
        var kind = entity switch
        {
            PlayerEntity => EntitySpatialQueryKinds.Player,
            EnemyEntity => EntitySpatialQueryKinds.Enemy,
            Game.Core.Projectiles.ProjectileEntity => EntitySpatialQueryKinds.Projectile,
            DroppedItemEntity => EntitySpatialQueryKinds.DroppedItem,
            _ => EntitySpatialQueryKinds.Other
        };
        return (kinds & kind) != EntitySpatialQueryKinds.None;
    }

    private void AddToCells(Entry entry)
    {
        GetCellBounds(entry.Bounds, out var minX, out var minY, out var maxX, out var maxY);
        for (var y = minY; y <= maxY; y++)
        {
            for (var x = minX; x <= maxX; x++)
            {
                var cell = new CellPos(x, y);
                if (!_cells.TryGetValue(cell, out var bucket))
                {
                    bucket = _bucketPool.Count > 0
                        ? _bucketPool.Pop()
                        : new List<Entry>(InitialBucketEntryCapacity);
                    _cells.Add(cell, bucket);
                }

                bucket.Add(entry);
            }
        }

        _peakActiveBucketCount = Math.Max(_peakActiveBucketCount, _cells.Count);
    }

    private void PrepareBucketReserve()
    {
        _cells.EnsureCapacity(checked(_cells.Count + PreparedBucketReserve));
        while (_bucketPool.Count < PreparedBucketReserve)
        {
            // Eight entries covers compact contact clusters without sizing every
            // bucket for the global entity limit. A fixed prepared frontier absorbs
            // moving-cell churn without O(entity-count) empty-bucket retention.
            _bucketPool.Push(new List<Entry>(InitialBucketEntryCapacity));
        }
    }

    private void RemoveFromCells(Entry entry)
    {
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
                    _bucketPool.Push(bucket);
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

    private uint NextQueryStamp()
    {
        _queryStamp++;
        if (_queryStamp != 0)
        {
            return _queryStamp;
        }

        foreach (var entry in _entries.Values)
        {
            entry.LastQueryStamp = 0;
        }

        _queryStamp = 1;
        return _queryStamp;
    }

    private static void AddNearestCandidate(
        Entity entity,
        float score,
        List<Entity> result,
        List<float> scores,
        int maximumResults)
    {
        if (result.Count < maximumResults)
        {
            result.Add(entity);
            scores.Add(score);
            SiftScoreUp(result, scores, result.Count - 1);
            return;
        }

        if (score >= scores[0])
        {
            return;
        }

        result[0] = entity;
        scores[0] = score;
        SiftScoreDown(result, scores, 0);
    }

    private static void SiftScoreUp(List<Entity> result, List<float> scores, int child)
    {
        while (child > 0)
        {
            var parent = (child - 1) / 2;
            if (scores[parent] >= scores[child])
            {
                return;
            }

            SwapCandidates(result, scores, parent, child);
            child = parent;
        }
    }

    private static void SiftScoreDown(List<Entity> result, List<float> scores, int parent)
    {
        while (true)
        {
            var left = parent * 2 + 1;
            if (left >= scores.Count)
            {
                return;
            }

            var greater = left;
            var right = left + 1;
            if (right < scores.Count && scores[right] > scores[left])
            {
                greater = right;
            }

            if (scores[parent] >= scores[greater])
            {
                return;
            }

            SwapCandidates(result, scores, parent, greater);
            parent = greater;
        }
    }

    private static void SwapCandidates(List<Entity> result, List<float> scores, int left, int right)
    {
        (result[left], result[right]) = (result[right], result[left]);
        (scores[left], scores[right]) = (scores[right], scores[left]);
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

    private sealed class Entry
    {
        public Entry(Entity entity, RectI bounds)
        {
            Entity = entity;
            Bounds = bounds;
        }

        public Entity Entity { get; }

        public RectI Bounds { get; set; }

        public uint LastQueryStamp { get; set; }
    }
}

internal readonly record struct EntitySpatialIndexTelemetry(
    int ActiveBuckets,
    int PreparedBuckets,
    int PeakActiveBuckets,
    int PreparedBucketReserve);
