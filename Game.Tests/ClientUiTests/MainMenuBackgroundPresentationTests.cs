using Game.Client.UI;
using Microsoft.Xna.Framework;
using Xunit;

namespace Game.Tests.ClientUiTests;

public sealed class MainMenuBackgroundPresentationTests
{
    [Theory]
    [InlineData(1920, 1080)]
    [InlineData(1280, 720)]
    [InlineData(640, 360)]
    public void Resolve_PreservesFullSourceForMatchingWideViewport(int width, int height)
    {
        var viewport = new Rectangle(0, 0, width, height);

        var placement = MainMenuBackgroundCropPlanner.Resolve(viewport, new Point(1672, 941));

        Assert.Equal(viewport, placement.Destination);
        Assert.Equal(0, placement.Source.X);
        Assert.Equal(1672, placement.Source.Width);
        Assert.InRange(placement.Source.Y, 0, 1);
        Assert.InRange(placement.Source.Height, 939, 941);
    }

    [Fact]
    public void Resolve_CropsSidesForFourByThreeViewport()
    {
        var viewport = new Rectangle(31, 47, 1024, 768);

        var placement = MainMenuBackgroundCropPlanner.Resolve(viewport, new Point(1672, 941));

        Assert.Equal(viewport, placement.Destination);
        Assert.Equal(941, placement.Source.Height);
        Assert.True(placement.Source.X > 0);
        Assert.True(placement.Source.Width < 1672);
        Assert.InRange(
            Math.Abs((1672 - placement.Source.Width) - placement.Source.X * 2),
            0,
            1);
    }

    [Fact]
    public void Resolve_CropsTopAndBottomForUltrawideViewport()
    {
        var viewport = new Rectangle(0, 0, 3440, 1440);

        var placement = MainMenuBackgroundCropPlanner.Resolve(viewport, new Point(1672, 941));

        Assert.Equal(0, placement.Source.X);
        Assert.Equal(1672, placement.Source.Width);
        Assert.True(placement.Source.Y > 0);
        Assert.True(placement.Source.Height < 941);
    }

    [Fact]
    public void Resolve_AllocatesZeroBytesAfterWarmup()
    {
        var viewport = new Rectangle(0, 0, 1920, 1080);
        var textureSize = new Point(1672, 941);
        _ = MainMenuBackgroundCropPlanner.Resolve(viewport, textureSize);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var checksum = 0;

        for (var index = 0; index < 10_000; index++)
        {
            checksum += MainMenuBackgroundCropPlanner.Resolve(viewport, textureSize).Source.Width;
        }

        Assert.True(checksum > 0);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
}
