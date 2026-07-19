namespace Game.Core.World.Generation;

/// <summary>
/// Deterministic, allocation-free topology sampler shared by finite and infinite worlds.
/// Wide interpolation bands create readable hills and valleys while the detail band keeps
/// the surface from becoming mechanically flat.
/// </summary>
public static class WorldSurfaceSampler
{
    private const int MacroSpacingTiles = 192;
    private const int RollingSpacingTiles = 64;
    private const int DetailSpacingTiles = 20;
    private const int SoilMacroSpacingTiles = 72;
    private const int SoilDetailSpacingTiles = 24;
    private const int SpawnPlateauRadiusTiles = 24;
    private const int SpawnTransitionRadiusTiles = 64;

    public static int GetSurfaceHeight(
        WorldGenerationProfile profile,
        int seed,
        int topologyTileX,
        float elevationMultiplier = 1f)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var baseSurfaceY = Math.Clamp(profile.SurfaceBaseY, 6, profile.HeightTiles - 10);
        var amplitude = Math.Max(
            0f,
            profile.SurfaceAmplitude * Math.Clamp(elevationMultiplier, 0.35f, 2.25f));
        if (amplitude <= 0f)
        {
            return ClampSurface(profile, baseSurfaceY);
        }

        var macro = SampleValueNoise(seed, topologyTileX, MacroSpacingTiles, 0x6A09E667F3BCC909UL);
        var rolling = SampleValueNoise(seed, topologyTileX, RollingSpacingTiles, 0xBB67AE8584CAA73BUL);
        var detail = SampleValueNoise(seed, topologyTileX, DetailSpacingTiles, 0x3C6EF372FE94F82BUL);
        var relief = macro * 0.55f + rolling * 0.34f + detail * 0.11f;
        var rawSurfaceY = baseSurfaceY + relief * amplitude;

        var absoluteX = Math.Abs((long)topologyTileX);
        if (absoluteX < SpawnTransitionRadiusTiles)
        {
            var spawnRelief = SampleValueNoise(seed, 0, MacroSpacingTiles, 0x6A09E667F3BCC909UL) * 0.45f +
                SampleValueNoise(seed, 0, RollingSpacingTiles, 0xBB67AE8584CAA73BUL) * 0.1f;
            var spawnSurfaceY = baseSurfaceY + spawnRelief * amplitude;
            var blend = absoluteX <= SpawnPlateauRadiusTiles
                ? 0f
                : SmoothStep((absoluteX - SpawnPlateauRadiusTiles) /
                    (float)(SpawnTransitionRadiusTiles - SpawnPlateauRadiusTiles));
            rawSurfaceY = Lerp(spawnSurfaceY, rawSurfaceY, blend);
        }

        return ClampSurface(profile, (int)MathF.Round(rawSurfaceY));
    }

    public static int GetDirtDepth(
        WorldGenerationProfile profile,
        int seed,
        int topologyTileX,
        float depthMultiplier = 1f)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var minimum = Math.Max(1, Math.Min(profile.DirtDepthMin, profile.DirtDepthMax));
        var maximum = Math.Max(minimum, Math.Max(profile.DirtDepthMin, profile.DirtDepthMax));
        if (minimum == maximum)
        {
            return minimum;
        }

        var broad = SampleValueNoise(seed, topologyTileX, SoilMacroSpacingTiles, 0xA54FF53A5F1D36F1UL);
        var detail = SampleValueNoise(seed, topologyTileX, SoilDetailSpacingTiles, 0x510E527FADE682D1UL);
        var normalized = Math.Clamp((broad * 0.72f + detail * 0.28f + 1f) * 0.5f, 0f, 1f);
        var depth = Lerp(minimum, maximum, normalized) * Math.Clamp(depthMultiplier, 0.5f, 2f);
        return Math.Max(1, (int)MathF.Round(depth));
    }

    private static float SampleValueNoise(int seed, int tileX, int spacing, ulong salt)
    {
        var anchor = FloorDivide(tileX, spacing);
        var anchorTileX = anchor * spacing;
        var t = (tileX - anchorTileX) / (float)spacing;
        var smooth = Quintic(Math.Clamp(t, 0f, 1f));
        var left = HashSigned(seed, anchor, salt);
        var right = HashSigned(seed, anchor + 1, salt);
        return Lerp(left, right, smooth);
    }

    private static float HashSigned(int seed, long coordinate, ulong salt)
    {
        var value = unchecked((ulong)(uint)seed) ^ unchecked((ulong)coordinate) ^ salt;
        value ^= value >> 30;
        value *= 0xBF58476D1CE4E5B9UL;
        value ^= value >> 27;
        value *= 0x94D049BB133111EBUL;
        value ^= value >> 31;
        return ((value >> 40) / 8_388_607.5f) - 1f;
    }

    private static long FloorDivide(long value, int divisor)
    {
        var quotient = value / divisor;
        var remainder = value % divisor;
        return remainder < 0 ? quotient - 1 : quotient;
    }

    private static int ClampSurface(WorldGenerationProfile profile, int surfaceY)
    {
        return Math.Clamp(
            surfaceY,
            Math.Max(3, profile.HeightTiles / 8),
            Math.Max(4, profile.HeightTiles / 2));
    }

    private static float Quintic(float value)
    {
        return value * value * value * (value * (value * 6f - 15f) + 10f);
    }

    private static float SmoothStep(float value)
    {
        value = Math.Clamp(value, 0f, 1f);
        return value * value * (3f - 2f * value);
    }

    private static float Lerp(float from, float to, float amount)
    {
        return from + (to - from) * amount;
    }
}
