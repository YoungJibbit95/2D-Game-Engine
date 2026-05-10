using Game.Core.Farming;
using Game.Core.Inventory;
using Game.Core.Items;
using Game.Core.Tiles;
using Game.Core.World;
using Xunit;

namespace Game.Tests.FarmingTests;

public sealed class FarmingSystemTests
{
    [Fact]
    public void TillPlantWaterAdvanceAndHarvest_ProducesCropItem()
    {
        var world = CreateWorld();
        world.SetTile(2, 2, KnownTileIds.Dirt);
        var plots = new FarmPlotManager();
        var farming = new FarmingSystem();
        var inventory = CreateInventory();
        inventory.AddItem(new ItemStack("parsnip_seeds", 1));

        var tilled = farming.Till(world, CreateTiles(), plots, new TilePos(2, 2));
        var planted = farming.PlantSeed(world, CreateCrops(), plots, inventory, new TilePos(2, 2), "parsnip_seeds", currentDay: 1, FarmSeason.Spring);
        var wateredDayOne = farming.Water(world, plots, new TilePos(2, 2));
        var dayOne = farming.AdvanceDay(CreateCrops(), plots, FarmSeason.Spring);
        var wateredDayTwo = farming.Water(world, plots, new TilePos(2, 2));
        var dayTwo = farming.AdvanceDay(CreateCrops(), plots, FarmSeason.Spring);
        var harvested = farming.Harvest(CreateCrops(), plots, inventory, new TilePos(2, 2), new Random(1));

        Assert.Equal(FarmActionStatus.Completed, tilled.Status);
        Assert.Equal(FarmActionStatus.Completed, planted.Status);
        Assert.Equal(FarmActionStatus.Completed, wateredDayOne.Status);
        Assert.Equal(1, dayOne.AdvancedCrops);
        Assert.Equal(FarmActionStatus.Completed, wateredDayTwo.Status);
        Assert.Equal(1, dayTwo.NewlyMatureCrops);
        Assert.Equal(FarmActionStatus.Completed, harvested.Status);
        Assert.Equal(0, inventory.CountItem("parsnip_seeds"));
        Assert.Equal(1, inventory.CountItem("parsnip"));
        Assert.True(plots.TryGetPlot(new TilePos(2, 2), out var plot));
        Assert.Null(plot.Crop);
    }

    [Fact]
    public void PlantSeed_RejectsWrongSeasonAndDoesNotConsumeSeed()
    {
        var world = CreateWorld();
        world.SetTile(1, 1, KnownTileIds.Dirt);
        var plots = new FarmPlotManager();
        var farming = new FarmingSystem();
        var inventory = CreateInventory();
        inventory.AddItem(new ItemStack("parsnip_seeds", 1));
        farming.Till(world, CreateTiles(), plots, new TilePos(1, 1));

        var result = farming.PlantSeed(world, CreateCrops(), plots, inventory, new TilePos(1, 1), "parsnip_seeds", 1, FarmSeason.Winter);

        Assert.Equal(FarmActionStatus.WrongSeason, result.Status);
        Assert.Equal(1, inventory.CountItem("parsnip_seeds"));
    }

    [Fact]
    public void AdvanceDay_WithWrongSeasonWithersExistingCrop()
    {
        var world = CreateWorld();
        world.SetTile(1, 1, KnownTileIds.Dirt);
        var plots = new FarmPlotManager();
        var farming = new FarmingSystem();
        var inventory = CreateInventory();
        inventory.AddItem(new ItemStack("parsnip_seeds", 1));
        farming.Till(world, CreateTiles(), plots, new TilePos(1, 1));
        farming.PlantSeed(world, CreateCrops(), plots, inventory, new TilePos(1, 1), "parsnip_seeds", 1, FarmSeason.Spring);

        var result = farming.AdvanceDay(CreateCrops(), plots, FarmSeason.Winter);

        Assert.Equal(1, result.WitheredCrops);
        Assert.True(plots.TryGetPlot(new TilePos(1, 1), out var plot));
        Assert.Null(plot.Crop);
    }

