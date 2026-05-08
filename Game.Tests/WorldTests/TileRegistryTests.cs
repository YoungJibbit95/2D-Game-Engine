using Game.Core.Data;
using Game.Core.Tiles;
using Xunit;

namespace Game.Tests.WorldTests;

public sealed class TileRegistryTests
{
    [Fact]
    public void Create_MapsTilesByTextAndNumericIds()
    {
        var registry = TileRegistry.Create(new[] { CreateDirtDefinition() });

        Assert.Equal("dirt", registry.GetByNumericId(1).Id);
        Assert.Equal((ushort)1, registry.GetById("DIRT").NumericId);
    }

    [Fact]
    public void Create_RejectsDuplicateNumericIds()
    {
        var first = CreateDirtDefinition() with { Id = "dirt" };
        var second = CreateDirtDefinition() with { Id = "mud" };

        Assert.Throws<RegistryValidationException>(() => TileRegistry.Create(new[] { first, second }));
    }

    [Fact]
    public void Create_RejectsDuplicateStringIds()
    {
        var first = CreateDirtDefinition() with { NumericId = 1 };
        var second = CreateDirtDefinition() with { NumericId = 2 };

        Assert.Throws<RegistryValidationException>(() => TileRegistry.Create(new[] { first, second }));
    }

    [Fact]
    public void GetById_ReturnsFallbackForMissingTile()
    {
        var registry = TileRegistry.Create(Array.Empty<TileDefinition>());

        var tile = registry.GetById("does_not_exist");

        Assert.Equal("missing_tile", tile.Id);
    }

    [Fact]
    public void Loader_ReadsCamelCaseJsonDefinition()
    {
        const string json = """
        {
          "id": "stone",
          "numericId": 3,
          "displayName": "Stone Block",
          "texture": "tiles/stone",
          "solid": true,
          "blocksLight": true,
          "hardness": 2.5,
          "miningPowerRequired": 0,
          "dropItem": "stone_block",
          "mergeGroup": "stone"
        }
        """;

        var loader = new TileDefinitionJsonLoader();

        var definition = loader.LoadDefinitionFromJson(json);

        Assert.Equal((ushort)3, definition.NumericId);
        Assert.Equal("stone", definition.Id);
        Assert.Equal("tiles/stone", definition.TexturePath);
        Assert.True(definition.Solid);
        Assert.Equal("stone_block", definition.DropItemId);
    }

    private static TileDefinition CreateDirtDefinition()
    {
        return new TileDefinition
        {
            NumericId = 1,
            Id = "dirt",
            DisplayName = "Dirt Block",
            TexturePath = "tiles/dirt",
            Solid = true,
            BlocksLight = true,
            Hardness = 1,
            MiningPowerRequired = 0,
            DropItemId = "dirt_block",
            MergeGroup = "soil"
        };
    }
}
