using Game.Core.Lighting;
using Microsoft.Xna.Framework;

namespace Game.Client.Rendering.Effects;

internal readonly record struct SolarLightState(
    Vector2 DirectionTowardPrimaryLight,
    float Elevation,
    float DirectIrradiance,
    float LunarIrradiance,
    float DiffuseIrradiance,
    float NightBlend);

internal static class SolarIlluminationCurve
{
    public static SolarLightState Evaluate(float normalizedTimeOfDay)
    {
        var state = SolarRadianceModel.Evaluate(normalizedTimeOfDay);
        return new SolarLightState(
            new Vector2(state.HorizontalDirection, state.VerticalDirection),
            state.Elevation,
            state.DirectIrradiance,
            state.LunarIrradiance,
            state.DiffuseIrradiance,
            state.NightBlend);
    }

    public static float ResolveSkyIllumination(float normalizedTimeOfDay)
    {
        return SolarRadianceModel.Evaluate(normalizedTimeOfDay).DiffuseIrradiance;
    }

    public static float ResolveNightBlend(float normalizedTimeOfDay)
    {
        return Evaluate(normalizedTimeOfDay).NightBlend;
    }

    public static float WrapUnit(float value)
    {
        return (float)SolarRadianceModel.WrapUnit(value);
    }

}
