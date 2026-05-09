using Game.Core.World.Generation;
using Xunit;

namespace Game.Tests.WorldTests;

public sealed class WorldGenerationProfileJsonLoaderTests
{
    [Fact]
    public void LoadProfileFromJson_LoadsProfile()
    {
        var profile = new WorldGenerationProfileJsonLoader().LoadProfileFromJson("""
        {
          "id": "tiny",
          "widthTiles": 96,
          "heightTiles": 64,
          "surfaceBaseY": 24,
          "surfaceAmplitude": 4,
          "dirtDepthMin": 4,
          "dirtDepthMax": 8,
          "caveWalkerCount": 2,
          "caveWalkLength": 16,
          "caveMinDepthOffset": 6,
          "caveMinRadius": 1,
          "caveMaxRadius": 3,
          "copperVeinCount": 3,
          "ironVeinCount": 2,
          "treeAttempts": 8,
          "treeAttemptChance": 0.75,
          "treeMinHeight": 3,
          "treeMaxHeight": 5,
          "waterPocketAttempts": 1,
          "dimensions": [
            {
              "id": "sky",
              "displayName": "Sky",
              "minTileY": 0,
              "maxTileYInclusive": 16,
              "surfaceTileId": 2,
              "subsurfaceTileId": 1,
              "fillTileId": 3,
              "ambientLight": 255
            }
          ],
          "ores": [
            { "tileId": 4, "veinCount": 3, "minDepthOffset": 8, "radius": 2, "replaceTileId": 3 }
          ]
        }
        """);

        Assert.Equal("tiny", profile.Id);
        Assert.Equal(96, profile.WidthTiles);
        Assert.Equal(1, profile.WaterPocketAttempts);
        Assert.Equal(6, profile.CaveMinDepthOffset);
        Assert.Equal(0.75f, profile.TreeAttemptChance);
        var ore = Assert.Single(profile.Ores);
        Assert.Equal(Game.Core.World.KnownTileIds.CopperOre, ore.TileId);
        Assert.Equal(Game.Core.World.KnownTileIds.Stone, ore.ReplaceTileId);
        var dimension = Assert.Single(profile.Dimensions);
        Assert.Equal("sky", dimension.Id);
        Assert.Equal(255, dimension.AmbientLight);
    }
}
