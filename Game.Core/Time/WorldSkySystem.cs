namespace Game.Core.Time;

public sealed class WorldSkySystem
{
    public SkyState Evaluate(WorldTime time)
    {
        ArgumentNullException.ThrowIfNull(time);

        var t = time.NormalizedTimeOfDay;
        var daylight = ComputeDaylight(t);
        var dawnDusk = ComputeDawnDusk(t);

        var r = Lerp(16, 91, daylight) + (byte)(dawnDusk * 54);
        var g = Lerp(22, 155, daylight) + (byte)(dawnDusk * 20);
        var b = Lerp(42, 213, daylight);
        var sunlight = Lerp(28, 255, daylight);

        return new SkyState(
            (byte)Math.Clamp((int)r, 0, 255),
            (byte)Math.Clamp((int)g, 0, 255),
            (byte)Math.Clamp((int)b, 0, 255),
            (byte)Math.Clamp((int)sunlight, 0, 255));
    }

    private static float ComputeDaylight(double normalizedTime)
    {
        var angle = (normalizedTime - 0.25) * Math.Tau;
        return (float)Math.Clamp((Math.Sin(angle) + 1) * 0.5, 0, 1);
    }

    private static float ComputeDawnDusk(double normalizedTime)
    {
        var dawn = Pulse(normalizedTime, 0.25, 0.08);
        var dusk = Pulse(normalizedTime, 0.75, 0.08);
        return Math.Max(dawn, dusk);
    }

    private static float Pulse(double value, double center, double width)
    {
        var distance = Math.Abs(value - center);
        distance = Math.Min(distance, 1 - distance);
        return (float)Math.Clamp(1 - distance / width, 0, 1);
    }

    private static byte Lerp(int from, int to, float amount)
    {
        return (byte)Math.Clamp((int)MathF.Round(from + (to - from) * amount), 0, 255);
    }
}
