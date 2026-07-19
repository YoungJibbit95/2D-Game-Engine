using Game.Client.UI;
using Microsoft.Xna.Framework;
using Xunit;

namespace Game.Tests.ClientUiTests;

public sealed class AdaptiveOverlayLayoutTests
{
    [Theory]
    [InlineData(120, 120)]
    [InlineData(320, 180)]
    [InlineData(640, 360)]
    [InlineData(1280, 720)]
    [InlineData(2560, 1440)]
    public void HudLayout_ContainsEverySurfaceAndHotbarSlot(int width, int height)
    {
        var viewport = new Rectangle(0, 0, width, height);

        var layout = PixelHudLayoutPlanner.Resolve(viewport);

        AssertContained(viewport, layout.HotbarDock);
        AssertContained(viewport, layout.ResourcePanel);
        AssertContained(layout.ResourcePanel, layout.HealthMeter);
        AssertContained(layout.ResourcePanel, layout.ManaMeter);
        AssertContained(layout.ResourcePanel, layout.GuardMeter);
        AssertContained(layout.ResourcePanel, layout.AttackMeter);
        for (var index = 0; index < layout.SlotCount; index++)
        {
            AssertContained(layout.HotbarDock, layout.HotbarSlot(index));
            if (index > 0)
            {
                Assert.Equal(layout.SlotGap, layout.HotbarSlot(index).X - layout.HotbarSlot(index - 1).Right);
            }
        }

        Assert.Equal(Rectangle.Empty, layout.HotbarSlot(-1));
        Assert.Equal(Rectangle.Empty, layout.HotbarSlot(layout.SlotCount));
    }

    [Fact]
    public void HudLayout_UsesCompactAndExpandedDensityWithoutOverflow()
    {
        var compact = PixelHudLayoutPlanner.Resolve(new Rectangle(0, 0, 480, 270));
        var expanded = PixelHudLayoutPlanner.Resolve(new Rectangle(0, 0, 2560, 1440));

        Assert.Equal(PixelUiDensity.Compact, compact.Density);
        Assert.Equal(PixelUiDensity.Expanded, expanded.Density);
        Assert.True(compact.SlotSize < expanded.SlotSize);
        Assert.True(compact.HotbarDock.Width < expanded.HotbarDock.Width);
        Assert.Equal(10, compact.SlotCount);
    }

    [Theory]
    [InlineData(360, true, false, PixelUiDensity.Compact)]
    [InlineData(1280, true, true, PixelUiDensity.Regular)]
    [InlineData(1280, false, false, PixelUiDensity.Regular)]
    public void PerformanceLayout_AdaptsColumnsToAvailableWidth(
        int width,
        bool allocationsRequested,
        bool expectedAllocationColumn,
        PixelUiDensity expectedDensity)
    {
        var viewport = new Rectangle(0, 0, width, 720);

        var layout = PixelPerformanceLayoutPlanner.Resolve(viewport, metricCount: 12, allocationsRequested);

        Assert.Equal(expectedDensity, layout.Density);
        Assert.Equal(expectedAllocationColumn, layout.ShowAllocationColumn);
        AssertContained(viewport, layout.Panel);
        AssertContained(layout.Panel, layout.Header);
        AssertContained(layout.Panel, layout.RendererCard);
        AssertContained(layout.Panel, layout.Rows);
        Assert.True(layout.Panel.Y >= 120);
        Assert.InRange(layout.NameX, layout.Rows.X, layout.Rows.Right);
        Assert.InRange(layout.TimingX, layout.NameX, layout.Rows.Right);
        Assert.InRange(layout.AllocationX, layout.TimingX, layout.Rows.Right);
    }

    [Fact]
    public void OverlayLayoutPlanning_RemainsAllocationFreeInSteadyState()
    {
        var viewport = new Rectangle(0, 0, 1920, 1080);
        _ = PixelHudLayoutPlanner.Resolve(viewport);
        _ = PixelPerformanceLayoutPlanner.Resolve(viewport, 16, allocationsRequested: true);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var checksum = 0;

        for (var index = 0; index < 10_000; index++)
        {
            checksum += PixelHudLayoutPlanner.Resolve(viewport).SlotSize;
            checksum += PixelPerformanceLayoutPlanner.Resolve(viewport, index % 32, allocationsRequested: true).Panel.Width;
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(checksum > 0);
        Assert.Equal(0, allocated);
    }

    private static void AssertContained(Rectangle outer, Rectangle inner)
    {
        Assert.True(
            inner.X >= outer.X && inner.Y >= outer.Y && inner.Right <= outer.Right && inner.Bottom <= outer.Bottom,
            $"Expected {inner} to be contained by {outer}.");
    }
}
