using Game.Core.Data;
using Game.Core.Farming;
using Xunit;

namespace Game.Tests.FarmingTests;

public sealed class CropRegistryTests
{
    [Fact]
    public void JsonLoader_ReadsCropDefinition()
    {
        var crop = new CropDefinitionJsonLoader().LoadDefinitionFromJson("""
        {
          "id": "parsnip",
          "displayName": "Parsnip",
          "texture": "crops/parsnip",
          "seedItem": "parsnip_seeds",
          "harvestItem": "parsnip",
          "baseYield": 2,
          "extraYieldChancePercent": 25,
          "regrowDays": 0,
          "requiresWater": true,
          "growthStageDays": [1, 2, 1],
          "seasons": ["Spring"],
          "tags": ["Vegetable", " spring "]
        }
        """);

        Assert.Equal("parsnip", crop.Id);
        Assert.Equal("parsnip_seeds", crop.SeedItemId);
        Assert.Equal(4, crop.TotalGrowthDays);
        Assert.True(crop.CanGrowIn(FarmSeason.Spring));
        Assert.False(crop.CanGrowIn(FarmSeason.Winter));
        Assert.True(crop.HasTag("vegetable"));
    }

    [Fact]
    public void Registry_RejectsDuplicateSeedItems()
    {
        var definitions = new[]
        {
            CreateCrop("parsnip", "seed"),
            CreateCrop("bean", "seed")
        };

        Assert.Throws<RegistryValidationException>(() => CropRegistry.Create(definitions));
    }

    private static CropDefinition CreateCrop(string id, string seedItem)
    {
        return new CropDefinition
        {
            Id = id,
            DisplayName = id,
            TexturePath = $"crops/{id}",
            SeedItemId = seedItem,
            HarvestItemId = $"{id}_item",
            GrowthStageDays = new[] { 1, 1 }
        };
    }
}
