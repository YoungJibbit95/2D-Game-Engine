using Game.Core.Items;
using Game.Core.Tiles;
using Xunit;

namespace Game.Tests.DataTests;

public sealed class DefinitionTagTests
{
    [Fact]
    public void TileJsonLoader_NormalizesTags()
    {
        var tile = new TileDefinitionJsonLoader().LoadDefinitionFromJson("""
        {
          "id": "dirt",
          "numericId": 1,
          "displayName": "Dirt",
          "texture": "tiles/dirt",
          "tags": [" Soil ", "soil", "Natural"]
        }
        """);

        Assert.Equal(new[] { "natural", "soil" }, tile.Tags);
        Assert.True(tile.HasTag("SOIL"));
    }

    [Fact]
    public void ItemJsonLoader_LoadsTagsAndPlacementSupport()
    {
        var item = new ItemDefinitionJsonLoader().LoadDefinitionFromJson("""
        {
          "id": "torch",
          "displayName": "Torch",
          "type": "PlaceableTile",
          "texture": "items/torch",
          "placesTile": "torch",
          "placementSupport": "AdjacentSolidOrWall",
          "tags": ["light", "Placeable"]
        }
        """);

        Assert.Equal(PlacementSupportRule.AdjacentSolidOrWall, item.PlacementSupport);
        Assert.True(item.HasTag("placeable"));
        Assert.True(item.HasTag("LIGHT"));
    }
}
