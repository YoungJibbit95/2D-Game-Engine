using Game.Core.Biomes;

namespace Game.Core.Weather;

public static class BiomeWeatherEligibility
{
    public static bool IsAllowed(BiomeWeatherProfile profile, WeatherKind kind)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return kind switch
        {
            WeatherKind.Clear => profile.ClearWeight > 0,
            WeatherKind.Rain => profile.RainWeight > 0,
            WeatherKind.Storm => profile.StormWeight > 0,
            WeatherKind.Fog => profile.FogWeight > 0,
            WeatherKind.Snow => profile.AllowsFrozenPrecipitation && profile.SnowWeight > 0,
            WeatherKind.Blizzard => profile.AllowsFrozenPrecipitation && profile.BlizzardWeight > 0,
            _ => false
        };
    }

    public static WeatherKind Normalize(BiomeWeatherProfile profile, WeatherKind requested)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (IsAllowed(profile, requested))
        {
            return requested;
        }

        return requested switch
        {
            WeatherKind.Snow => FirstAllowed(
                profile,
                WeatherKind.Rain,
                WeatherKind.Fog,
                WeatherKind.Clear,
                WeatherKind.Storm),
            WeatherKind.Blizzard => FirstAllowed(
                profile,
                WeatherKind.Storm,
                WeatherKind.Rain,
                WeatherKind.Fog,
                WeatherKind.Clear),
            WeatherKind.Storm => FirstAllowed(
                profile,
                WeatherKind.Rain,
                WeatherKind.Blizzard,
                WeatherKind.Snow,
                WeatherKind.Fog),
            WeatherKind.Rain => FirstAllowed(
                profile,
                WeatherKind.Snow,
                WeatherKind.Fog,
                WeatherKind.Clear,
                WeatherKind.Storm),
            WeatherKind.Fog => FirstAllowed(
                profile,
                WeatherKind.Clear,
                WeatherKind.Rain,
                WeatherKind.Snow,
                WeatherKind.Storm),
            _ => FirstAllowed(
                profile,
                WeatherKind.Clear,
                WeatherKind.Rain,
                WeatherKind.Snow,
                WeatherKind.Fog)
        };
    }

    public static WeatherState Normalize(BiomeWeatherProfile profile, in WeatherState state)
    {
        var normalizedKind = Normalize(profile, state.Kind);
        return normalizedKind == state.Kind
            ? state
            : WeatherAtmosphere.Apply(state with { Kind = normalizedKind }, normalizedKind);
    }

    private static WeatherKind FirstAllowed(
        BiomeWeatherProfile profile,
        WeatherKind first,
        WeatherKind second,
        WeatherKind third,
        WeatherKind fourth)
    {
        if (IsAllowed(profile, first))
        {
            return first;
        }

        if (IsAllowed(profile, second))
        {
            return second;
        }

        if (IsAllowed(profile, third))
        {
            return third;
        }

        if (IsAllowed(profile, fourth))
        {
            return fourth;
        }

        // Registry validation guarantees a positive weight; this also handles external snapshots.
        for (var value = (int)WeatherKind.Clear; value <= (int)WeatherKind.Blizzard; value++)
        {
            var candidate = (WeatherKind)value;
            if (IsAllowed(profile, candidate))
            {
                return candidate;
            }
        }

        return WeatherKind.Clear;
    }
}

internal static class WeatherAtmosphere
{
    public static WeatherState Apply(in WeatherState state, WeatherKind kind)
    {
        var intensity = kind == WeatherKind.Clear
            ? 0f
            : float.IsFinite(state.Intensity)
                ? Math.Clamp(state.Intensity, 0f, 1f)
                : 0f;
        var cloudCover = kind switch
        {
            WeatherKind.Clear => intensity * 0.1f,
            WeatherKind.Fog => 0.55f + intensity * 0.25f,
            WeatherKind.Rain => 0.65f + intensity * 0.25f,
            WeatherKind.Storm => 0.85f + intensity * 0.15f,
            WeatherKind.Snow => 0.58f + intensity * 0.27f,
            WeatherKind.Blizzard => 0.88f + intensity * 0.12f,
            _ => 0f
        };
        var visibility = kind switch
        {
            WeatherKind.Fog => 1f - intensity * 0.7f,
            WeatherKind.Storm => 1f - intensity * 0.45f,
            WeatherKind.Rain => 1f - intensity * 0.25f,
            WeatherKind.Snow => 1f - intensity * 0.2f,
            WeatherKind.Blizzard => 1f - intensity * 0.58f,
            _ => 1f
        };
        var ambientOcclusion = kind == WeatherKind.Snow ? 0.3f : 0.45f;

        return state with
        {
            Kind = kind,
            Intensity = intensity,
            CloudCover = Math.Clamp(cloudCover, 0f, 1f),
            Visibility = Math.Clamp(visibility, 0.15f, 1f),
            AmbientLightMultiplier = Math.Clamp(1f - cloudCover * ambientOcclusion, 0.45f, 1f)
        };
    }
}