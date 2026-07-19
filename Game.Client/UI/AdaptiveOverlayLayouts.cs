using Microsoft.Xna.Framework;

namespace Game.Client.UI;

public enum PixelUiDensity
{
    Compact,
    Regular,
    Expanded
}

public readonly record struct PixelHudLayout(
    PixelUiDensity Density,
    Rectangle HotbarDock,
    Rectangle ResourcePanel,
    Rectangle HealthMeter,
    Rectangle ManaMeter,
    Rectangle GuardMeter,
    Rectangle AttackMeter,
    int SlotSize,
    int SlotGap,
    int SlotCount)
{
    public Rectangle HotbarSlot(int index)
    {
        if (index < 0 || index >= SlotCount)
        {
            return Rectangle.Empty;
        }

        var totalWidth = SlotCount * SlotSize + Math.Max(0, SlotCount - 1) * SlotGap;
        var x = HotbarDock.Center.X - totalWidth / 2 + index * (SlotSize + SlotGap);
        return new Rectangle(x, HotbarDock.Center.Y - SlotSize / 2, SlotSize, SlotSize);
    }
}

public static class PixelHudLayoutPlanner
{
    public static PixelHudLayout Resolve(Rectangle viewport, int slotCount = 10)
    {
        slotCount = Math.Max(1, slotCount);
        var density = viewport.Width switch
        {
            < 720 => PixelUiDensity.Compact,
            >= 1440 => PixelUiDensity.Expanded,
            _ => PixelUiDensity.Regular
        };
        var edge = density switch
        {
            PixelUiDensity.Compact => 8,
            PixelUiDensity.Expanded => 24,
            _ => 16
        };
        var slotGap = density == PixelUiDensity.Compact ? 2 : 4;
        var availableDockWidth = Math.Max(slotCount, viewport.Width - edge * 2 - 12);
        var maximumSlotSize = Math.Max(
            1,
            (availableDockWidth - slotGap * Math.Max(0, slotCount - 1)) / slotCount);
        var slotSize = Math.Min(maximumSlotSize, density == PixelUiDensity.Expanded ? 46 : 42);
        var slotsWidth = slotCount * slotSize + slotGap * Math.Max(0, slotCount - 1);
        var dockWidth = Math.Min(viewport.Width, slotsWidth + 12);
        var dockHeight = Math.Min(viewport.Height, slotSize + 12);
        var dockX = viewport.X + Math.Max(0, (viewport.Width - dockWidth) / 2);
        var dockY = viewport.Bottom - edge - dockHeight;
        if (dockY < viewport.Y)
        {
            dockY = viewport.Y;
        }

        var hotbarDock = new Rectangle(dockX, dockY, dockWidth, dockHeight);
        var panelWidth = Math.Min(
            Math.Max(0, viewport.Width - edge * 2),
            density == PixelUiDensity.Expanded ? 244 : 224);
        var panelHeight = Math.Min(112, Math.Max(0, viewport.Height - edge * 2));
        var panelX = viewport.Right - edge - panelWidth;
        var panelY = viewport.Y + edge;
        if (panelX < viewport.X)
        {
            panelX = viewport.X;
        }

        var resourcePanel = new Rectangle(panelX, panelY, panelWidth, panelHeight);
        var contentX = resourcePanel.X + 10;
        var contentWidth = Math.Max(0, resourcePanel.Width - 20);
        var health = Contain(resourcePanel, new Rectangle(contentX, resourcePanel.Y + 24, contentWidth, 17));
        var mana = Contain(resourcePanel, new Rectangle(contentX, resourcePanel.Y + 48, contentWidth, 14));
        var guard = Contain(resourcePanel, new Rectangle(contentX, resourcePanel.Y + 70, contentWidth, 12));
        var attack = Contain(resourcePanel, new Rectangle(contentX, resourcePanel.Y + 88, contentWidth, 16));

        return new PixelHudLayout(
            density,
            hotbarDock,
            resourcePanel,
            health,
            mana,
            guard,
            attack,
            slotSize,
            slotGap,
            slotCount);
    }

    private static Rectangle Contain(Rectangle outer, Rectangle requested)
    {
        var x = Math.Clamp(requested.X, outer.X, outer.Right);
        var y = Math.Clamp(requested.Y, outer.Y, outer.Bottom);
        return new Rectangle(
            x,
            y,
            Math.Max(0, Math.Min(requested.Width, outer.Right - x)),
            Math.Max(0, Math.Min(requested.Height, outer.Bottom - y)));
    }
}

public readonly record struct PixelPerformanceLayout(
    PixelUiDensity Density,
    Rectangle Panel,
    Rectangle Header,
    Rectangle RendererCard,
    Rectangle Rows,
    int RowHeight,
    int NameX,
    int TimingX,
    int AllocationX,
    bool ShowAllocationColumn);

public static class PixelPerformanceLayoutPlanner
{
    public static PixelPerformanceLayout Resolve(
        Rectangle viewport,
        int metricCount,
        bool allocationsRequested)
    {
        var density = viewport.Width < 640 ? PixelUiDensity.Compact : PixelUiDensity.Regular;
        var edge = density == PixelUiDensity.Compact ? 8 : 12;
        var availableWidth = Math.Max(0, viewport.Width - edge * 2);
        var showAllocations = allocationsRequested && availableWidth >= 390;
        var desiredWidth = showAllocations ? 440 : density == PixelUiDensity.Compact ? 304 : 350;
        var width = Math.Min(desiredWidth, availableWidth);
        var rowHeight = density == PixelUiDensity.Compact ? 16 : 18;
        var desiredHeight = 76 + Math.Max(1, metricCount) * rowHeight;
        var panelY = Math.Min(viewport.Bottom, viewport.Y + edge + 120);
        var height = Math.Min(Math.Max(0, viewport.Bottom - edge - panelY), desiredHeight);
        var panel = new Rectangle(viewport.Right - edge - width, panelY, width, height);
        var header = Contain(panel, new Rectangle(panel.X + 1, panel.Y + 1, Math.Max(0, panel.Width - 2), 32));
        var renderer = Contain(panel, new Rectangle(panel.X + 10, panel.Y + 38, Math.Max(0, panel.Width - 20), 24));
        var rows = Contain(panel, new Rectangle(panel.X + 10, panel.Y + 68, Math.Max(0, panel.Width - 20), Math.Max(0, panel.Height - 76)));
        var nameX = rows.X;
        var timingX = rows.X + (showAllocations ? rows.Width * 44 / 100 : rows.Width * 57 / 100);
        var allocationX = rows.X + rows.Width * 76 / 100;
        return new PixelPerformanceLayout(
            density,
            panel,
            header,
            renderer,
            rows,
            rowHeight,
            nameX,
            timingX,
            allocationX,
            showAllocations);
    }

    private static Rectangle Contain(Rectangle outer, Rectangle requested)
    {
        var x = Math.Clamp(requested.X, outer.X, outer.Right);
        var y = Math.Clamp(requested.Y, outer.Y, outer.Bottom);
        return new Rectangle(
            x,
            y,
            Math.Max(0, Math.Min(requested.Width, outer.Right - x)),
            Math.Max(0, Math.Min(requested.Height, outer.Bottom - y)));
    }
}
