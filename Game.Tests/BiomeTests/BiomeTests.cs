using Game.Core.Biomes;
using Xunit;

namespace Game.Tests.BiomeTests;

public sealed class BiomeTests
{
    [Fact]
    public void Loader_ReadsBiomeJson()
    {
        const string json = """
        {
          "id": "forest",
          "displayName": "Forest",
          "surfaceTile": "grass",
          "undergroundTile": "dirt",
          "treeType": "normal_tree",
          "enemySpawnTable": "forest_day"
        }
        """;

        var biome = new BiomeJsonLoader().LoadDefinitionFromJson(json);

        Assert.Equal("forest", biome.Id);
        Assert.Equal("grass", biome.SurfaceTile);
        Assert.Equal("forest_day", biome.EnemySpawnTable);
    }

    [Fact]
    public void BiomeMap_ReturnsMostRecentMatchingRegion()
    {
        var map = new BiomeMap("forest");
        map.AddRegion(10, 20, "desert");
        map.AddRegion(15, 18, "oasis");

        Assert.Equal("forest", map.GetBiomeAt(5, 0));
        Assert.Equal("desert", map.GetBiomeAt(12, 0));
        Assert.Equal("oasis", map.GetBiomeAt(16, 0));
    }
}
