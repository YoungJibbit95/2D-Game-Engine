using Game.Core.World.Liquids;

namespace Game.Core.World.Simulation;

public sealed class WorldSimulationScheduler
{
    private readonly DirtyRegionTracker _liquidRegions = new();
    private readonly DirtyRegionTracker _renderRegions = new();
    private readonly DirtyRegionTracker _lightRegions = new();
    private float _liquidAccumulator;
    private bool _seededExistingLiquids;

    public int PendingLiquidRegionCount => _liquidRegions.Count;

    public int PendingRenderRegionCount => _renderRegions.Count;

    public int PendingLightRegionCount => _lightRegions.Count;

    public void MarkTileChanged(TilePos position, int padding = 1)
    {
        _renderRegions.AddTile(position, padding);
        _lightRegions.AddTile(position, padding);
        _liquidRegions.AddTile(position, padding);
    }

    public void MarkRegionChanged(RectI region, int padding = 1)
    {
        var dirty = region.Inflate(padding);
        _renderRegions.Add(dirty);
        _lightRegions.Add(dirty);
        _liquidRegions.Add(dirty);
    }

    public void MarkLiquidRegion(RectI region, int padding = 1)
    {
        _liquidRegions.Add(region.Inflate(padding));
    }

    public void MarkLiquidTile(TilePos position, int padding = 1)
    {
        _liquidRegions.AddTile(position, padding);
    }

    public void MarkRenderRegion(RectI region)
    {
        _renderRegions.Add(region);
    }

    public void MarkLightRegion(RectI region)
    {
        _lightRegions.Add(region);
    }

    public void SeedExistingLiquids(World world, int padding = 1)
    {
        ArgumentNullException.ThrowIfNull(world);

        for (var y = 0; y < world.HeightTiles; y++)
        {
            for (var x = 0; x < world.WidthTiles; x++)
            {
                if (world.GetTile(x, y).HasLiquid)
                {
                    MarkLiquidTile(new TilePos(x, y), padding);
                }
            }
        }

        _seededExistingLiquids = true;
    }

    public WorldSimulationTickResult Tick(
        World world,
        float deltaSeconds,
        LiquidSimulationSystem liquids,
        WorldSimulationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(liquids);
        options ??= WorldSimulationOptions.Default;

        if (options.SeedExistingLiquids && !_seededExistingLiquids)
        {
            SeedExistingLiquids(world, options.LiquidRegionPaddingTiles);
        }

        if (deltaSeconds > 0)
        {
            _liquidAccumulator += deltaSeconds;
        }

        var liquid = LiquidSimulationResult.None;
        var processedLiquidRegions = 0;
        if (ShouldStepLiquids(options))
        {
            _liquidAccumulator = Math.Max(0, _liquidAccumulator - options.LiquidStepIntervalSeconds);
            var regions = DrainLiquidRegions(world, options.LiquidRegionPaddingTiles);
            processedLiquidRegions = regions.Count;

            if (regions.Count > 0)
            {
                liquid = liquids.Step(world, regions, options.LiquidOptions);
                if (liquid.ChangedRegions.Count > 0)
                {
                    _liquidRegions.AddRange(liquid.ChangedRegions.Select(region => region.Inflate(options.LiquidRegionPaddingTiles)));
                    _renderRegions.AddRange(liquid.ChangedRegions);
                    _lightRegions.AddRange(liquid.ChangedRegions);
                }
            }
        }

        return new WorldSimulationTickResult(
            liquid,
            processedLiquidRegions,
            _renderRegions.DrainMerged(),
            _lightRegions.DrainMerged(),
            _liquidRegions.PeekMerged());
    }

    public void Clear()
    {
        _liquidRegions.Clear();
        _renderRegions.Clear();
        _lightRegions.Clear();
        _liquidAccumulator = 0;
        _seededExistingLiquids = false;
    }

    private bool ShouldStepLiquids(WorldSimulationOptions options)
    {
        if (options.LiquidStepIntervalSeconds <= 0)
        {
            return _liquidRegions.Count > 0;
        }

        return _liquidAccumulator >= options.LiquidStepIntervalSeconds && _liquidRegions.Count > 0;
    }

    private IReadOnlyList<RectI> DrainLiquidRegions(World world, int padding)
    {
        var bounds = new RectI(0, 0, world.WidthTiles, world.HeightTiles);
        return _liquidRegions
            .DrainMerged()
            .Select(region => region.Inflate(padding).ClampTo(bounds))
            .Where(region => !region.IsEmpty)
            .ToArray();
    }
}
