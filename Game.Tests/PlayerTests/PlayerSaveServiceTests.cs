using Game.Core.Inventory;
using Game.Core.Items;
using Game.Core.Saving;
using Xunit;

namespace Game.Tests.PlayerTests;

public sealed class PlayerSaveServiceTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), "terraria-like-player-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void SaveAndLoad_RoundTripsPlayerData()
    {
        var path = Path.Combine(_tempDirectory, "player_001.json");
        var data = new PlayerSaveData
        {
            PlayerId = "player_001",
            DisplayName = "Builder",
            PositionX = 100,
            PositionY = 200,
            Health = 75,
            MaxHealth = 100,
            Mana = 20,
            SelectedHotbarSlot = 3,
            InventorySlots = new[]
            {
                new ItemStack("dirt_block", 12),
                ItemStack.Empty,
                new ItemStack("copper_pickaxe", 1)
            }
        };

        var service = new PlayerSaveService();
        service.Save(data, path);

        var loaded = service.Load(path);

        Assert.Equal(data.PlayerId, loaded.PlayerId);
        Assert.Equal(data.DisplayName, loaded.DisplayName);
        Assert.Equal(data.PositionX, loaded.PositionX);
        Assert.Equal(data.InventorySlots[0], loaded.InventorySlots[0]);
        Assert.Equal(data.SelectedHotbarSlot, loaded.SelectedHotbarSlot);
    }

    [Fact]
    public void ToInventory_RecreatesSlotContents()
    {
        var data = new PlayerSaveData
        {
            PlayerId = "player_001",
            DisplayName = "Builder",
            PositionX = 0,
            PositionY = 0,
            Health = 100,
            MaxHealth = 100,
            SelectedHotbarSlot = 0,
            InventorySlots = new[]
            {
                new ItemStack("dirt_block", 12),
                ItemStack.Empty
            }
        };

        var inventory = new PlayerSaveService().ToInventory(data, CreateItems());

        Assert.Equal(new ItemStack("dirt_block", 12), inventory.Slots[0].Stack);
        Assert.True(inventory.Slots[1].IsEmpty);
    }

    [Fact]
    public void ToPlayerInventory_RehydratesHotbarAndMainInventory()
    {
        var slots = Enumerable.Repeat(ItemStack.Empty, PlayerInventory.HotbarSlotCount + PlayerInventory.MainSlotCount).ToArray();
        slots[2] = new ItemStack("dirt_block", 8);
        slots[PlayerInventory.HotbarSlotCount] = new ItemStack("stone_block", 4);
        var data = new PlayerSaveData
        {
            PlayerId = "player_001",
            DisplayName = "Builder",
            PositionX = 0,
            PositionY = 0,
            Health = 100,
            MaxHealth = 100,
            SelectedHotbarSlot = 2,
            InventorySlots = slots
        };

        var inventory = new PlayerSaveService().ToPlayerInventory(data, CreateItems());

        Assert.Equal(2, inventory.SelectedHotbarSlot);
        Assert.Equal(new ItemStack("dirt_block", 8), inventory.Hotbar.Slots[2].Stack);
        Assert.Equal(new ItemStack("stone_block", 4), inventory.Main.Slots[0].Stack);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    private static ItemRegistry CreateItems()
    {
        return ItemRegistry.Create(new[]
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
        });
    }
}
