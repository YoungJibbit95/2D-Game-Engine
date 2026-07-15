namespace Game.Core.UI;

public readonly record struct UiNumericRange
{
    public UiNumericRange(float minimum, float maximum, float step)
    {
        if (!float.IsFinite(minimum) || !float.IsFinite(maximum) || !float.IsFinite(step))
        {
            throw new ArgumentOutOfRangeException(nameof(minimum), "UI ranges require finite values.");
        }

        if (maximum <= minimum)
        {
            throw new ArgumentOutOfRangeException(nameof(maximum), "Maximum must be greater than minimum.");
        }

        if (step <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(step), "Step must be greater than zero.");
        }

        Minimum = minimum;
        Maximum = maximum;
        Step = step;
    }

    public float Minimum { get; }

    public float Maximum { get; }

    public float Step { get; }

    public float Coerce(float value)
    {
        if (!float.IsFinite(value))
        {
            return Minimum;
        }

        var clamped = Math.Clamp(value, Minimum, Maximum);
        var steps = MathF.Round((clamped - Minimum) / Step, MidpointRounding.AwayFromZero);
        return Math.Clamp(Minimum + steps * Step, Minimum, Maximum);
    }

    public float StepBy(float value, int direction)
    {
        if (direction == 0)
        {
            return Coerce(value);
        }

        return Coerce(value + Step * Math.Sign(direction));
    }

    public float Normalize(float value)
    {
        return (Math.Clamp(value, Minimum, Maximum) - Minimum) / (Maximum - Minimum);
    }

    public float ValueAt(float normalizedPosition)
    {
        var normalized = float.IsFinite(normalizedPosition)
            ? Math.Clamp(normalizedPosition, 0f, 1f)
            : 0f;
        return Coerce(Minimum + (Maximum - Minimum) * normalized);
    }
}
