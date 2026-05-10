using Game.Core.Data;
using Game.Core.Maps;
using Xunit;

namespace Game.Tests.MapTests;

public sealed class MapDefinitionJsonLoaderTests
{
    [Fact]
    public void JsonLoader_ReadsLayersObjectsAndSpawnPoints()
    {
        var map = new MapDefinitionJsonLoader().LoadDefinitionFromJson("""
        {
          "id": "farm",
          "displayName": "Farm",
          "widthTiles": 3,
          "heightTiles": 2,
          "layers": [
            {
              "id": "ground",
              "kind": "Ground",
              "width": 3,
              "height": 2,
              "tiles": [1,1,1,1,1,1]
            }
          ],
          "objects": [
            {
              "id": "door",
              "kind": "Warp",
              "tileX": 1,
              "tileY": 0,
              "targetMapId": "house",
              "targetSpawnId": "entry"
            }
          ],
          "spawnPoints": [
            { "id": "home", "tileX": 1, "tileY": 1, "facing": "up" }
          ],
          "tags": ["TopDown", " Farm "]
        }
        """);

        Assert.Equal("farm", map.Id);
        Assert.Equal(3, map.WidthTiles);
        Assert.Single(map.Layers);
        Assert.Equal(1, map.Layers[0].GetTileId(2, 1));
        var warp = Assert.Single(map.Objects);
        Assert.Equal(MapObjectKind.Warp, warp.Kind);
        Assert.Equal("house", warp.TargetMapId);
        Assert.True(map.TryGetSpawn("home", out var spawn));
        Assert.Equal("up", spawn.Facing);
        Assert.True(map.HasTag("topdown"));
    }

    [Fact]
    public void Registry_RejectsInvalidLayerDataLength()
    {
        var map = new MapDefinition
        {
            Id = "bad",
            DisplayName = "Bad",
            WidthTiles = 2,
            HeightTiles = 2,
            Layers = new[]
            {
                new MapTileLayerDefinition
                {
                    Id = "ground",
                    Kind = MapLayerKind.Ground,
                    Width = 2,
                    Height = 2,
                    Tiles = new[] { 1, 1, 1 }
                }
            }
        };

        Assert.Throws<RegistryValidationException>(() => MapRegistry.Create(new[] { map }));
    }
}
