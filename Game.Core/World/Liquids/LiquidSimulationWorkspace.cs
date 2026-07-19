namespace Game.Core.World.Liquids;

/// <summary>
/// Reusable, bounded storage for deterministic active-cell liquid simulation.
/// A workspace belongs to one world at a time and can be caller-owned when a
/// world runtime wants independent queues or explicit lifetime control.
/// </summary>
public sealed class LiquidSimulationWorkspace
{
    public const int DefaultMaximumQueuedCells = 65_536;
    public const int DefaultMaximumQueuedRegions = 256;

    private readonly int _maximumQueuedCells;
    private readonly int _maximumQueuedRegions;
    private readonly Queue<TilePos> _activeCells;
    private readonly HashSet<TilePos> _activeCellSet;
    private readonly Queue<RegionSeedCursor> _seedRegions;
    private readonly HashSet<RectI> _seedRegionSet;
    private readonly HashSet<TilePos> _changedTiles;
    private readonly Dictionary<ChunkPos, RectI> _changedChunkBounds;
    private readonly List<RectI> _changedRegions;
    private World? _boundWorld;
    private int _droppedActivations;
    private int _droppedSeedRegions;

    public LiquidSimulationWorkspace(
        int maximumQueuedCells = DefaultMaximumQueuedCells,
        int maximumQueuedRegions = DefaultMaximumQueuedRegions,
        int initialCapacity = 256)
    {
        if (maximumQueuedCells <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumQueuedCells),
                "The liquid active-cell capacity must be positive.");
        }

        if (maximumQueuedRegions <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumQueuedRegions),
                "The liquid seed-region capacity must be positive.");
        }

        if (initialCapacity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(initialCapacity));
        }

        _maximumQueuedCells = maximumQueuedCells;
        _maximumQueuedRegions = maximumQueuedRegions;
        var cellCapacity = Math.Min(initialCapacity, maximumQueuedCells);
        var regionCapacity = Math.Min(initialCapacity, maximumQueuedRegions);
        _activeCells = new Queue<TilePos>(cellCapacity);
        _activeCellSet = new HashSet<TilePos>(cellCapacity);
        _seedRegions = new Queue<RegionSeedCursor>(regionCapacity);
        _seedRegionSet = new HashSet<RectI>(regionCapacity);
        _changedTiles = new HashSet<TilePos>(cellCapacity);
        _changedChunkBounds = new Dictionary<ChunkPos, RectI>(cellCapacity);
        _changedRegions = new List<RectI>(regionCapacity);
    }

    public int PendingActiveCellCount => _activeCells.Count;

    public int PendingSeedRegionCount => _seedRegions.Count;

    public bool HasPendingWork => _activeCells.Count > 0 || _seedRegions.Count > 0;

    public int MaximumQueuedCells => _maximumQueuedCells;

    public int MaximumQueuedRegions => _maximumQueuedRegions;

    public bool Activate(TilePos position)
    {
        if (_activeCellSet.Contains(position))
        {
            return false;
        }

        if (_activeCells.Count >= _maximumQueuedCells)
        {
            _droppedActivations++;
            return false;
        }

        _activeCellSet.Add(position);
        _activeCells.Enqueue(position);
        return true;
    }

    public bool ActivateRegion(RectI region)
    {
        if (region.IsEmpty || _seedRegionSet.Contains(region))
        {
            return false;
        }

        if (_seedRegions.Count >= _maximumQueuedRegions)
        {
            _droppedSeedRegions++;
            return false;
        }

        _seedRegionSet.Add(region);
        _seedRegions.Enqueue(new RegionSeedCursor(region));
        return true;
    }

    public void Reset()
    {
        _activeCells.Clear();
        _activeCellSet.Clear();
        _seedRegions.Clear();
        _seedRegionSet.Clear();
        _changedTiles.Clear();
        _changedChunkBounds.Clear();
        _changedRegions.Clear();
        _boundWorld = null;
        _droppedActivations = 0;
        _droppedSeedRegions = 0;
    }

    internal void Bind(World world)
    {
        if (_boundWorld is null)
        {
            _boundWorld = world;
            return;
        }

        if (ReferenceEquals(_boundWorld, world))
        {
            return;
        }

        Reset();
        _boundWorld = world;
    }

    internal void BeginStep()
    {
        _changedTiles.Clear();
        _changedChunkBounds.Clear();
        _changedRegions.Clear();
    }

    internal TilePos DequeueActive()
    {
        var position = _activeCells.Dequeue();
        _activeCellSet.Remove(position);
        return position;
    }

    internal RegionSeedCursor DequeueSeedRegion()
    {
        return _seedRegions.Dequeue();
    }

    internal void RequeueSeedRegion(RegionSeedCursor cursor)
    {
        _seedRegions.Enqueue(cursor);
    }

    internal void CompleteSeedRegion(RectI region)
    {
        _seedRegionSet.Remove(region);
    }

    internal bool MarkChanged(TilePos position)
    {
        if (!_changedTiles.Add(position))
        {
            return false;
        }

        var chunk = CoordinateUtils.TileToChunk(position);
        var tileBounds = new RectI(position.X, position.Y, 1, 1);
        if (_changedChunkBounds.TryGetValue(chunk, out var existing))
        {
            _changedChunkBounds[chunk] = Union(existing, tileBounds);
        }
        else
        {
            _changedChunkBounds.Add(chunk, tileBounds);
        }

        return true;
    }

    internal IReadOnlyList<RectI> BuildChangedRegions()
    {
        foreach (var bounds in _changedChunkBounds.Values)
        {
            _changedRegions.Add(bounds.Inflate(1));
        }

        _changedRegions.Sort(RectPositionComparer.Instance);
        return _changedRegions;
    }

    internal int ChangedTileCount => _changedTiles.Count;

    internal (int Activations, int Regions) ConsumeDroppedCounts()
    {
        var result = (_droppedActivations, _droppedSeedRegions);
        _droppedActivations = 0;
        _droppedSeedRegions = 0;
        return result;
    }

    private static RectI Union(RectI left, RectI right)
    {
        return RectI.FromInclusiveTileBounds(
            Math.Min(left.Left, right.Left),
            Math.Min(left.Top, right.Top),
            Math.Max(left.Right, right.Right) - 1,
            Math.Max(left.Bottom, right.Bottom) - 1);
    }

    internal struct RegionSeedCursor
    {
        public RegionSeedCursor(RectI region)
        {
            Region = region;
            X = region.Left;
            Y = region.Bottom - 1;
        }

        public RectI Region { get; }

        public int X { get; private set; }

        public int Y { get; private set; }

        public bool MoveNext()
        {
            if (X < Region.Right - 1)
            {
                X++;
                return true;
            }

            X = Region.Left;
            Y--;
            return Y >= Region.Top;
        }
    }

    private sealed class RectPositionComparer : IComparer<RectI>
    {
        public static RectPositionComparer Instance { get; } = new();

        public int Compare(RectI left, RectI right)
        {
            var y = left.Y.CompareTo(right.Y);
            return y != 0 ? y : left.X.CompareTo(right.X);
        }
    }
}
