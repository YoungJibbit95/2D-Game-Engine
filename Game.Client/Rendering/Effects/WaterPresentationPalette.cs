using Game.Core.Biomes;
using Microsoft.Xna.Framework;

namespace Game.Client.Rendering.Effects;

/// <summary>
/// Presentation-only palette shared by liquid tiles and the existing
/// screen-space reflection surface planner. Gameplay liquid state remains in
/// <see cref="Game.Core.World.TileInstance"/>.
/// </summary>
public readonly record struct WaterPresentationPalette(
    Color ShallowColor,
    Color DeepColor,
    Color SurfaceHighlightColor,
    Color ShoreFoamColor,
    Color ReflectionTint,
    Color WetSurfaceTint,
    float WaterReflectivity,
    float WetSurfaceReflectivity);

public static class WaterPresentationPaletteCatalog
{
    public static WaterPresentationPalette ClearWater { get; } = new(
        new Color(45, 112, 181),
        new Color(17, 53, 119),
        new Color(157, 219, 242),
        new Color(211, 238, 238),
        new Color(118, 177, 211),
        new Color(174, 205, 220),
        WaterReflectivity: 0.42f,
        WetSurfaceReflectivity: 0.22f);

    public static WaterPresentationPalette Resolve(in BiomeRuntimeProfileSnapshot biome)
    {
        return Resolve(biome.BiomeId);
    }

    public static WaterPresentationPalette Resolve(string? biomeId)
    {
        if (string.Equals(biomeId, "twilight_marsh", StringComparison.OrdinalIgnoreCase))
        {
            return new WaterPresentationPalette(
                new Color(37, 106, 96),
                new Color(11, 48, 58),
                new Color(116, 190, 154),
                new Color(181, 219, 168),
                new Color(82, 158, 136),
                new Color(132, 184, 154),
                WaterReflectivity: 0.48f,
                WetSurfaceReflectivity: 0.27f);
        }

        if (string.Equals(biomeId, "amber_grove", StringComparison.OrdinalIgnoreCase))
        {
            return new WaterPresentationPalette(
                new Color(51, 108, 126),
                new Color(20, 58, 77),
                new Color(205, 190, 127),
                new Color(233, 216, 158),
                new Color(183, 154, 92),
                new Color(202, 176, 119),
                WaterReflectivity: 0.46f,
                WetSurfaceReflectivity: 0.24f);
        }

        if (string.Equals(biomeId, "crystal_depths", StringComparison.OrdinalIgnoreCase))
        {
            return new WaterPresentationPalette(
                new Color(65, 91, 161),
                new Color(29, 34, 91),
                new Color(173, 190, 248),
                new Color(219, 221, 255),
                new Color(145, 155, 232),
                new Color(179, 185, 234),
                WaterReflectivity: 0.5f,
                WetSurfaceReflectivity: 0.25f);
        }

        return ClearWater;
    }
}
