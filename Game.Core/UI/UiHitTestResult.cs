namespace Game.Core.UI;

public sealed record UiHitTestResult(UiElement? Element, UiLayer? Layer, bool BlocksLowerLayers)
{
    public bool Hit => Element is not null;
}
