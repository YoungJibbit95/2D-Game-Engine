using Game.Core.Inventory;
using Game.Core.UI;
using Xunit;

namespace Game.Tests.UITests;

public sealed class UiToolkitTests
{
    [Fact]
    public void LayoutEngine_ArrangesVerticalStackWithPaddingAndGap()
    {
        var root = new UiElement("root")
        {
            LayoutKind = UiLayoutKind.Stack,
            Orientation = UiOrientation.Vertical,
            Padding = UiThickness.Uniform(2),
            Gap = 4
        };
        var first = root.Add(new UiElement("first") { DesiredSize = new UiSize(0, 20) });
        var second = root.Add(new UiElement("second") { DesiredSize = new UiSize(0, 30) });

        new UiLayoutEngine().Arrange(root, new UiRect(0, 0, 100, 100));

        Assert.Equal(new UiRect(2, 2, 96, 20), first.Bounds);
        Assert.Equal(new UiRect(2, 26, 96, 30), second.Bounds);
    }

    [Fact]
    public void LayoutEngine_ArrangesGridCellsWithSpans()
    {
        var root = new UiElement("root")
        {
            LayoutKind = UiLayoutKind.Grid,
            GridRows = 2,
            GridColumns = 2,
            Gap = 2
        };
        var cell = root.Add(new UiElement("cell")
        {
            GridRow = 1,
            GridColumn = 1
        });
        var span = root.Add(new UiElement("span")
        {
            GridRow = 0,
            GridColumn = 0,
            GridColumnSpan = 2
        });

        new UiLayoutEngine().Arrange(root, new UiRect(0, 0, 100, 100));

        Assert.Equal(new UiRect(51, 51, 49, 49), cell.Bounds);
        Assert.Equal(new UiRect(0, 0, 100, 49), span.Bounds);
    }

    [Fact]
    public void LayoutEngine_ArrangesDockSplitterScrollAndTabs()
    {
        var dockRoot = new UiElement("dock") { LayoutKind = UiLayoutKind.Dock, Gap = 2 };
        var top = dockRoot.Add(new UiElement("top") { Dock = UiDock.Top, DesiredSize = new UiSize(0, 20) });
        var fill = dockRoot.Add(new UiElement("fill") { Dock = UiDock.Fill });
        new UiLayoutEngine().Arrange(dockRoot, new UiRect(0, 0, 120, 80));
        Assert.Equal(new UiRect(0, 0, 120, 20), top.Bounds);
        Assert.Equal(new UiRect(0, 22, 120, 58), fill.Bounds);

        var splitter = new UiElement("split")
        {
            LayoutKind = UiLayoutKind.Splitter,
            Orientation = UiOrientation.Horizontal,
            SplitRatio = 0.25f,
            Gap = 4
        };
        var left = splitter.Add(new UiElement("left"));
        var right = splitter.Add(new UiElement("right"));
        new UiLayoutEngine().Arrange(splitter, new UiRect(0, 0, 104, 40));
        Assert.Equal(new UiRect(0, 0, 25, 40), left.Bounds);
        Assert.Equal(new UiRect(29, 0, 75, 40), right.Bounds);

        var tabs = new UiElement("tabs")
        {
            LayoutKind = UiLayoutKind.Tabs,
            SelectedTabIndex = 1
        };
        var hidden = tabs.Add(new UiElement("hidden"));
        var selected = tabs.Add(new UiElement("selected"));
        new UiLayoutEngine().Arrange(tabs, new UiRect(0, 0, 50, 50));
        Assert.True(hidden.Bounds.IsEmpty);
        Assert.Equal(new UiRect(0, 0, 50, 50), selected.Bounds);

        var scroll = new UiElement("scroll")
        {
            LayoutKind = UiLayoutKind.Scroll,
            ScrollY = 12
        };
        var content = scroll.Add(new UiElement("content") { DesiredSize = new UiSize(100, 200) });
        new UiLayoutEngine().Arrange(scroll, new UiRect(0, 0, 80, 40));
        Assert.Equal(new UiRect(0, -12, 100, 200), content.Bounds);
    }

