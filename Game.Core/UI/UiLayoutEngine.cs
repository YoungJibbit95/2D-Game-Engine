namespace Game.Core.UI;

public sealed class UiLayoutEngine
{
    public void Arrange(UiElement root, UiRect bounds)
    {
        ArgumentNullException.ThrowIfNull(root);
        root.Bounds = bounds;
        ArrangeChildren(root);
    }

    private void ArrangeChildren(UiElement element)
    {
        if (!element.IsVisible || element.Children.Count == 0)
        {
            return;
        }

        switch (element.LayoutKind)
        {
            case UiLayoutKind.Stack:
                ArrangeStack(element);
                break;
            case UiLayoutKind.Grid:
                ArrangeGrid(element);
                break;
            case UiLayoutKind.Scroll:
                ArrangeScroll(element);
                break;
            case UiLayoutKind.Tabs:
                ArrangeTabs(element);
                break;
            case UiLayoutKind.Splitter:
                ArrangeSplitter(element);
                break;
            case UiLayoutKind.Dock:
                ArrangeDock(element);
                break;
            default:
                ArrangeFree(element);
                break;
        }
    }

    private void ArrangeFree(UiElement element)
    {
        var content = element.Bounds.Deflate(element.Padding);
        foreach (var child in VisibleChildren(element))
        {
            var size = ResolveSize(child, content.Size);
            var x = content.X + child.Bounds.X + child.Margin.Left;
            var y = content.Y + child.Bounds.Y + child.Margin.Top;
            child.Bounds = new UiRect(x, y, size.Width, size.Height);
            ArrangeChildren(child);
        }
    }

    private void ArrangeStack(UiElement element)
    {
        var content = element.Bounds.Deflate(element.Padding);
        var children = VisibleChildren(element).ToArray();
        if (children.Length == 0)
        {
            return;
        }

        var gapTotal = element.Gap * Math.Max(0, children.Length - 1);
        var autoCount = children.Count(child => PrimaryDesired(child, element.Orientation) <= 0);
        var fixedTotal = children.Sum(child => Math.Max(0, PrimaryDesired(child, element.Orientation)));
        var availablePrimary = PrimarySize(content.Size, element.Orientation);
        var autoSize = autoCount == 0 ? 0 : Math.Max(0, (availablePrimary - fixedTotal - gapTotal) / autoCount);
        var cursor = element.Orientation == UiOrientation.Vertical ? content.Y : content.X;

        foreach (var child in children)
        {
            var desiredPrimary = PrimaryDesired(child, element.Orientation);
            var primary = desiredPrimary > 0 ? desiredPrimary : autoSize;
            if (element.Orientation == UiOrientation.Vertical)
            {
                var width = Math.Max(0, content.Width - child.Margin.Left - child.Margin.Right);
                child.Bounds = new UiRect(content.X + child.Margin.Left, cursor + child.Margin.Top, width, primary);
                cursor += primary + element.Gap + child.Margin.Top + child.Margin.Bottom;
            }
            else
            {
                var height = Math.Max(0, content.Height - child.Margin.Top - child.Margin.Bottom);
                child.Bounds = new UiRect(cursor + child.Margin.Left, content.Y + child.Margin.Top, primary, height);
                cursor += primary + element.Gap + child.Margin.Left + child.Margin.Right;
            }

            ArrangeChildren(child);
        }
    }

    private void ArrangeGrid(UiElement element)
    {
        var content = element.Bounds.Deflate(element.Padding);
        var rows = Math.Max(1, element.GridRows);
        var columns = Math.Max(1, element.GridColumns);
        var cellWidth = Math.Max(0, (content.Width - element.Gap * (columns - 1)) / columns);
        var cellHeight = Math.Max(0, (content.Height - element.Gap * (rows - 1)) / rows);

        foreach (var child in VisibleChildren(element))
        {
            var row = Math.Clamp(child.GridRow, 0, rows - 1);
            var column = Math.Clamp(child.GridColumn, 0, columns - 1);
            var rowSpan = Math.Clamp(child.GridRowSpan, 1, rows - row);
            var columnSpan = Math.Clamp(child.GridColumnSpan, 1, columns - column);
            child.Bounds = new UiRect(
                content.X + column * (cellWidth + element.Gap) + child.Margin.Left,
                content.Y + row * (cellHeight + element.Gap) + child.Margin.Top,
                Math.Max(0, cellWidth * columnSpan + element.Gap * (columnSpan - 1) - child.Margin.Left - child.Margin.Right),
                Math.Max(0, cellHeight * rowSpan + element.Gap * (rowSpan - 1) - child.Margin.Top - child.Margin.Bottom));
            ArrangeChildren(child);
        }
    }

    private void ArrangeScroll(UiElement element)
    {
        var content = element.Bounds.Deflate(element.Padding);
        foreach (var child in VisibleChildren(element))
        {
            var size = ResolveSize(child, content.Size);
            child.Bounds = new UiRect(
                content.X - element.ScrollX + child.Margin.Left,
                content.Y - element.ScrollY + child.Margin.Top,
                Math.Max(size.Width, content.Width - child.Margin.Left - child.Margin.Right),
                Math.Max(size.Height, content.Height - child.Margin.Top - child.Margin.Bottom));
            ArrangeChildren(child);
        }
    }

