using Game.Client.UI;
using Microsoft.Xna.Framework;
using Xunit;

namespace Game.Tests.ClientUiTests;

public sealed class PixelMenuSceneTests
{
    [Theory]
    [InlineData(640, 360, false, false)]
    [InlineData(1280, 720, true, true)]
    [InlineData(2560, 1440, true, true)]
    public void Plan_ContainsSceneLayersAndEnablesDetailsByViewport(
        int width,
        int height,
        bool expectedLargeTree,
        bool expectedCampfire)
    {
        var viewport = new Rectangle(0, 0, width, height);

        var layout = PixelMenuScene.Plan(viewport);

        AssertContained(viewport, layout.Sky);
        AssertContained(viewport, layout.Horizon);
        AssertContained(viewport, layout.Ground);
        AssertContained(viewport, layout.Soil);
        AssertContained(viewport, layout.SafeContent);
        Assert.Equal(layout.Sky.Bottom, layout.Horizon.Y);
        Assert.Equal(layout.Horizon.Bottom, layout.Ground.Y);
        Assert.Equal(layout.Ground.Bottom, layout.Soil.Y);
        Assert.Equal(viewport.Bottom, layout.Soil.Bottom);
        Assert.Equal(expectedLargeTree, layout.ShowLargeTree);
        Assert.Equal(expectedCampfire, layout.ShowCampfire);
    }

    [Fact]
    public void Plan_RespectsOffsetViewportAndMaintainsUsefulSafeContent()
    {
        var viewport = new Rectangle(31, 47, 960, 540);

        var layout = PixelMenuScene.Plan(viewport);

        AssertContained(viewport, layout.SafeContent);
        Assert.True(layout.SafeContent.Width >= viewport.Width * 4 / 5);
        Assert.True(layout.SafeContent.Height >= viewport.Height * 4 / 5);
        Assert.Equal(viewport.X, layout.Sky.X);
        Assert.Equal(viewport.Right, layout.Soil.Right);
    }

    private static void AssertContained(Rectangle outer, Rectangle inner)
    {
        Assert.True(
            inner.X >= outer.X && inner.Y >= outer.Y && inner.Right <= outer.Right && inner.Bottom <= outer.Bottom,
            $"Expected {inner} to be contained by {outer}.");
    }
}