    [Fact]
    public void HitTest_UsesTopmostZAndSelectedTabs()
    {
        var root = new UiElement("root")
        {
            Bounds = new UiRect(0, 0, 100, 100)
        };
        root.Add(new UiElement("bottom")
        {
            Bounds = new UiRect(10, 10, 40, 40),
            ZIndex = 0
        });
        var top = root.Add(new UiElement("top")
        {
            Bounds = new UiRect(10, 10, 40, 40),
            ZIndex = 5
        });

        Assert.Equal(top, new UiHitTestService().HitTest(root, new UiPoint(20, 20)));

        var tabs = new UiElement("tabs")
        {
            LayoutKind = UiLayoutKind.Tabs,
            Bounds = new UiRect(0, 0, 100, 100),
            SelectedTabIndex = 1
        };
        tabs.Add(new UiElement("inactive") { Bounds = new UiRect(0, 0, 100, 100), ZIndex = 10 });
        var active = tabs.Add(new UiElement("active") { Bounds = new UiRect(0, 0, 100, 100) });

        Assert.Equal(active, new UiHitTestService().HitTest(tabs, new UiPoint(10, 10)));
    }

    [Fact]
    public void LayerStack_BlocksLowerLayersWhenModalLayerIsOnTop()
    {
        var lowerRoot = new UiElement("lower-root")
        {
            Bounds = new UiRect(0, 0, 100, 100),
            IsHitTestVisible = false
        };
        var lowerButton = lowerRoot.Add(new UiElement("lower-button")
        {
            Bounds = new UiRect(10, 10, 20, 20)
        });
        var modalRoot = new UiElement("modal-root")
        {
            Bounds = new UiRect(0, 0, 100, 100),
            IsHitTestVisible = false
        };

        var stack = new UiLayerStack();
        stack.Add(new UiLayer("lower", lowerRoot, ZIndex: 0));
        stack.Add(new UiLayer("modal", modalRoot, ZIndex: 10, IsModal: true));

        var blocked = stack.HitTest(new UiPoint(20, 20));

        Assert.False(blocked.Hit);
        Assert.True(blocked.BlocksLowerLayers);
        Assert.Equal("modal", blocked.Layer?.Id);
        Assert.NotEqual(lowerButton, blocked.Element);
    }

    [Fact]
    public void FocusManager_NavigatesFocusableVisibleEnabledElementsAndSelectedTabs()
    {
        var root = new UiElement("root");
        var first = root.Add(new UiElement("first") { IsFocusable = true });
        root.Add(new UiElement("disabled") { IsFocusable = true, IsEnabled = false });
        var tabs = root.Add(new UiElement("tabs")
        {
            LayoutKind = UiLayoutKind.Tabs,
            SelectedTabIndex = 1
        });
        tabs.Add(new UiElement("inactive-tab-button") { IsFocusable = true });
        var activeTabButton = tabs.Add(new UiElement("active-tab-button") { IsFocusable = true });

        var focus = new UiFocusManager();

        Assert.Equal(first, focus.FocusFirst(root));
        Assert.Equal(activeTabButton, focus.MoveFocus(root, UiNavigationDirection.Next));
        Assert.Equal(first, focus.MoveFocus(root, UiNavigationDirection.Next));
        Assert.Equal(activeTabButton, focus.MoveFocus(root, UiNavigationDirection.Previous));
    }

    [Fact]
    public void TooltipController_ShowsAfterDelayAndCanPin()
    {
        var element = new UiElement("button")
        {
            TooltipText = "Open inventory"
        };
        var tooltip = new UiTooltipController { DelaySeconds = 0.5f };

        tooltip.Update(element, 0.25f);
        Assert.Null(tooltip.VisibleText);

        tooltip.Update(element, 0.25f, pinRequested: true);

        Assert.Equal("Open inventory", tooltip.VisibleText);
        Assert.True(tooltip.IsPinned);

        tooltip.Update(null, 10f);
        Assert.Equal("Open inventory", tooltip.VisibleText);

        tooltip.Update(null, 0f, unpinRequested: true);
        Assert.Null(tooltip.VisibleText);
        Assert.False(tooltip.IsPinned);
    }

    [Fact]
    public void InteractionSnapshot_ExposesPointerAndCursorDragState()
    {
        var hit = new UiElement("slot");
        var cursor = new CursorItemState();
        cursor.Set(new ItemStack("gel", 3));

        var snapshot = new UiInteractionSnapshot(new UiPoint(1, 2), hit, hit, cursor);

        Assert.True(snapshot.IsPointerOverUi);
        Assert.True(snapshot.IsDraggingItem);
        Assert.Equal(hit, snapshot.HitElement);
    }
}
