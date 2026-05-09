using Game.Core.Assets;
using Game.Core.Data;
using Xunit;

namespace Game.Tests.AssetTests;

public sealed class SpriteAssetTests
{
    [Fact]
    public void Loader_ReadsSpriteManifest()
    {
        const string json = """
        {
          "sprites": [
            {
              "id": "items/copper_pickaxe",
              "path": "sprites/tools/copper_pickaxe.png",
              "category": "Tool",
              "width": 16,
              "height": 16,
              "tags": ["tool", "pickaxe"]
            }
          ]
        }
        """;

        var definition = Assert.Single(new SpriteAssetJsonLoader().LoadDefinitionsFromJson(json));

        Assert.Equal("items/copper_pickaxe", definition.Id);
        Assert.Equal(SpriteAssetCategory.Tool, definition.Category);
        Assert.True(definition.HasTag("pickaxe"));
    }

    [Fact]
    public void Registry_RejectsDuplicateAssetIds()
    {
        var definitions = new[]
        {
            CreateSprite("items/wood"),
            CreateSprite("items/wood")
        };

        Assert.Throws<RegistryValidationException>(() => SpriteAssetRegistry.Create(definitions));
    }

    private static SpriteAssetDefinition CreateSprite(string id)
    {
        return new SpriteAssetDefinition
        {
            Id = id,
            Path = $"sprites/{id}.png",
            Category = SpriteAssetCategory.Item,
            Width = 16,
            Height = 16
        };
    }
}
