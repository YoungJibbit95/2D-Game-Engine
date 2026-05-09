using Game.Core.Interaction;
using Game.Core.Inventory;
using Game.Core.Items;
using Game.Core.Tiles;
using Game.Core.World;
using System.Numerics;
using Xunit;

namespace Game.Tests.InteractionTests;

public sealed class MiningAndBuildingTests
{
    [Fact]
    public void MiningSystem_RemovesTileAndReturnsDropAfterProgressCompletes()
    {
        var world = CreateWorld();
        world.SetTile(2, 2, KnownTileIds.Dirt);
        var mining = new MiningSystem();

        var result = mining.Update(world, CreateTiles(), new TilePos(2, 2), new Vector2(40, 40), 96, 0, 2f);

        Assert.True(result.Completed);
        Assert.True(world.GetTile(2, 2).IsAir);
        Assert.Equal(new ItemStack("dirt_block", 1), result.DroppedItem);
    }

    [Fact]
    public void MiningSystem_DoesNotMineOutsideReach()
    {
        var world = CreateWorld();
        world.SetTile(7, 7, KnownTileIds.Dirt);

        var result = new MiningSystem().Update(world, CreateTiles(), new TilePos(7, 7), Vector2.Zero, 16, 0, 10f);

        Assert.False(result.Completed);
        Assert.False(world.GetTile(7, 7).IsAir);
    }

    [Fact]
    public void BuildingSystem_PlacesTileAndConsumesItem()
    {
        var world = CreateWorld();
        var inventory = CreateInventory();
        inventory.AddItem(new ItemStack("dirt_block", 2));

        var placed = new BuildingSystem().PlaceTile(
            world,
            inventory,
            CreateItems(),
            CreateTiles(),
            new TilePos(3, 3),
            "dirt_block",
            new Vector2(40, 40),
            96,
            new RectI(0, 0, 12, 28));

        Assert.True(placed);
        Assert.Equal(KnownTileIds.Dirt, world.GetTile(3, 3).TileId);
        Assert.Equal(1, inventory.CountItem("dirt_block"));
    }

    [Fact]
    public void BuildingSystem_RejectsPlacementInsideActorBounds()
    {
        var world = CreateWorld();
        var inventory = CreateInventory();
        inventory.AddItem(new ItemStack("dirt_block", 1));

        var placed = new BuildingSystem().PlaceTile(
            world,
            inventory,
            CreateItems(),
            CreateTiles(),
            new TilePos(0, 0),
            "dirt_block",
            Vector2.Zero,
            96,
            new RectI(0, 0, 16, 16));

        Assert.False(placed);
        Assert.True(world.GetTile(0, 0).IsAir);
    }

    [Fact]
    public void BuildingSystem_RequiresAdjacentSolidSupportWhenConfigured()
    {
        var world = CreateWorld();
        var inventory = CreateInventory();
        inventory.AddItem(new ItemStack("dirt_block", 2));
        var items = CreateItems(PlacementSupportRule.AdjacentSolid);

        var unsupported = new BuildingSystem().PlaceTile(
            world,
            inventory,
            items,
            CreateTiles(),
            new TilePos(4, 4),
            "dirt_block",
            new Vector2(64, 64),
            96,
            new RectI(0, 0, 12, 28));

        world.SetTile(3, 4, KnownTileIds.Stone);
        var supported = new BuildingSystem().PlaceTile(
            world,
            inventory,
            items,
            CreateTiles(),
            new TilePos(4, 4),
            "dirt_block",
            new Vector2(64, 64),
            96,
            new RectI(0, 0, 12, 28));

        Assert.False(unsupported);
        Assert.True(supported);
        Assert.Equal(KnownTileIds.Dirt, world.GetTile(4, 4).TileId);
    }

    [Fact]
    public void BuildingSystem_UsesTileDefinitionSolidityWhenPlacing()
    {
        var world = CreateWorld();
        world.SetTile(4, 4, KnownTileIds.Stone);
        var inventory = new Inventory(4, CreateItemsForWorkbench());
        inventory.AddItem(new ItemStack("workbench", 1));

        var placed = new BuildingSystem().PlaceTile(
            world,
            inventory,
            CreateItemsForWorkbench(),
            CreateTilesWithWorkbench(),
            new TilePos(4, 3),
            "workbench",
            new Vector2(64, 32),
            96,
            new RectI(0, 0, 12, 28));

        Assert.True(placed);
        Assert.Equal(KnownTileIds.Workbench, world.GetTile(4, 3).TileId);
        Assert.False(world.GetTile(4, 3).IsSolid);
    }

    private static World CreateWorld()
    {
        return new World(16, 16, WorldMetadata.CreateDefault(seed: 1));
    }

    private static Inventory CreateInventory()
    {
        return new Inventory(4, CreateItems());
    }

    private static ItemRegistry CreateItems(PlacementSupportRule placementSupport = PlacementSupportRule.None)
    {
        return ItemRegistry.Create(new[]
        {
            new ItemDefinition
            {
                Id = "dirt_block",
                DisplayName = "Dirt Block",
                Type = ItemType.PlaceableTile,
                TexturePath = "items/dirt_block",
                MaxStack = 999,
                PlacesTileId = "dirt",
                PlacementSupport = placementSupport
            }
        });
    }

    private static TileRegistry CreateTiles()
    {
        return TileRegistry.Create(new[]
        {
            new TileDefinition
            {
                NumericId = KnownTileIds.Stone,
                Id = "stone",
                DisplayName = "Stone",
                TexturePath = "tiles/stone",
                Solid = true,
                BlocksLight = true,
                Hardness = 1,
                MiningPowerRequired = 0,
                DropItemId = "stone_block"
            },
            new TileDefinition
            {
                NumericId = KnownTileIds.Dirt,
                Id = "dirt",
                DisplayName = "Dirt",
                TexturePath = "tiles/dirt",
                Solid = true,
                BlocksLight = true,
                Hardness = 1,
                MiningPowerRequired = 0,
                DropItemId = "dirt_block"
            }
        });
    }

    private static ItemRegistry CreateItemsForWorkbench()
    {
        return ItemRegistry.Create(new[]
        {
            new ItemDefinition
            {
                Id = "workbench",
                DisplayName = "Workbench",
                Type = ItemType.PlaceableTile,
                TexturePath = "items/workbench",
                MaxStack = 99,
                PlacesTileId = "workbench",
                PlacementSupport = PlacementSupportRule.OnSolidGround
            }
        });
    }

    private static TileRegistry CreateTilesWithWorkbench()
    {
        return TileRegistry.Create(new[]
        {
            new TileDefinition
            {
                NumericId = KnownTileIds.Stone,
                Id = "stone",
                DisplayName = "Stone",
                TexturePath = "tiles/stone",
                Solid = true,
                BlocksLight = true,
                Hardness = 1,
                MiningPowerRequired = 0,
                DropItemId = "stone_block"
            },
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
}
