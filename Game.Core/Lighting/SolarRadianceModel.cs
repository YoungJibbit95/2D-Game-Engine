namespace Game.Core.Lighting;

public readonly record struct SolarRadianceState(
    float Elevation,
    float HorizontalDirection,
    float VerticalDirection,
    float DirectIrradiance,
    float LunarIrradiance,
    float DiffuseIrradiance,
    float NightBlend);

/// <summary>
/// Renderer-neutral solar model shared by simulation and presentation adapters.
/// It separates direct irradiance from diffuse twilight and uses a bounded
/// air-mass approximation so the horizon remains finite and deterministic.
/// </summary>
public static class SolarRadianceModel
{
    public const float NightDiffuseFloor = 0.20f;

    public static SolarRadianceState Evaluate(double normalizedTimeOfDay)
    {
        var time = WrapUnit(normalizedTimeOfDay);
        var phase = (time - 0.25d) * Math.Tau;
        var elevation = (float)Math.Sin(phase);
        var daylight = elevation >= 0f;
        var horizontal = (float)-Math.Cos(phase) * (daylight ? 1f : -1f);
        var vertical = -Math.Max(0.08f, Math.Abs(elevation));
        var inverseLength = 1f / MathF.Sqrt(horizontal * horizontal + vertical * vertical);
        horizontal *= inverseLength;
        vertical *= inverseLength;

        var horizonVisibility = SmoothStep(0f, 0.08f, elevation);
        var airMass = 1f / Math.Max(0.04f, elevation);
        var direct = horizonVisibility * MathF.Exp(-0.12f * Math.Max(0f, airMass - 1f));
        var lunarElevation = -elevation;
        var lunarVisibility = SmoothStep(0.02f, 0.18f, lunarElevation);
        var lunar = lunarVisibility * (0.075f + Math.Max(0f, lunarElevation) * 0.035f);
        var diffuse = ResolveDiffuse(elevation);
        var nightBlend = Math.Clamp(
            (1f - diffuse) / (1f - NightDiffuseFloor),
            0f,
            1f);
        return new SolarRadianceState(
            elevation,
            horizontal,
            vertical,
            direct,
            lunar,
            diffuse,
            nightBlend);
    }

    public static double WrapUnit(double value)
    {
        if (!double.IsFinite(value))
        {
            return 0.5d;
        }

        var wrapped = value - Math.Floor(value);
        return wrapped >= 1d ? 0d : wrapped;
    }

    private static float ResolveDiffuse(float elevation)
    {
        var twilight = Math.Clamp((elevation + 0.5f) / 0.68f, 0f, 1f);
        twilight *= twilight * (3f - 2f * twilight);
        return NightDiffuseFloor + (1f - NightDiffuseFloor) * twilight;
    }

    private static float SmoothStep(float minimum, float maximum, float value)
    {
        var t = Math.Clamp((value - minimum) / (maximum - minimum), 0f, 1f);
        return t * t * (3f - 2f * t);
    }
}

