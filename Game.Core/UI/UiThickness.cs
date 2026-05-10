namespace Game.Core.UI;

public readonly record struct UiThickness(float Left, float Top, float Right, float Bottom)
{
    public static UiThickness Zero => new(0, 0, 0, 0);

    public static UiThickness Uniform(float value)
    {
        return new UiThickness(value, value, value, value);
    }
}
