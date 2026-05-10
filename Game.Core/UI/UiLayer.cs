namespace Game.Core.UI;

public sealed record UiLayer(string Id, UiElement Root, int ZIndex = 0, bool IsModal = false, bool IsVisible = true);
