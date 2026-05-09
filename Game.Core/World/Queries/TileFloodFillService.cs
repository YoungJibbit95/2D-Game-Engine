namespace Game.Core.World.Queries;

public sealed class TileFloodFillService
{
    public IReadOnlyList<TilePos> FloodFill(
        World world,
        TilePos start,
        Func<TileInstance, bool> predicate,
        int maxTiles = 1024)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(predicate);

        if (maxTiles <= 0 || !world.IsInBounds(start.X, start.Y) || !predicate(world.GetTile(start.X, start.Y)))
        {
            return Array.Empty<TilePos>();
        }

        var results = new List<TilePos>();
        var open = new Queue<TilePos>();
        var visited = new HashSet<TilePos>();
        open.Enqueue(start);
        visited.Add(start);

        while (open.Count > 0 && results.Count < maxTiles)
        {
            var current = open.Dequeue();
            results.Add(current);

            Enqueue(current.X - 1, current.Y);
            Enqueue(current.X + 1, current.Y);
            Enqueue(current.X, current.Y - 1);
            Enqueue(current.X, current.Y + 1);
        }

        return results;

        void Enqueue(int x, int y)
        {
            var next = new TilePos(x, y);
            if (!world.IsInBounds(x, y) || !visited.Add(next) || !predicate(world.GetTile(x, y)))
            {
                return;
            }

            open.Enqueue(next);
        }
    }
}
