namespace Game.Core.Entities;

/// <summary>
/// Reusable storage for spatial queries. Runtime owners should keep one workspace per
/// independently executing query pipeline instead of allocating result collections per query.
/// </summary>
public sealed class EntityQueryWorkspace
{
    private readonly List<Entity> _results;
    private readonly HashSet<Entity> _seen;
    private readonly List<float> _scores;

    public EntityQueryWorkspace(int initialCapacity = 32)
    {
        if (initialCapacity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(initialCapacity));
        }

        _results = new List<Entity>(initialCapacity);
        _seen = new HashSet<Entity>(initialCapacity);
        _scores = new List<float>(initialCapacity);
    }

    public int Count => _results.Count;

    public int Capacity => _results.Capacity;

    public int PeakResultCount { get; private set; }

    public long QueryCount { get; private set; }

    public long TruncatedQueryCount { get; private set; }

    public bool LastQueryTruncated { get; private set; }

    public Entity this[int index] => _results[index];

    public void EnsureCapacity(int capacity)
    {
        if (capacity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        _results.EnsureCapacity(capacity);
        _seen.EnsureCapacity(capacity);
        _scores.EnsureCapacity(capacity);
    }

    public void ResetTelemetry()
    {
        PeakResultCount = 0;
        QueryCount = 0;
        TruncatedQueryCount = 0;
        LastQueryTruncated = false;
    }

    internal List<Entity> Results => _results;

    internal HashSet<Entity> Seen => _seen;

    internal List<float> Scores => _scores;

    internal void RecordQuery(bool truncated = false)
    {
        QueryCount++;
        LastQueryTruncated = truncated;
        TruncatedQueryCount += truncated ? 1 : 0;
        PeakResultCount = Math.Max(PeakResultCount, _results.Count);
    }
}
