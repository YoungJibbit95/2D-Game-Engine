namespace Game.Core.UI;

public sealed class UiHitTestService
{
    public UiElement? HitTest(UiElement root, UiPoint point)
    {
        ArgumentNullException.ThrowIfNull(root);
        return HitTestElement(root, point);
    }

    internal UiElement? HitTestElement(UiElement element, UiPoint point)
    {
        if (!element.IsVisible || element.Bounds.IsEmpty || !element.Bounds.Contains(point))
        {
            return null;
        }

        foreach (var child in EnumerateHitTestChildren(element))
        {
            var hit = HitTestElement(child, point);
            if (hit is not null)
            {
                return hit;
            }
        }

        return element.IsEnabled && element.IsHitTestVisible
            ? element
            : null;
    }

    private static IEnumerable<UiElement> EnumerateHitTestChildren(UiElement element)
    {
        if (element.LayoutKind == UiLayoutKind.Tabs && element.Children.Count > 0)
        {
            var visible = element.Children.Where(child => child.IsVisible).ToArray();
            if (visible.Length == 0)
            {
                yield break;
            }

            yield return visible[Math.Clamp(element.SelectedTabIndex, 0, visible.Length - 1)];
            yield break;
        }

        foreach (var child in element.Children
                     .Select((value, index) => new { value, index })
                     .OrderByDescending(item => item.value.ZIndex)
                     .ThenByDescending(item => item.index)
                     .Select(item => item.value))
        {
            yield return child;
        }
    }
}
