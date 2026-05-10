using Game.Core.Inventory;

namespace Game.Core.UI;

public sealed record UiInteractionSnapshot(
    UiPoint PointerPosition,
    UiElement? HitElement,
    UiElement? FocusedElement,
    CursorItemState? CursorItem)
{
    public bool IsPointerOverUi => HitElement is not null;

    public bool IsDraggingItem => CursorItem?.IsHoldingItem == true;
}
