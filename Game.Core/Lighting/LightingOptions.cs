namespace Game.Core.Lighting;

public sealed record LightingOptions(
    byte MinimumAmbientLight = 4,
    int OpenAirFalloff = 6,
    int UndergroundAirFalloff = 18,
    int SolidFalloff = 96,
    int PointLightAirFalloff = 28,
    int PointLightSolidFalloff = 96)
{
    public static LightingOptions Default { get; } = new();
}
