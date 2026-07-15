namespace Game.Core.Lighting;

public sealed record LightingOptions(
    byte MinimumAmbientLight = 4,
    int OpenAirFalloff = 6,
    int UndergroundAirFalloff = 10,
    int SolidFalloff = 96,
    int PointLightAirFalloff = 28,
    int PointLightSolidFalloff = 96,
    int IndirectSkylightAirFalloff = 10,
    int IndirectSkylightSolidFalloff = 255,
    int SkylightRelaxationPasses = 2,
    byte UnknownSkyLight = 56)
{
    public static LightingOptions Default { get; } = new();
}
