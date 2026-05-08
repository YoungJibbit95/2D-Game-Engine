using Game.Core.Data;
using Game.Core.Items;
using Xunit;

namespace Game.Tests.InventoryTests;

public sealed class ItemRegistryTests
{
    [Fact]
    public void Create_MapsItemsCaseInsensitively()
    {
        var registry = ItemRegistry.Create(new[] { CreateDirtBlockDefinition() });

        Assert.Equal("dirt_block", registry.GetById("DIRT_BLOCK").Id);
    }

    [Fact]
    public void Create_RejectsDuplicateIds()
    {
        var first = CreateDirtBlockDefinition();
        var second = CreateDirtBlockDefinition();

        Assert.Throws<RegistryValidationException>(() => ItemRegistry.Create(new[] { first, second }));
    }

    [Fact]
    public void Loader_ReadsItemDefinitionJson()
    {
        const string json = """
        {
          "id": "copper_pickaxe",
          "displayName": "Copper Pickaxe",
          "type": "ToolPickaxe",
          "texture": "items/copper_pickaxe",
          "maxStack": 1,
          "useTime": 0.35,
          "toolPower": 35,
          "damage": 3,
          "knockback": 1.5
        }
        """;

        var loader = new ItemDefinitionJsonLoader();

        var definition = loader.LoadDefinitionFromJson(json);

        Assert.Equal("copper_pickaxe", definition.Id);
        Assert.Equal(ItemType.ToolPickaxe, definition.Type);
        Assert.Equal("items/copper_pickaxe", definition.TexturePath);
        Assert.Equal(35, definition.ToolPower);
    }

    private static ItemDefinition CreateDirtBlockDefinition()
    {
        return new ItemDefinition
        {
            Id = "dirt_block",
            DisplayName = "Dirt Block",
            Type = ItemType.PlaceableTile,
            TexturePath = "items/dirt_block",
            MaxStack = 999,
            PlacesTileId = "dirt"
        };
    }
}
