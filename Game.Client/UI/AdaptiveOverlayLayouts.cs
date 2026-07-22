
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
    Rectangle ResourceCrest,
    Rectangle WorldPanel,
    Rectangle WorldIcon,
    Rectangle WorldEventMeter,
    Rectangle ContextPanel,
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
        if (viewport.Height < 400)
        {
            density = PixelUiDensity.Compact;
        }

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
        var slotSize = Math.Min(maximumSlotSize, density == PixelUiDensity.Expanded ? 52 : 44);
        var slotsWidth = slotCount * slotSize + slotGap * Math.Max(0, slotCount - 1);
        var dockWidth = Math.Min(viewport.Width, slotsWidth + 18);
        var dockHeight = Math.Min(viewport.Height, slotSize + 16);
        var dockX = viewport.X + Math.Max(0, (viewport.Width - dockWidth) / 2);
        var dockY = viewport.Bottom - edge - dockHeight;
        if (dockY < viewport.Y)
        {
            dockY = viewport.Y;
        }

        var hotbarDock = new Rectangle(dockX, dockY, dockWidth, dockHeight);
        var resourceWidth = Math.Min(
            Math.Max(0, viewport.Width - edge * 2),
            density == PixelUiDensity.Expanded ? 420 : density == PixelUiDensity.Compact ? 184 : 320);
        var desiredPanelHeight = density switch
        {
            PixelUiDensity.Compact => 106,
            PixelUiDensity.Expanded => 154,
            _ => 126
        };
        var panelHeight = Math.Min(desiredPanelHeight, Math.Max(0, viewport.Height - edge * 2));
        var panelX = viewport.X + edge;
        var panelY = viewport.Y + edge;
        var resourcePanel = new Rectangle(panelX, panelY, resourceWidth, panelHeight);

        var showCrest = resourcePanel.Width >= 150 && resourcePanel.Height >= 78;
        var crestSize = showCrest
            ? Math.Min(density == PixelUiDensity.Expanded ? 66 : 46, Math.Max(0, resourcePanel.Height - 42))
            : 0;
        var resourceCrest = crestSize > 0
            ? Contain(resourcePanel, new Rectangle(resourcePanel.X + 10, resourcePanel.Y + 27, crestSize, crestSize))
            : Rectangle.Empty;
        var contentX = resourceCrest.IsEmpty ? resourcePanel.X + 10 : resourceCrest.Right + 10;
        var contentWidth = Math.Max(0, resourcePanel.Right - contentX - 10);
        var health = Contain(
            resourcePanel,
            new Rectangle(
                contentX,
                resourcePanel.Y + (density == PixelUiDensity.Expanded ? 30 : 24),
                contentWidth,
                density == PixelUiDensity.Expanded ? 22 : 18));
        var mana = Contain(
            resourcePanel,
            new Rectangle(
                contentX,
                resourcePanel.Y + (density == PixelUiDensity.Expanded ? 61 : 48),
                contentWidth,
                density == PixelUiDensity.Expanded ? 18 : 15));
        var guard = Contain(
            resourcePanel,
            new Rectangle(
                contentX,
                resourcePanel.Y + (density == PixelUiDensity.Expanded ? 87 : 70),
                contentWidth,
                density == PixelUiDensity.Expanded ? 15 : 13));
        var attack = Contain(
            resourcePanel,
            new Rectangle(
                contentX,
                resourcePanel.Y + (density == PixelUiDensity.Expanded ? 113 : 91),
                contentWidth,
                density == PixelUiDensity.Expanded ? 20 : 17));

        var worldLeft = resourcePanel.Right + 8;
        var worldRight = viewport.Right - edge;
        var worldWidth = Math.Min(
            density == PixelUiDensity.Expanded ? 360 : density == PixelUiDensity.Compact ? 210 : 260,
            Math.Max(0, worldRight - worldLeft));
        var worldHeight = Math.Min(density == PixelUiDensity.Compact ? 82 : density == PixelUiDensity.Expanded ? 136 : 104, panelHeight);
        var worldPanel = new Rectangle(worldRight - worldWidth, panelY, worldWidth, worldHeight);
        var worldIconSize = worldPanel.Width >= 120
            ? Math.Min(density == PixelUiDensity.Compact ? 40 : density == PixelUiDensity.Expanded ? 72 : 54, Math.Max(0, worldPanel.Height - 34))
            : Math.Min(24, Math.Max(0, worldPanel.Height - 22));
        var worldIcon = Contain(
            worldPanel,
            new Rectangle(worldPanel.X + 10, worldPanel.Y + 25, worldIconSize, worldIconSize));
        var worldEventMeter = Contain(
            worldPanel,
            new Rectangle(worldPanel.X + 10, worldPanel.Bottom - 12, Math.Max(0, worldPanel.Width - 20), 5));

        var contextWidth = Math.Min(
            density == PixelUiDensity.Expanded ? 560 : 360,
            Math.Max(0, viewport.Width - edge * 2));
        var contextHeight = density == PixelUiDensity.Compact ? 24 : 29;
        var contextY = Math.Max(viewport.Y, hotbarDock.Y - contextHeight - 7);
        var contextPanel = new Rectangle(
            viewport.X + Math.Max(0, (viewport.Width - contextWidth) / 2),
            contextY,
            contextWidth,
            Math.Min(contextHeight, Math.Max(0, viewport.Bottom - contextY)));

        return new PixelHudLayout(
            density,
            hotbarDock,
            resourcePanel,
            resourceCrest,
            worldPanel,
            worldIcon,
            worldEventMeter,
            contextPanel,
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

public readonly record struct PixelInventoryLayout(
    PixelUiDensity Density,
    Rectangle Panel,
    Rectangle Header,
    Rectangle Toolbar,
    Rectangle Filters,
    Rectangle PackSurface,
    Rectangle EquipmentSurface,
    Rectangle StatusBar,
    Point HotbarOrigin,
    Point MainOrigin,
    int SlotSize,
    int SlotGap,
    int Columns,
    bool ShowEquipment)
{
    public Rectangle HotbarSlot(int index) => GridSlot(HotbarOrigin, index);

    public Rectangle MainSlot(int index) => GridSlot(MainOrigin, index);

    private Rectangle GridSlot(Point origin, int index)
    {
        if (index < 0 || Columns <= 0 || SlotSize <= 0)
        {
            return Rectangle.Empty;
        }

        return new Rectangle(
            origin.X + index % Columns * (SlotSize + SlotGap),
            origin.Y + index / Columns * (SlotSize + SlotGap),
            SlotSize,
            SlotSize);
    }
}

public static class PixelInventoryLayoutPlanner
{
    private const int HotbarSlots = 10;
    private const int MainSlots = 40;

    public static PixelInventoryLayout Resolve(Rectangle viewport)
    {
        var density = ResolveDensity(viewport);
        var edge = density == PixelUiDensity.Compact ? 4 : density == PixelUiDensity.Expanded ? 28 : 14;
        var panelWidth = Math.Min(density == PixelUiDensity.Expanded ? 1100 : 980, Math.Max(0, viewport.Width - edge * 2));
        var panelHeight = Math.Min(density == PixelUiDensity.Expanded ? 620 : 540, Math.Max(0, viewport.Height - edge * 2));
        var panel = new Rectangle(
            viewport.X + Math.Max(0, (viewport.Width - panelWidth) / 2),
            viewport.Y + Math.Max(0, (viewport.Height - panelHeight) / 2),
            panelWidth,
            panelHeight);
        var headerHeight = density == PixelUiDensity.Compact ? 38 : 54;
        var toolbarHeight = density == PixelUiDensity.Compact ? 24 : 30;
        var filterHeight = density == PixelUiDensity.Compact ? 22 : 26;
        var gap = density == PixelUiDensity.Compact ? 3 : 8;
        var header = Contain(panel, new Rectangle(panel.X + 1, panel.Y + 1, Math.Max(0, panel.Width - 2), headerHeight));
        var toolbar = Contain(panel, new Rectangle(panel.X + 8, header.Bottom + 3, Math.Max(0, panel.Width - 16), toolbarHeight));
        var filters = Contain(panel, new Rectangle(panel.X + 8, toolbar.Bottom + 3, Math.Max(0, panel.Width - 16), filterHeight));
        var statusHeight = density == PixelUiDensity.Compact ? 16 : 24;
        var statusBar = Contain(panel, new Rectangle(panel.X + 10, panel.Bottom - statusHeight - 5, Math.Max(0, panel.Width - 20), statusHeight));
        var contentTop = filters.Bottom + gap;
        var contentHeight = Math.Max(0, statusBar.Y - gap - contentTop);
        var showEquipment = panel.Width >= 700 && contentHeight >= 250;
        var equipmentWidth = showEquipment ? Math.Clamp(panel.Width / 3, 230, 338) : 0;
        var packWidth = Math.Max(0, panel.Width - 20 - (showEquipment ? equipmentWidth + gap : 0));
        var packSurface = Contain(panel, new Rectangle(panel.X + 10, contentTop, packWidth, contentHeight));
        var equipmentSurface = showEquipment
            ? Contain(panel, new Rectangle(packSurface.Right + gap, contentTop, equipmentWidth, contentHeight))
            : Rectangle.Empty;
        const int columns = 10;
        var slotGap = density == PixelUiDensity.Compact ? 2 : 5;
        var gridWidth = Math.Max(0, packSurface.Width - 16);
        var widthSlot = Math.Max(1, (gridWidth - slotGap * (columns - 1)) / columns);
        var hotbarRows = (HotbarSlots + columns - 1) / columns;
        var mainRows = (MainSlots + columns - 1) / columns;
        var rowCount = hotbarRows + mainRows;
        var verticalLabels = density == PixelUiDensity.Compact ? 26 : 42;
        var heightSlot = Math.Max(
            1,
            (Math.Max(0, packSurface.Height - verticalLabels) - slotGap * Math.Max(0, rowCount - 1)) / Math.Max(1, rowCount));
        var slotSize = Math.Clamp(
            Math.Min(widthSlot, heightSlot),
            density == PixelUiDensity.Compact ? 10 : 22,
            density == PixelUiDensity.Expanded ? 48 : 42);
        var hotbarOrigin = new Point(packSurface.X + 8, packSurface.Y + (density == PixelUiDensity.Compact ? 12 : 20));
        var mainOrigin = new Point(
            hotbarOrigin.X,
            hotbarOrigin.Y + hotbarRows * (slotSize + slotGap) + (density == PixelUiDensity.Compact ? 10 : 18));

        return new PixelInventoryLayout(
            density,
            panel,
            header,
            toolbar,
            filters,
            packSurface,
            equipmentSurface,
            statusBar,
            hotbarOrigin,
            mainOrigin,
            slotSize,
            slotGap,
            columns,
            showEquipment);
    }

    private static PixelUiDensity ResolveDensity(Rectangle viewport)
    {
        if (viewport.Width < 720 || viewport.Height < 400)
        {
            return PixelUiDensity.Compact;
        }

        return viewport.Width >= 1600 && viewport.Height >= 900
            ? PixelUiDensity.Expanded
            : PixelUiDensity.Regular;
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

public readonly record struct PixelCraftingLayout(
    PixelUiDensity Density,
    Rectangle Panel,
    Rectangle Header,
    Rectangle Title,
    Rectangle Search,
    Rectangle Visibility,
    Rectangle Categories,
    Rectangle RecipeList,
    Rectangle RecipeHeader,
    Rectangle RecipeRows,
    Rectangle Details,
    Rectangle DetailsHeader,
    Rectangle IngredientList,
    Rectangle ActionBar,
    Rectangle StatusBar,
    bool CompactDetails);

public static class PixelCraftingLayoutPlanner
{
    public static PixelCraftingLayout Resolve(Rectangle viewport)
    {
        var density = viewport.Width < 720 || viewport.Height < 420
            ? PixelUiDensity.Compact
            : viewport.Width >= 1600 && viewport.Height >= 900
                ? PixelUiDensity.Expanded
                : PixelUiDensity.Regular;
        var compact = density == PixelUiDensity.Compact;
        var edge = density switch
        {
            PixelUiDensity.Compact => 4,
            PixelUiDensity.Expanded => 28,
            _ => 16
        };
        var panelWidth = Math.Min(
            density == PixelUiDensity.Expanded ? 1120 : 980,
            Math.Max(0, viewport.Width - edge * 2));
        var panelHeight = Math.Min(
            density == PixelUiDensity.Expanded ? 660 : 580,
            Math.Max(0, viewport.Height - edge * 2));
        var panel = new Rectangle(
            viewport.X + Math.Max(0, (viewport.Width - panelWidth) / 2),
            viewport.Y + Math.Max(0, (viewport.Height - panelHeight) / 2),
            panelWidth,
            panelHeight);
        var headerHeight = compact ? 38 : density == PixelUiDensity.Expanded ? 58 : 52;
        var header = Contain(panel, new Rectangle(panel.X + 1, panel.Y + 1, Math.Max(0, panel.Width - 2), headerHeight));
        var titleWidth = compact ? Math.Min(72, panel.Width / 4) : Math.Min(140, panel.Width / 5);
        var title = Contain(header, new Rectangle(header.X + 8, header.Y + 5, titleWidth, Math.Max(0, header.Height - 10)));
        var visibilityWidth = compact ? Math.Min(108, panel.Width * 36 / 100) : Math.Min(230, panel.Width / 4);
        var visibility = Contain(header, new Rectangle(header.Right - visibilityWidth - 8, header.Y + 5, visibilityWidth, Math.Max(0, header.Height - 10)));
        var search = Contain(header, new Rectangle(title.Right + 5, header.Y + 5, Math.Max(0, visibility.X - title.Right - 10), Math.Max(0, header.Height - 10)));
        var categoryHeight = compact ? 22 : density == PixelUiDensity.Expanded ? 32 : 28;
        var categories = Contain(panel, new Rectangle(panel.X + 8, header.Bottom + 4, Math.Max(0, panel.Width - 16), categoryHeight));
        var statusHeight = compact ? 18 : 24;
        var statusBar = Contain(panel, new Rectangle(panel.X + 10, panel.Bottom - statusHeight - 4, Math.Max(0, panel.Width - 20), statusHeight));
        var contentTop = categories.Bottom + 5;
        var contentHeight = Math.Max(0, statusBar.Y - 5 - contentTop);
        var horizontalInset = compact ? 8 : 12;
        var paneGap = compact ? 4 : 10;
        var contentWidth = Math.Max(0, panel.Width - horizontalInset * 2);
        var minimumDetailsWidth = Math.Min(compact ? 148 : 300, Math.Max(1, contentWidth / 2));
        var maximumListWidth = Math.Max(1, contentWidth - paneGap - minimumDetailsWidth);
        var minimumListWidth = Math.Min(compact ? 96 : 250, maximumListWidth);
        var desiredListWidth = compact
            ? contentWidth * 38 / 100
            : Math.Clamp(contentWidth * 38 / 100, 270, 410);
        var listWidth = Math.Clamp(desiredListWidth, minimumListWidth, maximumListWidth);
        var recipeList = Contain(
            panel,
            new Rectangle(panel.X + horizontalInset, contentTop, listWidth, contentHeight));
        var details = Contain(
            panel,
            new Rectangle(
                recipeList.Right + paneGap,
                contentTop,
                Math.Max(0, panel.Right - horizontalInset - recipeList.Right - paneGap),
                contentHeight));

        var recipeHeaderHeight = Math.Min(compact ? 24 : 32, recipeList.Height);
        var recipeHeader = Contain(
            recipeList,
            new Rectangle(recipeList.X + 1, recipeList.Y + 1, Math.Max(0, recipeList.Width - 2), recipeHeaderHeight));
        var recipeRows = Contain(
            recipeList,
            new Rectangle(
                recipeList.X + 5,
                recipeHeader.Bottom + 3,
                Math.Max(0, recipeList.Width - 10),
                Math.Max(0, recipeList.Bottom - recipeHeader.Bottom - 7)));

        var actionHeight = Math.Min(compact ? 26 : density == PixelUiDensity.Expanded ? 38 : 34, details.Height);
        var actionBar = Contain(
            details,
            new Rectangle(details.X + 6, details.Bottom - actionHeight - 5, Math.Max(0, details.Width - 12), actionHeight));
        var detailsHeaderY = details.Y + 5;
        var maximumDetailsHeaderHeight = Math.Max(0, actionBar.Y - detailsHeaderY - 4);
        var detailsHeaderHeight = Math.Min(compact ? 50 : 68, maximumDetailsHeaderHeight);
        var detailsHeader = Contain(
            details,
            new Rectangle(details.X + 6, detailsHeaderY, Math.Max(0, details.Width - 12), detailsHeaderHeight));
        var ingredientTop = detailsHeader.Bottom + 4;
        var ingredientList = Contain(
            details,
            new Rectangle(
                details.X + 7,
                ingredientTop,
                Math.Max(0, details.Width - 14),
                Math.Max(0, actionBar.Y - ingredientTop - 4)));

        return new PixelCraftingLayout(
            density,
            panel,
            header,
            title,
            search,
            visibility,
            categories,
            recipeList,
            recipeHeader,
            recipeRows,
            details,
            detailsHeader,
            ingredientList,
            actionBar,
            statusBar,
            compact);
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

public readonly record struct PixelSettingsShellLayout(
    PixelUiDensity Density,
    Rectangle Horizon,
    Rectangle Moon,
    Rectangle LeftSpire,
    Rectangle RightSpire,
    Rectangle FooterRibbon);

public static class PixelSettingsShellLayoutPlanner
{
    public static PixelSettingsShellLayout Resolve(Rectangle viewport)
    {
        var density = viewport.Width < 720 || viewport.Height < 400
            ? PixelUiDensity.Compact
            : viewport.Width >= 1600 ? PixelUiDensity.Expanded : PixelUiDensity.Regular;
        var horizonHeight = Math.Max(1, viewport.Height * 38 / 100);
        var horizon = new Rectangle(viewport.X, viewport.Bottom - horizonHeight, viewport.Width, horizonHeight);
        var moonSize = Math.Clamp(Math.Min(viewport.Width, viewport.Height) / 7, 18, 120);
        var moon = new Rectangle(viewport.Right - moonSize - Math.Max(8, viewport.Width / 18), viewport.Y + Math.Max(8, viewport.Height / 14), moonSize, moonSize);
        var spireWidth = Math.Max(1, viewport.Width / 6);
        var leftSpire = new Rectangle(viewport.X, viewport.Bottom - horizonHeight - viewport.Height / 12, spireWidth, horizonHeight + viewport.Height / 12);
        var rightSpire = new Rectangle(viewport.Right - spireWidth, viewport.Bottom - horizonHeight - viewport.Height / 18, spireWidth, horizonHeight + viewport.Height / 18);
        var ribbon = new Rectangle(viewport.X + viewport.Width / 5, viewport.Bottom - Math.Max(5, viewport.Height / 45), viewport.Width * 3 / 5, Math.Max(2, viewport.Height / 90));
        return new PixelSettingsShellLayout(density, horizon, moon, leftSpire, rightSpire, ribbon);
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
