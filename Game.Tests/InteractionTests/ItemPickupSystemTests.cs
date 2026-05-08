using Game.Core.Entities;
using Game.Core.Interaction;
using Game.Core.Inventory;
using Game.Core.Items;
using Game.Core.Physics;
using Game.Core.World;
using System.Numerics;
using Xunit;

namespace Game.Tests.InteractionTests;

public sealed class ItemPickupSystemTests
{
    [Fact]
    public void PickupItems_AddsDroppedItemToInventoryAndDeactivatesEntity()
    {
        var entities = new EntityManager(spatialCellSize: 16);
        var droppedItem = new DroppedItemEntity(new ItemStack("dirt_block", 3), Vector2.Zero, new TileCollisionResolver());
        entities.Add(droppedItem);
        var inventory = CreateInventory(2);

        var pickedUp = new ItemPickupSystem().PickupItems(entities, inventory, new RectI(-4, -4, 20, 20));

        Assert.Equal(1, pickedUp);
        Assert.Equal(3, inventory.CountItem("dirt_block"));
        Assert.False(droppedItem.IsActive);
    }

    [Fact]
    public void PickupItems_LeavesItemWhenInventoryIsFull()
    {
        var entities = new EntityManager(spatialCellSize: 16);
        var droppedItem = new DroppedItemEntity(new ItemStack("dirt_block", 3), Vector2.Zero, new TileCollisionResolver());
        entities.Add(droppedItem);
        var inventory = CreateInventory(1);
        inventory.AddItem(new ItemStack("stone_block", 999));

        var pickedUp = new ItemPickupSystem().PickupItems(entities, inventory, new RectI(-4, -4, 20, 20));

        Assert.Equal(0, pickedUp);
        Assert.True(droppedItem.IsActive);
    }

    private static Inventory CreateInventory(int slots)
    {
        return new Inventory(slots, ItemRegistry.Create(new[]
        {
            new ItemDefinition
            {
                Id = "dirt_block",
                DisplayName = "Dirt Block",
                Type = ItemType.PlaceableTile,
                TexturePath = "items/dirt_block",
                MaxStack = 999
            },
            new ItemDefinition
            {
                Id = "stone_block",
                DisplayName = "Stone Block",
                Type = ItemType.PlaceableTile,
                TexturePath = "items/stone_block",
                MaxStack = 999
            }
        }));
    }
}
