namespace Game.Core.UI;

public readonly record struct UiSize(float Width, float Height)
{
    public static UiSize Zero => new(0, 0);
}
