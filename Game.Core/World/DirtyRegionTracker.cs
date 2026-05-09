namespace Game.Core.World;

public sealed class DirtyRegionTracker
{
    private readonly List<RectI> _regions = new();

    public int Count => _regions.Count;

    public void Add(RectI region)
    {
        if (region.IsEmpty)
        {
            return;
        }

        _regions.Add(region);
    }

    public void AddRange(IEnumerable<RectI> regions)
    {
        ArgumentNullException.ThrowIfNull(regions);

        foreach (var region in regions)
        {
            Add(region);
        }
    }

    public void AddTile(TilePos position, int padding = 0)
    {
        var size = padding * 2 + 1;
        Add(new RectI(position.X - padding, position.Y - padding, size, size));
    }

    public void AddChunk(ChunkPos position, int paddingTiles = 0)
    {
        Add(CoordinateUtils.ChunkTileBounds(position).Inflate(paddingTiles));
    }

    public IReadOnlyList<RectI> PeekMerged()
    {
        return Merge(_regions);
    }

    public IReadOnlyList<RectI> DrainMerged()
    {
        var merged = Merge(_regions);
        _regions.Clear();
        return merged;
    }

    public void Clear()
    {
        _regions.Clear();
    }

    private static IReadOnlyList<RectI> Merge(IEnumerable<RectI> regions)
    {
        var merged = new List<RectI>();
        foreach (var region in regions)
        {
            var current = region;
            var didMerge = true;

            while (didMerge)
            {
                didMerge = false;
                for (var index = 0; index < merged.Count; index++)
                {
                    if (!TouchesOrIntersects(current, merged[index]))
                    {
                        continue;
                    }

                    current = Union(current, merged[index]);
                    merged.RemoveAt(index);
                    didMerge = true;
                    break;
                }
            }

            merged.Add(current);
        }

        return merged;
    }

    private static bool TouchesOrIntersects(RectI a, RectI b)
    {
        return a.Left <= b.Right &&
               a.Right >= b.Left &&
               a.Top <= b.Bottom &&
               a.Bottom >= b.Top;
    }

    private static RectI Union(RectI a, RectI b)
    {
        var left = Math.Min(a.Left, b.Left);
        var top = Math.Min(a.Top, b.Top);
        var right = Math.Max(a.Right, b.Right);
        var bottom = Math.Max(a.Bottom, b.Bottom);
        return new RectI(left, top, right - left, bottom - top);
    }
}
