using Game.Client.UI;
using Microsoft.Xna.Framework;
using Xunit;

namespace Game.Tests.ClientUiTests;

public sealed class AdventureShellLayoutTests
{
    [Theory]
    [InlineData(320, 240)]
    [InlineData(360, 240)]
    [InlineData(1280, 720)]
    [InlineData(1920, 1080)]
    [InlineData(2560, 1440)]
    [InlineData(3840, 2160)]
    public void InventoryLayout_ContainsAllInteractiveSurfaces(int width, int height)
    {
        var viewport = new Rectangle(0, 0, width, height);
        var layout = PixelInventoryLayoutPlanner.Resolve(viewport);

        AssertContained(viewport, layout.Panel);
        AssertContained(layout.Panel, layout.Header);
        AssertContained(layout.Panel, layout.Toolbar);
        AssertContained(layout.Panel, layout.Filters);
        AssertContained(layout.Panel, layout.PackSurface);
        AssertContained(layout.Panel, layout.StatusBar);
        for (var index = 0; index < 10; index++)
        {
            AssertContained(layout.PackSurface, layout.HotbarSlot(index));
        }

        for (var index = 0; index < 40; index++)
        {
            AssertContained(layout.PackSurface, layout.MainSlot(index));
        }

        if (layout.ShowEquipment)
        {
            AssertContained(layout.Panel, layout.EquipmentSurface);
            Assert.True(layout.PackSurface.Right <= layout.EquipmentSurface.X);
        }
        else
        {
            Assert.Equal(Rectangle.Empty, layout.EquipmentSurface);
        }
    }

    [Theory]
    [InlineData(320, 240)]
    [InlineData(1280, 720)]
    [InlineData(1920, 1080)]
    [InlineData(3840, 2160)]
    public void CraftingLayout_ContainsAllInteractiveSurfaces(int width, int height)
    {
        var viewport = new Rectangle(0, 0, width, height);
        var layout = PixelCraftingLayoutPlanner.Resolve(viewport);

        AssertContained(viewport, layout.Panel);
        AssertContained(layout.Panel, layout.Header);
        AssertContained(layout.Header, layout.Title);
        AssertContained(layout.Header, layout.Search);
        AssertContained(layout.Header, layout.Visibility);
        AssertContained(layout.Panel, layout.Categories);
        AssertContained(layout.Panel, layout.RecipeList);
        AssertContained(layout.RecipeList, layout.RecipeHeader);
        AssertContained(layout.RecipeList, layout.RecipeRows);
        AssertContained(layout.Panel, layout.Details);
        AssertContained(layout.Details, layout.DetailsHeader);
        AssertContained(layout.Details, layout.IngredientList);
        AssertContained(layout.Details, layout.ActionBar);
        AssertContained(layout.Panel, layout.StatusBar);
        Assert.False(layout.RecipeList.Intersects(layout.Details));
    }

    [Theory]
    [InlineData(320, 240)]
    [InlineData(1280, 720)]
    [InlineData(1920, 1080)]
    [InlineData(3840, 2160)]
    public void SettingsShellLayout_ContainsDecorativeSurfaces(int width, int height)
    {
        var viewport = new Rectangle(0, 0, width, height);
        var layout = PixelSettingsShellLayoutPlanner.Resolve(viewport);

        AssertContained(viewport, layout.Horizon);
        AssertContained(viewport, layout.Moon);
        AssertContained(viewport, layout.LeftSpire);
        AssertContained(viewport, layout.RightSpire);
        AssertContained(viewport, layout.FooterRibbon);
    }

    [Fact]
    public void AdventureLayoutPlanning_AllocatesZeroBytesAfterWarmup()
    {
        var viewport = new Rectangle(0, 0, 1920, 1080);
        _ = PixelInventoryLayoutPlanner.Resolve(viewport);
        _ = PixelCraftingLayoutPlanner.Resolve(viewport);
        _ = PixelSettingsShellLayoutPlanner.Resolve(viewport);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var checksum = 0;

        for (var index = 0; index < 10_000; index++)
        {
            checksum += PixelInventoryLayoutPlanner.Resolve(viewport).SlotSize;
            checksum += PixelCraftingLayoutPlanner.Resolve(viewport).RecipeList.Width;
            checksum += PixelSettingsShellLayoutPlanner.Resolve(viewport).Horizon.Height;
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
