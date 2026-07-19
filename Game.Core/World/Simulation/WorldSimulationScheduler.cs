using Game.Core.World.Liquids;

namespace Game.Core.World.Simulation;

public sealed class WorldSimulationScheduler
{
    private readonly DirtyRegionTracker _liquidRegions = new();
    private readonly DirtyRegionTracker _renderRegions = new();
    private readonly DirtyRegionTracker _lightRegions = new();
    private readonly List<RectI> _drainedLiquidRegions = new();
    private float _liquidAccumulator;
    private bool _seededExistingLiquids;
    private World? _boundWorld;

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
        BindWorld(world);

        if (world.IsHorizontallyInfinite)
        {
            foreach (var chunk in world.Chunks.Values)
            {
                var bounds = world.ClampRegionToBounds(CoordinateUtils.ChunkTileBounds(chunk.Position));
                MarkLiquidRegion(bounds, padding);
            }
        }
        else
        {
            MarkLiquidRegion(new RectI(0, 0, world.WidthTiles, world.HeightTiles), padding);
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
        BindWorld(world);
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
        if (ShouldStepLiquids(options, liquids))
        {
            _liquidAccumulator = Math.Max(0, _liquidAccumulator - options.LiquidStepIntervalSeconds);
            var regions = DrainLiquidRegions(world, options.LiquidRegionPaddingTiles);
            processedLiquidRegions = regions.Count;

            liquid = regions.Count > 0
                ? liquids.Step(world, regions, options.LiquidOptions)
                : liquids.Step(world, options.LiquidOptions);
            if (liquid.ChangedRegions.Count > 0)
            {
                for (var index = 0; index < liquid.ChangedRegions.Count; index++)
                {
                    var region = liquid.ChangedRegions[index];
                    _liquidRegions.Add(region.Inflate(options.LiquidRegionPaddingTiles));
                    _renderRegions.Add(region);
                    _lightRegions.Add(region);
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
        _boundWorld = null;
    }

    private bool ShouldStepLiquids(WorldSimulationOptions options, LiquidSimulationSystem liquids)
    {
        var hasWork = _liquidRegions.Count > 0 || liquids.HasPendingWork;
        if (options.LiquidStepIntervalSeconds <= 0)
        {
            return hasWork;
        }

        return _liquidAccumulator >= options.LiquidStepIntervalSeconds && hasWork;
    }

    private IReadOnlyList<RectI> DrainLiquidRegions(World world, int padding)
    {
        _drainedLiquidRegions.Clear();
        var merged = _liquidRegions.DrainMerged();
        for (var index = 0; index < merged.Count; index++)
        {
            var clamped = world.ClampRegionToBounds(merged[index].Inflate(padding));
            if (!clamped.IsEmpty)
            {
                _drainedLiquidRegions.Add(clamped);
            }
        }

        return _drainedLiquidRegions;
    }

    private void BindWorld(World world)
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

        Clear();
        _boundWorld = world;
    }
}
