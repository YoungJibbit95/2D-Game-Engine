using Microsoft.Xna.Framework;

namespace Game.Client.UI;

public abstract class UIElement
{
    private readonly List<UIElement> _children = new();

    public Rectangle Bounds { get; set; }

    public bool IsVisible { get; set; } = true;

    public bool IsEnabled { get; set; } = true;

    public IReadOnlyList<UIElement> Children => _children;

    public void AddChild(UIElement child)
    {
        ArgumentNullException.ThrowIfNull(child);
        _children.Add(child);
    }

    public virtual void Update(UIContext context)
    {
        foreach (var child in _children)
        {
            if (child.IsVisible && child.IsEnabled)
            {
                child.Update(context);
            }
        }
    }

    public void Draw(UIContext context)
    {
        if (!IsVisible)
        {
            return;
        }

        DrawSelf(context);

        foreach (var child in _children)
        {
            child.Draw(context);
        }
    }

    public UIElement? HitTest(Point point)
    {
        if (!IsVisible || !IsEnabled || !Bounds.Contains(point))
        {
            return null;
        }

        for (var index = _children.Count - 1; index >= 0; index--)
        {
            var hit = _children[index].HitTest(point);
            if (hit is not null)
            {
                return hit;
            }
        }

        return this;
    }

    protected virtual void DrawSelf(UIContext context)
    {
    }
}
