using Game.Core.Crafting;
using Game.Core.Inventory;
using Game.Core.Items;
using Game.Core.Tiles;
using Game.Core.World;
using Xunit;

namespace Game.Tests.CraftingTests;

public sealed class CraftingStationLocatorTests
{
    [Fact]
    public void FindStations_ReturnsStationsNearActor()
    {
        var world = new World(12, 8, WorldMetadata.CreateDefault(seed: 1));
        world.SetTile(5, 4, TileInstance.FromTileId(KnownTileIds.Workbench, isSolid: false));

        var stations = new CraftingStationLocator().FindStations(world, CreateTiles(), new TilePos(4, 4), radiusTiles: 2);

        Assert.Contains("workbench", stations);
    }

    [Fact]
    public void CreateContext_AllowsStationRecipeWhenStationIsNearby()
    {
        var world = new World(12, 8, WorldMetadata.CreateDefault(seed: 1));
        world.SetTile(5, 4, TileInstance.FromTileId(KnownTileIds.Workbench, isSolid: false));
        var items = CreateItems();
        var inventory = new PlayerInventory(items);
        inventory.AddItem(new ItemStack("wood", 10));
        var recipe = new RecipeDefinition
        {
            Id = "torch",
            Result = new ItemStack("torch", 3),
            Ingredients = new[] { new RecipeIngredient { ItemId = "wood", Count = 2 } },
            Station = "workbench"
        };
        var context = new CraftingStationLocator().CreateContext(
            inventory,
            world,
            CreateTiles(),
            new TilePos(4, 4),
            radiusTiles: 2);

        var query = Assert.Single(new CraftingSystem().QueryRecipes(context, RecipeRegistry.Create(new[] { recipe })));

        Assert.True(query.HasStation);
        Assert.True(query.CanCraft);
    }

    private static TileRegistry CreateTiles()
    {
        return TileRegistry.Create(new[]
        {
            new TileDefinition
            {
                NumericId = KnownTileIds.Workbench,
                Id = "workbench",
                DisplayName = "Workbench",
                TexturePath = "tiles/workbench",
                Solid = false,
                BlocksLight = false,
                Hardness = 1,
                MiningPowerRequired = 0,
                DropItemId = "workbench",
                CraftingStationId = "workbench"
            }
        });
    }

    private static ItemRegistry CreateItems()
    {
        return ItemRegistry.Create(new[]
        {
            new ItemDefinition
            {
                Id = "wood",
                DisplayName = "Wood",
                Type = ItemType.Material,
                TexturePath = "items/wood",
                MaxStack = 999
            },
            new ItemDefinition
            {
                Id = "torch",
                DisplayName = "Torch",
                Type = ItemType.PlaceableTile,
                TexturePath = "items/torch",
                MaxStack = 999
            }
        });
    }
}
