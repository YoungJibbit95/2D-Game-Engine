using Game.Core.World.Generation;
using Xunit;

namespace Game.Tests.WorldGenerationTests;

public sealed class StructureTemplateMaterializerTests
{
    [Fact]
    public void Loader_ValidatesAndLoadsMaterializedRows()
    {
        const string json = """
        {
          "id": "camp",
          "templateId": "camp_v1",
          "placement": "surface",
          "widthTiles": 3,
          "heightTiles": 2,
          "minTileY": 0,
          "maxTileY": 80,
          "legend": { "W": "wood", "_": "air" },
          "rows": ["W_W", "WWW"]
        }
        """;

        var definition = new StructurePlanDefinitionJsonLoader().LoadDefinitionFromJson(json);

        Assert.Equal(new[] { "W_W", "WWW" }, definition.Rows);
        Assert.Equal("wood", definition.Legend["W"]);
    }

    [Fact]
    public void Loader_RejectsUnknownTemplateSymbols()
    {
        const string json = """
        {
          "id": "broken",
          "templateId": "broken_v1",
          "placement": "surface",
          "widthTiles": 2,
          "heightTiles": 1,
          "minTileY": 0,
          "maxTileY": 80,
          "legend": { "W": "wood" },
          "rows": ["WX"]
        }
        """;

        Assert.Throws<InvalidDataException>(
            () => new StructurePlanDefinitionJsonLoader().LoadDefinitionFromJson(json));
    }

    [Fact]
    public void TryResolveTile_HandlesLongOriginsAndTransparentCells()
    {
        var structure = new PlannedStructure(
            "camp",
            "camp_v1",
            "surface",
            int.MinValue,
            40,
            3,
            2)
        {
            Rows = new[] { "W.W", "SSS" },
            Legend = new Dictionary<string, string> { ["W"] = "wood", ["S"] = "stone" }
        };

        Assert.True(StructureTemplateMaterializer.TryResolveTile(
            structure,
            int.MinValue,
            37,
            37,
            out var wood));
        Assert.Equal("wood", wood);
        Assert.False(StructureTemplateMaterializer.TryResolveTile(
            structure,
            (long)int.MinValue + 1,
            37,
            37,
            out _));
        Assert.True(StructureTemplateMaterializer.TryResolveTile(
            structure,
            (long)int.MinValue + 2,
            38,
            37,
            out var stone));
        Assert.Equal("stone", stone);
    }
}
