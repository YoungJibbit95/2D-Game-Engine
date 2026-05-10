namespace Game.Core.UI;

public sealed class UiTooltipController
{
    private UiElement? _hoveredElement;
    private float _hoverTime;

    public float DelaySeconds { get; init; } = 0.45f;

    public bool IsPinned { get; private set; }

    public UiElement? SourceElement { get; private set; }

    public string? VisibleText { get; private set; }

    public void Update(UiElement? hitElement, float deltaSeconds, bool pinRequested = false, bool unpinRequested = false)
    {
        if (unpinRequested)
        {
            Unpin();
        }

        if (IsPinned)
        {
            return;
        }

        if (!ReferenceEquals(_hoveredElement, hitElement))
        {
            _hoveredElement = hitElement;
            _hoverTime = 0;
            SourceElement = null;
            VisibleText = null;
        }

        if (hitElement is null || string.IsNullOrWhiteSpace(hitElement.TooltipText))
        {
            return;
        }

        _hoverTime += Math.Max(0, deltaSeconds);
        if (_hoverTime >= DelaySeconds)
        {
            SourceElement = hitElement;
            VisibleText = hitElement.TooltipText;
        }

        if (pinRequested && VisibleText is not null)
        {
            IsPinned = true;
        }
    }

    public void Unpin()
    {
        IsPinned = false;
        SourceElement = null;
        VisibleText = null;
        _hoveredElement = null;
        _hoverTime = 0;
    }
}
