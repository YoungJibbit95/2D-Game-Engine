namespace Game.Core.World.Liquids;

public sealed class LiquidSimulationSystem
{
    private readonly HashSet<TilePos> _changedTiles = new();

    public LiquidSimulationResult Step(World world, RectI tileRegion, LiquidSimulationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(world);

        if (tileRegion.IsEmpty)
        {
            return LiquidSimulationResult.None;
        }

        options ??= LiquidSimulationOptions.Default;

        var minX = Math.Max(0, tileRegion.Left);
        var maxX = Math.Min(world.WidthTiles - 1, tileRegion.Right - 1);
        var minY = Math.Max(0, tileRegion.Top);
        var maxY = Math.Min(world.HeightTiles - 1, tileRegion.Bottom - 1);

        if (minX > maxX || minY > maxY)
        {
            return LiquidSimulationResult.None;
        }

        var changedTiles = 0;
        var movedLiquid = 0;
        _changedTiles.Clear();

        for (var y = maxY; y >= minY; y--)
        {
            for (var x = minX; x <= maxX; x++)
            {
                if (!world.GetTile(x, y).HasLiquid)
                {
                    continue;
                }

                movedLiquid += TryFlowDown(world, x, y, options, ref changedTiles);
                movedLiquid += TryFlowSideways(world, x, y, options, ref changedTiles);
            }
        }

        var result = new LiquidSimulationResult(changedTiles, movedLiquid, BuildChangedRegions());
        _changedTiles.Clear();
        return result;
    }

    public LiquidSimulationResult Step(World world, IEnumerable<RectI> tileRegions, LiquidSimulationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(tileRegions);

        var total = LiquidSimulationResult.None;
        foreach (var region in tileRegions)
        {
            total = total.Add(Step(world, region, options));
        }

        return total;
    }

    private int TryFlowDown(World world, int x, int y, LiquidSimulationOptions options, ref int changedTiles)
    {
        var belowY = y + 1;
        if (!world.IsInBounds(x, belowY))
        {
            return 0;
        }

        return Transfer(world, x, y, x, belowY, options.MaxLiquid, options, ref changedTiles);
    }

    private int TryFlowSideways(World world, int x, int y, LiquidSimulationOptions options, ref int changedTiles)
    {
        var source = world.GetTile(x, y);
        if (!source.HasLiquid)
        {
            return 0;
        }

        var moved = 0;
        var firstDirection = ((x + y) & 1) == 0 ? -1 : 1;
        moved += TryBalanceSide(world, x, y, firstDirection, options, ref changedTiles);
        moved += TryBalanceSide(world, x, y, -firstDirection, options, ref changedTiles);
        return moved;
    }

    private int TryBalanceSide(
        World world,
        int x,
        int y,
        int direction,
        LiquidSimulationOptions options,
        ref int changedTiles)
    {
        var targetX = x + direction;
        if (!world.IsInBounds(targetX, y))
        {
            return 0;
        }

        var source = world.GetTile(x, y);
        var target = world.GetTile(targetX, y);
        if (!CanContainLiquid(target))
        {
            return 0;
        }

        var difference = source.LiquidAmount - target.LiquidAmount;
        if (difference <= options.MinimumHorizontalDifference)
        {
            return 0;
        }

        var requested = Math.Min(options.MaxHorizontalFlow, difference / 2);
        return Transfer(world, x, y, targetX, y, requested, options, ref changedTiles);
    }

    private int Transfer(
        World world,
        int sourceX,
        int sourceY,
        int targetX,
        int targetY,
        int requestedAmount,
        LiquidSimulationOptions options,
        ref int changedTiles)
    {
        var source = world.GetTile(sourceX, sourceY);
        var target = world.GetTile(targetX, targetY);
        if (!source.HasLiquid || !CanContainLiquid(target) || requestedAmount <= 0)
        {
            return 0;
        }

        var capacity = options.MaxLiquid - target.LiquidAmount;
        var moved = Math.Min(Math.Min(source.LiquidAmount, requestedAmount), capacity);
        if (moved <= 0)
        {
            return 0;
        }

        world.SetTile(sourceX, sourceY, WithLiquid(source, source.LiquidAmount - moved));
        world.SetTile(targetX, targetY, WithLiquid(target, target.LiquidAmount + moved));
        changedTiles += MarkChanged(new TilePos(sourceX, sourceY), new TilePos(targetX, targetY));
        return moved;
    }

    private static bool CanContainLiquid(TileInstance tile)
    {
        return !tile.IsSolid;
    }

    private static TileInstance WithLiquid(TileInstance tile, int amount)
    {
        var clamped = (byte)Math.Clamp(amount, 0, byte.MaxValue);
        tile.LiquidAmount = clamped;

        if (clamped == 0)
        {
            tile.Flags &= ~TileFlags.HasLiquid;
        }
        else
        {
            tile.Flags |= TileFlags.HasLiquid;
        }

        return tile;
    }

    private int MarkChanged(TilePos source, TilePos target)
    {
        var added = 0;
        if (_changedTiles.Add(source))
        {
            added++;
        }

        if (_changedTiles.Add(target))
        {
            added++;
        }

        return added;
    }

    private IReadOnlyList<RectI> BuildChangedRegions()
    {
        if (_changedTiles.Count == 0)
        {
            return Array.Empty<RectI>();
        }

        var tracker = new DirtyRegionTracker();
        foreach (var position in _changedTiles)
        {
            tracker.AddTile(position, padding: 1);
        }

        return tracker.DrainMerged();
    }
}
