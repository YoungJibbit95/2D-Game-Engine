using Game.Core.World.Generation;
using Xunit;

namespace Game.Tests.WorldTests;

public sealed class WorldGenerationProfileJsonLoaderTests
{
    [Theory]
    [InlineData("small.json", 4, 3, 10)]
    [InlineData("medium.json", 8, 5, 20)]
    [InlineData("large.json", 14, 8, 34)]
    public void LoadProfileFromFile_LoadsTunedDataProfiles(
        string fileName,
        int cavernRooms,
        int surfaceLakes,
        int cavePools)
    {
        var dataDirectory = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "Game.Data",
            "worldgen"));

        var profile = new WorldGenerationProfileJsonLoader().LoadProfileFromFile(Path.Combine(dataDirectory, fileName));

        Assert.Equal(cavernRooms, profile.CavernRoomCount);
        Assert.Equal(surfaceLakes, profile.SurfaceLakeAttempts);
        Assert.Equal(cavePools, profile.CavePoolAttempts);
        Assert.NotEqual((ushort)0, profile.DirtWallId);
        Assert.NotEqual((ushort)0, profile.StoneWallId);
    }

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
          "cavernRoomCount": 2,
          "cavernMinRadiusX": 5,
          "cavernMaxRadiusX": 9,
          "cavernMinRadiusY": 3,
          "cavernMaxRadiusY": 6,
          "copperVeinCount": 3,
          "ironVeinCount": 2,
          "treeAttempts": 8,
          "treeAttemptChance": 0.75,
          "treeMinHeight": 3,
          "treeMaxHeight": 5,
          "waterPocketAttempts": 1,
          "surfaceLakeAttempts": 1,
          "surfaceLakeMinWidth": 9,
          "surfaceLakeMaxWidth": 15,
          "surfaceLakeMinDepth": 2,
          "surfaceLakeMaxDepth": 5,
          "cavePoolAttempts": 1,
          "cavePoolMinWidth": 7,
          "cavePoolMaxWidth": 13,
          "cavePoolMinDepth": 2,
          "cavePoolMaxDepth": 4,
          "dirtWallId": 3,
          "stoneWallId": 4,
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
        Assert.Equal(2, profile.CavernRoomCount);
        Assert.Equal(1, profile.SurfaceLakeAttempts);
        Assert.Equal(1, profile.CavePoolAttempts);
        Assert.Equal((ushort)4, profile.StoneWallId);
        Assert.Equal(0.75f, profile.TreeAttemptChance);
        var ore = Assert.Single(profile.Ores);
        Assert.Equal(Game.Core.World.KnownTileIds.CopperOre, ore.TileId);
        Assert.Equal(Game.Core.World.KnownTileIds.Stone, ore.ReplaceTileId);
        var dimension = Assert.Single(profile.Dimensions);
        Assert.Equal("sky", dimension.Id);
        Assert.Equal(255, dimension.AmbientLight);
    }

    [Fact]
    public void LoadProfileFromJson_RejectsInvalidMilestoneRanges()
    {
        var exception = Assert.Throws<InvalidDataException>(() =>
            new WorldGenerationProfileJsonLoader().LoadProfileFromJson("""
            {
              "id": "invalid",
              "widthTiles": 96,
              "heightTiles": 64,
              "surfaceBaseY": 24,
              "surfaceAmplitude": 4,
              "dirtDepthMin": 4,
              "dirtDepthMax": 8,
              "caveWalkerCount": 2,
              "caveWalkLength": 16,
              "cavernRoomCount": 2,
              "cavernMinRadiusX": 12,
              "cavernMaxRadiusX": 6,
              "surfaceLakeAttempts": 1,
              "surfaceLakeMinWidth": 18,
              "surfaceLakeMaxWidth": 10,
              "caveWallCoverageChance": 1.5,
              "dirtWallId": 0
            }
            """));

        Assert.Contains("CavernMinRadiusX/CavernMaxRadiusX", exception.Message);
        Assert.Contains("SurfaceLakeMinWidth/SurfaceLakeMaxWidth", exception.Message);
        Assert.Contains("CaveWallCoverageChance", exception.Message);
        Assert.Contains("DirtWallId", exception.Message);
    }
}