    [Fact]
    public void Harvest_RegrowCropRestartsGrowthInsteadOfRemovingCrop()
    {
        var world = CreateWorld();
        world.SetTile(1, 1, KnownTileIds.Dirt);
        var plots = new FarmPlotManager();
        var farming = new FarmingSystem();
        var inventory = CreateInventory();
        inventory.AddItem(new ItemStack("bean_seeds", 1));
        farming.Till(world, CreateTiles(), plots, new TilePos(1, 1));
        farming.PlantSeed(world, CreateCrops(), plots, inventory, new TilePos(1, 1), "bean_seeds", 1, FarmSeason.Spring);
        farming.Water(world, plots, new TilePos(1, 1));
        farming.AdvanceDay(CreateCrops(), plots, FarmSeason.Spring);

        var harvested = farming.Harvest(CreateCrops(), plots, inventory, new TilePos(1, 1), new Random(1));

        Assert.Equal(FarmActionStatus.Completed, harvested.Status);
        Assert.True(plots.TryGetPlot(new TilePos(1, 1), out var plot));
        Assert.NotNull(plot.Crop);
        Assert.Equal(2, plot.Crop!.DaysUntilHarvest);
        Assert.Equal(1, inventory.CountItem("bean"));
    }

    private static World CreateWorld()
    {
        return new World(8, 8, WorldMetadata.CreateDefault(seed: 1));
    }

    private static TileRegistry CreateTiles()
    {
        return TileRegistry.Create(new[]
        {
            new TileDefinition
            {
                NumericId = KnownTileIds.Dirt,
                Id = "dirt",
                DisplayName = "Dirt",
                TexturePath = "tiles/dirt",
                Solid = false,
                BlocksLight = false,
                Hardness = 1,
                MergeGroup = "soil",
                Tags = new[] { "soil", "farmable" }
            }
        });
    }

    private static PlayerInventory CreateInventory()
    {
        return new PlayerInventory(CreateItems());
    }

    private static ItemRegistry CreateItems()
    {
        return ItemRegistry.Create(new[]
        {
            new ItemDefinition
            {
                Id = "parsnip_seeds",
                DisplayName = "Parsnip Seeds",
                Type = ItemType.Seed,
                TexturePath = "items/parsnip_seeds",
                MaxStack = 99
            },
            new ItemDefinition
            {
                Id = "parsnip",
                DisplayName = "Parsnip",
                Type = ItemType.Consumable,
                TexturePath = "items/parsnip",
                MaxStack = 99
            },
            new ItemDefinition
            {
                Id = "bean_seeds",
                DisplayName = "Bean Seeds",
                Type = ItemType.Seed,
                TexturePath = "items/bean_seeds",
                MaxStack = 99
            },
            new ItemDefinition
            {
                Id = "bean",
                DisplayName = "Bean",
                Type = ItemType.Consumable,
                TexturePath = "items/bean",
                MaxStack = 99
            }
        });
    }

    private static CropRegistry CreateCrops()
    {
        return CropRegistry.Create(new[]
        {
            new CropDefinition
            {
                Id = "parsnip",
                DisplayName = "Parsnip",
                TexturePath = "crops/parsnip",
                SeedItemId = "parsnip_seeds",
                HarvestItemId = "parsnip",
                GrowthStageDays = new[] { 1, 1 },
                Seasons = new[] { FarmSeason.Spring }
            },
            new CropDefinition
            {
                Id = "bean",
                DisplayName = "Bean",
                TexturePath = "crops/bean",
                SeedItemId = "bean_seeds",
                HarvestItemId = "bean",
                GrowthStageDays = new[] { 1 },
                RegrowDays = 2,
                Seasons = new[] { FarmSeason.Spring }
            }
        });
    }
}