    private void ArrangeTabs(UiElement element)
    {
        var content = element.Bounds.Deflate(element.Padding);
        var visible = VisibleChildren(element).ToArray();
        if (visible.Length == 0)
        {
            return;
        }

        var selected = Math.Clamp(element.SelectedTabIndex, 0, visible.Length - 1);
        for (var index = 0; index < visible.Length; index++)
        {
            visible[index].Bounds = index == selected
                ? content
                : new UiRect(content.X, content.Y, 0, 0);
            if (index == selected)
            {
                ArrangeChildren(visible[index]);
            }
        }
    }

    private void ArrangeSplitter(UiElement element)
    {
        var content = element.Bounds.Deflate(element.Padding);
        var visible = VisibleChildren(element).Take(2).ToArray();
        if (visible.Length == 0)
        {
            return;
        }

        if (visible.Length == 1)
        {
            visible[0].Bounds = content;
            ArrangeChildren(visible[0]);
            return;
        }

        var ratio = Math.Clamp(element.SplitRatio, 0.05f, 0.95f);
        if (element.Orientation == UiOrientation.Vertical)
        {
            var firstHeight = MathF.Round((content.Height - element.Gap) * ratio);
            visible[0].Bounds = new UiRect(content.X, content.Y, content.Width, firstHeight);
            visible[1].Bounds = new UiRect(content.X, content.Y + firstHeight + element.Gap, content.Width, Math.Max(0, content.Height - firstHeight - element.Gap));
        }
        else
        {
            var firstWidth = MathF.Round((content.Width - element.Gap) * ratio);
            visible[0].Bounds = new UiRect(content.X, content.Y, firstWidth, content.Height);
            visible[1].Bounds = new UiRect(content.X + firstWidth + element.Gap, content.Y, Math.Max(0, content.Width - firstWidth - element.Gap), content.Height);
        }

        ArrangeChildren(visible[0]);
        ArrangeChildren(visible[1]);
    }

    private void ArrangeDock(UiElement element)
    {
        var remaining = element.Bounds.Deflate(element.Padding);
        foreach (var child in VisibleChildren(element))
        {
            var desired = ResolveSize(child, remaining.Size);
            switch (child.Dock)
            {
                case UiDock.Left:
                    child.Bounds = new UiRect(remaining.X, remaining.Y, Math.Min(desired.Width, remaining.Width), remaining.Height);
                    remaining = new UiRect(child.Bounds.Right + element.Gap, remaining.Y, Math.Max(0, remaining.Width - child.Bounds.Width - element.Gap), remaining.Height);
                    break;
                case UiDock.Right:
                    child.Bounds = new UiRect(remaining.Right - Math.Min(desired.Width, remaining.Width), remaining.Y, Math.Min(desired.Width, remaining.Width), remaining.Height);
                    remaining = new UiRect(remaining.X, remaining.Y, Math.Max(0, remaining.Width - child.Bounds.Width - element.Gap), remaining.Height);
                    break;
                case UiDock.Top:
                    child.Bounds = new UiRect(remaining.X, remaining.Y, remaining.Width, Math.Min(desired.Height, remaining.Height));
                    remaining = new UiRect(remaining.X, child.Bounds.Bottom + element.Gap, remaining.Width, Math.Max(0, remaining.Height - child.Bounds.Height - element.Gap));
                    break;
                case UiDock.Bottom:
                    child.Bounds = new UiRect(remaining.X, remaining.Bottom - Math.Min(desired.Height, remaining.Height), remaining.Width, Math.Min(desired.Height, remaining.Height));
                    remaining = new UiRect(remaining.X, remaining.Y, remaining.Width, Math.Max(0, remaining.Height - child.Bounds.Height - element.Gap));
                    break;
                default:
                    child.Bounds = remaining;
                    remaining = new UiRect(remaining.X, remaining.Y, 0, 0);
                    break;
            }

            ArrangeChildren(child);
        }
    }

    private static IEnumerable<UiElement> VisibleChildren(UiElement element)
    {
        return element.Children.Where(child => child.IsVisible);
    }

    private static float PrimaryDesired(UiElement element, UiOrientation orientation)
    {
        return orientation == UiOrientation.Vertical
            ? element.DesiredSize.Height
            : element.DesiredSize.Width;
    }

    private static float PrimarySize(UiSize size, UiOrientation orientation)
    {
        return orientation == UiOrientation.Vertical ? size.Height : size.Width;
    }

    private static UiSize ResolveSize(UiElement element, UiSize available)
    {
        var width = element.DesiredSize.Width > 0 ? element.DesiredSize.Width : available.Width;
        var height = element.DesiredSize.Height > 0 ? element.DesiredSize.Height : available.Height;
        width = Math.Max(width, element.MinSize.Width);
        height = Math.Max(height, element.MinSize.Height);
        if (element.MaxSize is { } max)
        {
            width = Math.Min(width, max.Width);
            height = Math.Min(height, max.Height);
        }

        return new UiSize(width, height);
    }
}
