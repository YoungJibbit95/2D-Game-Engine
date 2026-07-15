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

    [Fact]
    public void EnumerateChunksOverlapping_ReturnsAllTouchedChunksForNegativeRegions()
    {
        var chunks = CoordinateUtils
            .EnumerateChunksOverlapping(new RectI(-1, 31, 34, 2))
            .OrderBy(chunk => chunk.Y)
            .ThenBy(chunk => chunk.X)
            .ToArray();

        Assert.Equal(
            new[]
            {
                new ChunkPos(-1, 0),
                new ChunkPos(0, 0),
                new ChunkPos(1, 0),
                new ChunkPos(-1, 1),
                new ChunkPos(0, 1),
                new ChunkPos(1, 1)
            },
            chunks);
    }

    [Fact]
    public void ExtremeCoordinatesUseSaturatingConversionsInsteadOfWrapping()
    {
        Assert.Equal(int.MaxValue, CoordinateUtils.WorldToTile(float.PositiveInfinity, 0).X);
        Assert.Equal(int.MinValue, CoordinateUtils.WorldToTile(float.NegativeInfinity, 0).X);
        Assert.Equal(0, CoordinateUtils.WorldToTile(float.NaN, 0).X);

        var maximumBounds = CoordinateUtils.ChunkTileBounds(new ChunkPos(int.MaxValue, 0));
        var minimumBounds = CoordinateUtils.ChunkTileBounds(new ChunkPos(int.MinValue, 0));
        Assert.Equal(int.MaxValue, maximumBounds.Right);
        Assert.Equal(int.MinValue, minimumBounds.Left);
        Assert.True(maximumBounds.Width > 0);
        Assert.True(minimumBounds.Width > 0);
    }
}
