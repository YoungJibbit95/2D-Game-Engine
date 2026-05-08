using Game.Core.World;
using Xunit;

namespace Game.Tests.WorldTests;

public sealed class CoordinateUtilsTests
{
    [Theory]
    [InlineData(0f, 0f, 0, 0)]
    [InlineData(15.99f, 15.99f, 0, 0)]
    [InlineData(16f, 16f, 1, 1)]
    [InlineData(-0.01f, -0.01f, -1, -1)]
    [InlineData(-16f, -16f, -1, -1)]
    [InlineData(-16.01f, -16.01f, -2, -2)]
    public void WorldToTile_UsesFloorForPositiveAndNegativePixels(float pixelX, float pixelY, int expectedX, int expectedY)
    {
        var tile = CoordinateUtils.WorldToTile(pixelX, pixelY);

        Assert.Equal(new TilePos(expectedX, expectedY), tile);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(31, 0)]
    [InlineData(32, 1)]
    [InlineData(63, 1)]
    [InlineData(64, 2)]
    [InlineData(-1, -1)]
    [InlineData(-32, -1)]
    [InlineData(-33, -2)]
    public void TileToChunk_UsesFloorDivisionForNegativeTiles(int tileX, int expectedChunkX)
    {
        var chunk = CoordinateUtils.TileToChunk(tileX, 0);

        Assert.Equal(new ChunkPos(expectedChunkX, 0), chunk);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(31, 31)]
    [InlineData(32, 0)]
    [InlineData(33, 1)]
    [InlineData(-1, 31)]
    [InlineData(-32, 0)]
    [InlineData(-33, 31)]
    public void LocalTileInChunk_WrapsNegativeTilesIntoPositiveLocalRange(int tileX, int expectedLocalX)
    {
        var local = CoordinateUtils.LocalTileInChunk(tileX, 0);

        Assert.Equal(new TilePos(expectedLocalX, 0), local);
    }
}
