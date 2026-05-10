using Game.Core.Inventory;
using Game.Core.Tiles;
using Game.Core.World;

namespace Game.Core.Farming;

public sealed class FarmingSystem
{
    public FarmActionResult Till(World.World world, TileRegistry tiles, FarmPlotManager plots, TilePos position)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(tiles);
        ArgumentNullException.ThrowIfNull(plots);

        if (!world.IsInBounds(position.X, position.Y))
        {
            return FarmActionResult.Failed(FarmActionStatus.OutOfBounds, position);
        }

        var tile = world.GetTile(position.X, position.Y);
        var definition = tiles.GetByNumericId(tile.TileId);
        if (!IsTillable(definition))
        {
            return FarmActionResult.Failed(FarmActionStatus.NotTillable, position);
        }

        var plot = plots.GetOrCreatePlot(position);
        if (plot.IsTilled)
        {
            return FarmActionResult.Failed(FarmActionStatus.AlreadyTilled, position);
        }

        plot.IsTilled = true;
        plot.IsWatered = false;
        return FarmActionResult.Completed(position);
    }

    public FarmActionResult Water(World.World world, FarmPlotManager plots, TilePos position)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(plots);

        if (!world.IsInBounds(position.X, position.Y))
        {
            return FarmActionResult.Failed(FarmActionStatus.OutOfBounds, position);
        }

        if (!plots.TryGetPlot(position, out var plot) || !plot.IsTilled)
        {
            return FarmActionResult.Failed(FarmActionStatus.NotTilled, position);
        }

        plot.IsWatered = true;
        return FarmActionResult.Completed(position);
    }

    public FarmActionResult PlantSeed(
        World.World world,
        CropRegistry crops,
        FarmPlotManager plots,
        PlayerInventory inventory,
        TilePos position,
        string seedItemId,
        int currentDay,
        FarmSeason season)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(crops);
        ArgumentNullException.ThrowIfNull(plots);
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentException.ThrowIfNullOrWhiteSpace(seedItemId);

        if (!world.IsInBounds(position.X, position.Y))
        {
            return FarmActionResult.Failed(FarmActionStatus.OutOfBounds, position);
        }

        if (!crops.TryGetBySeedItemId(seedItemId, out var crop))
        {
            return FarmActionResult.Failed(FarmActionStatus.UnknownCrop, position);
        }

        if (!crop.CanGrowIn(season))
        {
            return FarmActionResult.Failed(FarmActionStatus.WrongSeason, position, crop.Id);
        }

        if (!plots.TryGetPlot(position, out var plot) || !plot.IsTilled)
        {
            return FarmActionResult.Failed(FarmActionStatus.NotTilled, position, crop.Id);
        }

        if (plot.HasCrop)
        {
            return FarmActionResult.Failed(FarmActionStatus.AlreadyOccupied, position, crop.Id);
        }

        if (!inventory.RemoveItem(seedItemId, 1))
        {
            return FarmActionResult.Failed(FarmActionStatus.MissingSeed, position, crop.Id);
        }

        plot.Crop = new CropInstance(crop.Id, currentDay, crop.TotalGrowthDays);
        return FarmActionResult.Completed(position, cropId: crop.Id);
    }

    public FarmActionResult Harvest(CropRegistry crops, FarmPlotManager plots, PlayerInventory inventory, TilePos position, Random? random = null)
    {
        ArgumentNullException.ThrowIfNull(crops);
        ArgumentNullException.ThrowIfNull(plots);
        ArgumentNullException.ThrowIfNull(inventory);

        if (!plots.TryGetPlot(position, out var plot) || plot.Crop is null)
        {
            return FarmActionResult.Failed(FarmActionStatus.NoCrop, position);
        }

        if (!crops.TryGetById(plot.Crop.CropId, out var crop))
        {
            return FarmActionResult.Failed(FarmActionStatus.UnknownCrop, position, plot.Crop.CropId);
        }

        if (!plot.Crop.IsMature)
        {
            return FarmActionResult.Failed(FarmActionStatus.CropNotMature, position, crop.Id);
        }

        var yield = crop.BaseYield + RollExtraYield(crop, random);
        var harvest = new ItemStack(crop.HarvestItemId, yield);
        if (!inventory.AddItem(harvest))
        {
            return FarmActionResult.Failed(FarmActionStatus.InventoryFull, position, crop.Id);
        }

        if (crop.RegrowDays > 0)
        {
            plot.Crop.RestartRegrow(crop.RegrowDays);
        }
        else
        {
            plot.Crop.RecordFinalHarvest();
            plot.Crop = null;
        }

        return FarmActionResult.Completed(position, harvest, crop.Id);
    }

    public FarmDailyTickResult AdvanceDay(CropRegistry crops, FarmPlotManager plots, FarmSeason season)
    {
        ArgumentNullException.ThrowIfNull(crops);
        ArgumentNullException.ThrowIfNull(plots);

        var advanced = 0;
        var matured = 0;
        var watered = 0;
        var withered = 0;

        foreach (var plot in plots.Plots)
        {
            if (plot.IsWatered)
            {
                watered++;
            }

            if (plot.Crop is not null)
            {
                if (!crops.TryGetById(plot.Crop.CropId, out var crop) || !crop.CanGrowIn(season))
                {
                    plot.Crop = null;
                    withered++;
                }
                else if (!crop.RequiresWater || plot.IsWatered)
                {
                    var wasMature = plot.Crop.IsMature;
                    plot.Crop.AdvanceGrowthDay();
                    advanced++;
                    if (!wasMature && plot.Crop.IsMature)
                    {
                        matured++;
                    }
                }
            }

            plot.IsWatered = false;
        }

        plots.ClearEmptyUntilledPlots();
        return new FarmDailyTickResult(advanced, matured, watered, withered);
    }

    private static bool IsTillable(TileDefinition definition)
    {
        return definition.HasTag("farmable") ||
               definition.HasTag("soil") ||
               string.Equals(definition.MergeGroup, "soil", StringComparison.OrdinalIgnoreCase);
    }

    private static int RollExtraYield(CropDefinition crop, Random? random)
    {
        if (crop.ExtraYieldChancePercent <= 0)
        {
            return 0;
        }

        var rng = random ?? Random.Shared;
        return rng.Next(0, 100) < crop.ExtraYieldChancePercent ? 1 : 0;
    }
}
