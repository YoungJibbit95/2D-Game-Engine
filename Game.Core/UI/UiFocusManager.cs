namespace Game.Core.UI;

public sealed class UiFocusManager
{
    public UiElement? FocusedElement { get; private set; }

    public bool SetFocus(UiElement? element)
    {
        if (element is null)
        {
            FocusedElement = null;
            return true;
        }

        if (!CanFocus(element))
        {
            return false;
        }

        FocusedElement = element;
        return true;
    }

    public UiElement? FocusFirst(UiElement root)
    {
        ArgumentNullException.ThrowIfNull(root);
        var first = EnumerateFocusable(root).FirstOrDefault();
        SetFocus(first);
        return FocusedElement;
    }

    public UiElement? MoveFocus(UiElement root, UiNavigationDirection direction)
    {
        ArgumentNullException.ThrowIfNull(root);
        var focusable = EnumerateFocusable(root).ToArray();
        if (focusable.Length == 0)
        {
            FocusedElement = null;
            return null;
        }

        var current = FocusedElement is null ? -1 : Array.IndexOf(focusable, FocusedElement);
        var delta = direction == UiNavigationDirection.Previous ? -1 : 1;
        var next = current < 0
            ? (direction == UiNavigationDirection.Previous ? focusable.Length - 1 : 0)
            : ((current + delta) % focusable.Length + focusable.Length) % focusable.Length;
        FocusedElement = focusable[next];
        return FocusedElement;
    }

    public static IEnumerable<UiElement> EnumerateFocusable(UiElement root)
    {
        if (CanFocus(root))
        {
            yield return root;
        }

        var visibleChildren = root.Children.Where(child => child.IsVisible).ToArray();
        var children = root.LayoutKind == UiLayoutKind.Tabs && visibleChildren.Length > 0
            ? visibleChildren.Skip(Math.Clamp(root.SelectedTabIndex, 0, visibleChildren.Length - 1)).Take(1)
            : visibleChildren;

        foreach (var child in children)
        {
            foreach (var focusable in EnumerateFocusable(child))
            {
                yield return focusable;
            }
        }
    }

    private static bool CanFocus(UiElement element)
    {
        return element.IsVisible && element.IsEnabled && element.IsFocusable;
    }
}
