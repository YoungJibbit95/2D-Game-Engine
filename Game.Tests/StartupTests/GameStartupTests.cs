using Game.Core.Data;
using Game.Core.Inventory;
using Game.Core.Items;
using Game.Core.Startup;
using Xunit;

namespace Game.Tests.StartupTests;

public sealed class GameStartupTests
{
    [Fact]
    public void Loader_ReadsGameStartupDefinitionJson()
    {
        var startup = new GameStartupDefinitionJsonLoader().LoadDefinitionFromJson("""
        {
          "id": "default",
          "displayName": "Default Start",
          "worldProfileId": "small",
          "startupMapId": "farm",
          "selectedHotbarSlot": 2,
          "tags": ["starter"],
          "starterItems": [
            { "itemId": "pickaxe", "count": 1, "target": "Hotbar", "slot": 0, "sortOrder": 10 },
            { "itemId": "seed", "count": 12, "target": "Main", "sortOrder": 20 }
          ]
        }
        """);

        Assert.Equal("default", startup.Id);
        Assert.Equal("small", startup.WorldProfileId);
        Assert.Equal("farm", startup.StartupMapId);
        Assert.Equal(2, startup.SelectedHotbarSlot);
        Assert.True(startup.HasTag("starter"));
        Assert.Equal(2, startup.StarterItems.Count);
        Assert.Equal(StarterInventoryTarget.Hotbar, startup.StarterItems[0].Target);
    }

    [Fact]
    public void Registry_RejectsDuplicateTargetedSlots()
    {
        var startup = new GameStartupDefinition
        {
            Id = "bad",
            DisplayName = "Bad",
            StarterItems = new[]
            {
                new StarterItemDefinition { ItemId = "one", Count = 1, Target = StarterInventoryTarget.Hotbar, Slot = 0 },
                new StarterItemDefinition { ItemId = "two", Count = 1, Target = StarterInventoryTarget.Hotbar, Slot = 0 }
            }
        };

        Assert.Throws<RegistryValidationException>(() => GameStartupRegistry.Create(new[] { startup }));
    }

    [Fact]
    public void InventoryService_PlacesTargetedAndAutomaticStarterItems()
    {
        var startup = new GameStartupDefinition
        {
            Id = "default",
            DisplayName = "Default",
            SelectedHotbarSlot = 1,
            StarterItems = new[]
            {
                new StarterItemDefinition { ItemId = "pickaxe", Count = 1, Target = StarterInventoryTarget.Hotbar, Slot = 0, SortOrder = 10 },
                new StarterItemDefinition { ItemId = "seed", Count = 12, Target = StarterInventoryTarget.Hotbar, Slot = 3, SortOrder = 20 },
                new StarterItemDefinition { ItemId = "stone", Count = 25, Target = StarterInventoryTarget.Main, SortOrder = 30 },
                new StarterItemDefinition { ItemId = "coin", Count = 5, SortOrder = 40 }
            }
        };

        var result = new GameStartupInventoryService().BuildPlayerInventory(CreateItems(), startup);

        Assert.True(result.Success);
        Assert.Equal(4, result.AppliedItems.Count);
        Assert.Equal(1, result.Inventory.SelectedHotbarSlot);
        Assert.Equal("pickaxe", result.Inventory.Hotbar.Slots[0].Stack.ItemId);
        Assert.Equal(12, result.Inventory.Hotbar.Slots[3].Stack.Count);
        Assert.Equal(25, result.Inventory.CountItem("stone"));
        Assert.Equal(5, result.Inventory.CountItem("coin"));
    }

    [Fact]
    public void InventoryService_ReportsExactSlotStackOverflow()
    {
        var startup = new GameStartupDefinition
        {
            Id = "bad",
            DisplayName = "Bad",
            StarterItems = new[]
            {
                new StarterItemDefinition { ItemId = "pickaxe", Count = 2, Target = StarterInventoryTarget.Hotbar, Slot = 0 }
            }
        };

        var result = new GameStartupInventoryService().BuildPlayerInventory(CreateItems(), startup);

        Assert.False(result.Success);
        var failed = Assert.Single(result.FailedItems);
        Assert.Equal("stack_exceeds_slot_limit", failed.Reason);
    }

    private static ItemRegistry CreateItems()
    {
        return ItemRegistry.Create(new[]
        {
            CreateItem("pickaxe", 1),
            CreateItem("seed", 99),
            CreateItem("stone", 999),
            CreateItem("coin", 999)
        });
    }

    private static ItemDefinition CreateItem(string id, int maxStack)
    {
        return new ItemDefinition
        {
            Id = id,
            DisplayName = id,
            Type = ItemType.Material,
            TexturePath = $"items/{id}",
            MaxStack = maxStack
        };
    }
}
