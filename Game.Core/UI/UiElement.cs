namespace Game.Core.UI;

public class UiElement
{
    private readonly List<UiElement> _children = new();

    public UiElement(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        Id = id;
    }

    public string Id { get; }

    public UiElement? Parent { get; private set; }

    public IReadOnlyList<UiElement> Children => _children;

    public UiRect Bounds { get; set; }

    public UiSize DesiredSize { get; set; } = UiSize.Zero;

    public UiSize MinSize { get; set; } = UiSize.Zero;

    public UiSize? MaxSize { get; set; }

    public UiThickness Margin { get; set; } = UiThickness.Zero;

    public UiThickness Padding { get; set; } = UiThickness.Zero;

    public UiLayoutKind LayoutKind { get; set; } = UiLayoutKind.Free;

    public UiOrientation Orientation { get; set; } = UiOrientation.Vertical;

    public UiDock Dock { get; set; } = UiDock.Fill;

    public int GridRow { get; set; }

    public int GridColumn { get; set; }

    public int GridRowSpan { get; set; } = 1;

    public int GridColumnSpan { get; set; } = 1;

    public int GridRows { get; set; } = 1;

    public int GridColumns { get; set; } = 1;

    public int SelectedTabIndex { get; set; }

    public float SplitRatio { get; set; } = 0.5f;

    public float ScrollX { get; set; }

    public float ScrollY { get; set; }

    public float Gap { get; set; }

    public int ZIndex { get; set; }

    public bool IsVisible { get; set; } = true;

    public bool IsEnabled { get; set; } = true;

    public bool IsHitTestVisible { get; set; } = true;

    public bool IsFocusable { get; set; }

    public string? TooltipText { get; set; }

    public T Add<T>(T child)
        where T : UiElement
    {
        ArgumentNullException.ThrowIfNull(child);
        if (child.Parent is not null)
        {
            throw new InvalidOperationException($"UI element '{child.Id}' already has a parent.");
        }

        child.Parent = this;
        _children.Add(child);
        return child;
    }

    public bool Remove(UiElement child)
    {
        ArgumentNullException.ThrowIfNull(child);
        if (!_children.Remove(child))
        {
            return false;
        }

        child.Parent = null;
        return true;
    }

    public IEnumerable<UiElement> EnumerateSelfAndDescendants()
    {
        yield return this;
        foreach (var child in _children)
        {
            foreach (var descendant in child.EnumerateSelfAndDescendants())
            {
                yield return descendant;
            }
        }
    }
}
