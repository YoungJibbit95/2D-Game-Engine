using Game.Client.UI;
using Microsoft.Xna.Framework;
using Xunit;

namespace Game.Tests.ClientUiTests;

public sealed class AdventureMenuSceneTests
{
    [Theory]
    [InlineData(640, 360, false, false)]
    [InlineData(900, 500, true, false)]
    [InlineData(1280, 720, true, true)]
    [InlineData(2560, 1440, true, true)]
    public void Plan_EnablesComplexLayersOnlyWhenViewportCanCarryThem(
        int width,
        int height,
        bool expectedFloatingIslands,
        bool expectedEdgeSettlements)
    {
        var viewport = new Rectangle(0, 0, width, height);

        var layout = AdventureMenuScene.Plan(viewport);

        AssertContained(viewport, layout.Sky);
        AssertContained(viewport, layout.Valley);
        AssertContained(viewport, layout.Ground);
        AssertContained(viewport, layout.Soil);
        AssertContained(viewport, layout.SafeContent);
        Assert.Equal(layout.Sky.Bottom, layout.Valley.Y);
        Assert.Equal(layout.Valley.Bottom, layout.Ground.Y);
        Assert.Equal(layout.Ground.Bottom, layout.Soil.Y);
        Assert.Equal(viewport.Bottom, layout.Soil.Bottom);
        Assert.Equal(expectedFloatingIslands, layout.ShowFloatingIslands);
        Assert.Equal(expectedEdgeSettlements, layout.ShowEdgeSettlements);

        if (expectedFloatingIslands)
        {
            AssertIsland(viewport, layout.Ground.Y, layout.FloatingIslandA);
            AssertIsland(viewport, layout.Ground.Y, layout.FloatingIslandB);
            AssertIsland(viewport, layout.Ground.Y, layout.FloatingIslandC);
        }
        else
        {
            Assert.Equal(Rectangle.Empty, layout.FloatingIslandA);
            Assert.Equal(Rectangle.Empty, layout.FloatingIslandB);
            Assert.Equal(Rectangle.Empty, layout.FloatingIslandC);
        }

        if (expectedEdgeSettlements)
        {
            AssertContained(viewport, layout.LeftSettlement);
            AssertContained(viewport, layout.RightSettlement);
            Assert.Equal(layout.Ground.Y, layout.LeftSettlement.Bottom);
            Assert.Equal(layout.Ground.Y, layout.RightSettlement.Bottom);
        }
        else
        {
            Assert.Equal(Rectangle.Empty, layout.LeftSettlement);
            Assert.Equal(Rectangle.Empty, layout.RightSettlement);
        }
    }

    [Fact]
    public void Plan_RespectsOffsetViewport()
    {
        var viewport = new Rectangle(37, 53, 1920, 1080);

        var layout = AdventureMenuScene.Plan(viewport);

        AssertContained(viewport, layout.SafeContent);
        AssertContained(viewport, layout.LeftSettlement);
        AssertContained(viewport, layout.RightSettlement);
        AssertIsland(viewport, layout.Ground.Y, layout.FloatingIslandA);
        AssertIsland(viewport, layout.Ground.Y, layout.FloatingIslandB);
        AssertIsland(viewport, layout.Ground.Y, layout.FloatingIslandC);
        Assert.Equal(viewport.X, layout.Sky.X);
        Assert.Equal(viewport.Right, layout.Soil.Right);
    }

    [Fact]
    public void Plan_AllocatesZeroBytesAfterWarmup()
    {
        var viewport = new Rectangle(0, 0, 1920, 1080);
        _ = AdventureMenuScene.Plan(viewport);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var checksum = 0;

        for (var index = 0; index < 10_000; index++)
        {
            var layout = AdventureMenuScene.Plan(viewport);
            checksum += layout.SafeContent.Width;
            checksum += layout.FloatingIslandB.Height;
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(checksum > 0);
        Assert.Equal(0, allocated);
    }

    private static void AssertIsland(Rectangle viewport, int groundY, Rectangle island)
    {
        AssertContained(viewport, island);
        Assert.Equal(groundY, island.Bottom);
        Assert.True(island.Width >= 84);
        Assert.True(island.Height > island.Width);
    }

    private static void AssertContained(Rectangle outer, Rectangle inner)
    {
        Assert.True(
            inner.X >= outer.X && inner.Y >= outer.Y && inner.Right <= outer.Right && inner.Bottom <= outer.Bottom,
            $"Expected {inner} to be contained by {outer}.");
    }
}
