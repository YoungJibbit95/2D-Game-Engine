using Game.Core.Biomes;
using Game.Core.World.Generation;

namespace Game.Core.Weather;

public enum WeatherKind
{
    Clear,
    Rain,
    Storm,
    Fog
}

public readonly record struct WeatherState(
    WeatherKind Kind,
    long StartTick,
    long EndTickExclusive,
    float Intensity,
    float Wind,
    float CloudCover,
    float Visibility,
    float AmbientLightMultiplier);

public readonly record struct WeatherSystemSnapshot(
    int FormatVersion,
    string BiomeId,
    long Sequence,
    long LastAdvancedTick,
    WeatherState Previous,
    WeatherState Current,
    long TransitionStartTick,
    long TransitionEndTickExclusive)
{
    public const int CurrentFormatVersion = 1;
}

public readonly record struct WeatherTransitionState(
    WeatherState From,
    WeatherState To,
    WeatherState Sample,
    float Progress);

public sealed class DeterministicWeatherSystem
{
    private const ulong WeatherSalt = 0xC6A4A7935BD1E995UL;
    private readonly int _seed;

    public DeterministicWeatherSystem(int seed)
    {
        _seed = seed;
    }

    public WeatherSystemSnapshot CreateSnapshot(BiomeDefinition biome, long startTick = 0)
    {
        ArgumentNullException.ThrowIfNull(biome);
        if (startTick < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(startTick));
        }

        var current = CreatePeriod(biome, sequence: 0, startTick, previousKind: null);
        return new WeatherSystemSnapshot(
            WeatherSystemSnapshot.CurrentFormatVersion,
            biome.Id,
            0,
            startTick,
            current,
            current,
            startTick,
            startTick);
    }

    public WeatherSystemSnapshot Advance(
        BiomeDefinition biome,
        WeatherSystemSnapshot snapshot,
        long worldTick)
    {
        ArgumentNullException.ThrowIfNull(biome);
        ValidateSnapshot(snapshot);
        if (worldTick < snapshot.LastAdvancedTick)
        {
            throw new ArgumentOutOfRangeException(nameof(worldTick), "Weather snapshots cannot advance backwards.");
        }

        var currentSnapshot = snapshot;
        if (!string.Equals(snapshot.BiomeId, biome.Id, StringComparison.OrdinalIgnoreCase))
        {
            var previous = ResolveTransition(snapshot, snapshot.LastAdvancedTick).Sample;
            var current = CreatePeriod(biome, sequence: 0, snapshot.LastAdvancedTick, previous.Kind);
            currentSnapshot = new WeatherSystemSnapshot(
                WeatherSystemSnapshot.CurrentFormatVersion,
                biome.Id,
                0,
                snapshot.LastAdvancedTick,
                previous,
                current,
                snapshot.LastAdvancedTick,
                Math.Min(
                    current.EndTickExclusive,
                    SaturatingAdd(snapshot.LastAdvancedTick, biome.Weather.TransitionDurationTicks)));
        }

        while (worldTick >= currentSnapshot.Current.EndTickExclusive &&
               currentSnapshot.Current.EndTickExclusive < long.MaxValue)
        {
            var previous = currentSnapshot.Current;
            var sequence = checked(currentSnapshot.Sequence + 1L);
            var current = CreatePeriod(biome, sequence, previous.EndTickExclusive, previous.Kind);
            currentSnapshot = currentSnapshot with
            {
                Sequence = sequence,
                Previous = previous,
                Current = current,
                TransitionStartTick = current.StartTick,
                TransitionEndTickExclusive = SaturatingAdd(
                    current.StartTick,
                    Math.Min(
                        (long)biome.Weather.TransitionDurationTicks,
                        current.EndTickExclusive - current.StartTick))
            };
        }

        return currentSnapshot with { LastAdvancedTick = worldTick };
    }

    public WeatherTransitionState ResolveTransition(WeatherSystemSnapshot snapshot, long worldTick)
    {
        ValidateSnapshot(snapshot);
        if (worldTick < snapshot.Current.StartTick || worldTick >= snapshot.Current.EndTickExclusive)
        {
            throw new ArgumentOutOfRangeException(nameof(worldTick));
        }

        var transitionDuration = snapshot.TransitionEndTickExclusive - snapshot.TransitionStartTick;
        var progress = transitionDuration <= 0
            ? 1f
            : Math.Clamp(
                (worldTick - snapshot.TransitionStartTick) / (float)transitionDuration,
                0f,
                1f);
        return new WeatherTransitionState(
            snapshot.Previous,
            snapshot.Current,
            Blend(snapshot.Previous, snapshot.Current, progress),
            progress);
    }

    public WeatherState GetState(BiomeDefinition biome, long worldTick)
    {
        ArgumentNullException.ThrowIfNull(biome);
        if (worldTick < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(worldTick));
        }

        var salt = GetSalt(biome.Id);
        var duration = DeterministicCoordinateHash.Range(
            _seed,
            0,
            0,
            salt,
            biome.Weather.MinDurationTicks,
            biome.Weather.MaxDurationTicks);
        var period = worldTick / duration;
        return CreatePeriod(
            biome,
            period,
            period * duration,
            previousKind: null,
            durationOverride: duration);
    }

    public static void ValidateSnapshot(WeatherSystemSnapshot snapshot)
    {
        if (snapshot.FormatVersion != WeatherSystemSnapshot.CurrentFormatVersion ||
            string.IsNullOrWhiteSpace(snapshot.BiomeId) ||
            snapshot.Sequence < 0 ||
            snapshot.LastAdvancedTick < 0 ||
            snapshot.Current.StartTick < 0 ||
            snapshot.Current.EndTickExclusive <= snapshot.Current.StartTick ||
            snapshot.LastAdvancedTick < snapshot.Current.StartTick ||
            snapshot.LastAdvancedTick >= snapshot.Current.EndTickExclusive ||
            snapshot.TransitionStartTick < snapshot.Current.StartTick ||
            snapshot.TransitionEndTickExclusive < snapshot.TransitionStartTick ||
            snapshot.TransitionEndTickExclusive > snapshot.Current.EndTickExclusive)
        {
            throw new InvalidDataException("Weather snapshot is invalid or uses an unsupported format.");
        }
    }

    private WeatherState CreatePeriod(
        BiomeDefinition biome,
        long sequence,
        long startTick,
        WeatherKind? previousKind,
        int? durationOverride = null)
    {
        var salt = GetSalt(biome.Id);
        var duration = durationOverride ??
            DeterministicCoordinateHash.Range(
                _seed,
                sequence,
                0,
                salt,
                biome.Weather.MinDurationTicks,
                biome.Weather.MaxDurationTicks);
        var kind = SelectKind(
            biome.Weather,
            DeterministicCoordinateHash.Hash(_seed, sequence, salt: salt + 1),
            previousKind);
        var intensity = kind == WeatherKind.Clear
            ? 0f
            : (float)(0.25d + DeterministicCoordinateHash.Unit(_seed, sequence, salt: salt + 2) * 0.75d);
        var windSign = DeterministicCoordinateHash.Unit(_seed, sequence, salt: salt + 3) < 0.5d ? -1f : 1f;
        var wind = windSign * (float)DeterministicCoordinateHash.Unit(_seed, sequence, salt: salt + 4);
        var cloudCover = kind switch
        {
            WeatherKind.Clear => intensity * 0.1f,
            WeatherKind.Fog => 0.55f + intensity * 0.25f,
            WeatherKind.Rain => 0.65f + intensity * 0.25f,
            WeatherKind.Storm => 0.85f + intensity * 0.15f,
            _ => 0f
        };
        var visibility = kind switch
        {
            WeatherKind.Fog => 1f - intensity * 0.7f,
            WeatherKind.Storm => 1f - intensity * 0.45f,
            WeatherKind.Rain => 1f - intensity * 0.25f,
            _ => 1f
        };

        return new WeatherState(
            kind,
            startTick,
            SaturatingAdd(startTick, duration),
            intensity,
            wind,
            Math.Clamp(cloudCover, 0f, 1f),
            Math.Clamp(visibility, 0.15f, 1f),
            Math.Clamp(1f - cloudCover * 0.45f, 0.45f, 1f));
    }

    private static WeatherState Blend(WeatherState from, WeatherState to, float progress)
    {
        return to with
        {
            Intensity = Lerp(from.Intensity, to.Intensity, progress),
            Wind = Lerp(from.Wind, to.Wind, progress),
            CloudCover = Lerp(from.CloudCover, to.CloudCover, progress),
            Visibility = Lerp(from.Visibility, to.Visibility, progress),
            AmbientLightMultiplier = Lerp(
                from.AmbientLightMultiplier,
                to.AmbientLightMultiplier,
                progress)
        };
    }

    private static WeatherKind SelectKind(
        BiomeWeatherProfile profile,
        ulong hash,
        WeatherKind? excludedKind)
    {
        Span<int> weights = stackalloc int[4]
        {
            profile.ClearWeight,
            profile.RainWeight,
            profile.StormWeight,
            profile.FogWeight
        };
        var excludedIndex = excludedKind.HasValue ? (int)excludedKind.Value : -1;
        var totalWithoutExcluded = 0L;
        for (var index = 0; index < weights.Length; index++)
        {
            if (index != excludedIndex)
            {
                totalWithoutExcluded += weights[index];
            }
        }

        var exclude = excludedIndex >= 0 && totalWithoutExcluded > 0;
        var total = exclude ? totalWithoutExcluded : totalWithoutExcluded +
            (excludedIndex >= 0 ? weights[excludedIndex] : 0);
        var roll = (long)(hash % (ulong)total);
        for (var index = 0; index < weights.Length; index++)
        {
            if (exclude && index == excludedIndex)
            {
                continue;
            }

            roll -= weights[index];
            if (roll < 0)
            {
                return (WeatherKind)index;
            }
        }

        return WeatherKind.Clear;
    }

    private static float Lerp(float from, float to, float progress)
    {
        return from + (to - from) * progress;
    }

    private static ulong GetSalt(string biomeId)
    {
        return DeterministicCoordinateHash.Salt(biomeId) ^ WeatherSalt;
    }

    private static long SaturatingAdd(long value, long amount)
    {
        return value > long.MaxValue - amount ? long.MaxValue : value + amount;
    }
}

public readonly record struct AmbientState(
    string BiomeId,
    string? SubBiomeId,
    string SoundscapeId,
    bool IsCave,
    float Light,
    float Visibility,
    float Temperature,
    float Humidity,
    float Wind,
    WeatherKind Weather);

public sealed class AmbientStateService
{
    public AmbientState Resolve(
        BiomeDefinition biome,
        SubBiomeDefinition? subBiome,
        WeatherTransitionState weather,
        bool isCave,
        float daylight)
    {
        return Resolve(biome, subBiome, weather.Sample, isCave, daylight);
    }

    public AmbientState Resolve(
        BiomeDefinition biome,
        SubBiomeDefinition? subBiome,
        WeatherState weather,
        bool isCave,
        float daylight)
    {
        ArgumentNullException.ThrowIfNull(biome);
        var soundscape = subBiome?.SoundscapeId ??
            (isCave ? biome.Ambient.CaveSoundscapeId : biome.Ambient.SurfaceSoundscapeId);
        var light = biome.Ambient.BaseLight * weather.AmbientLightMultiplier;
        if (!isCave)
        {
            light *= Math.Clamp(daylight, 0f, 1f);
        }

        return new AmbientState(
            biome.Id,
            subBiome?.Id,
            soundscape,
            isCave,
            Math.Clamp(light, 0f, 1f),
            Math.Clamp(biome.Ambient.BaseVisibility * weather.Visibility * (isCave ? 0.65f : 1f), 0f, 1f),
            Math.Clamp(biome.Climate.Temperature + (subBiome?.TemperatureOffset ?? 0f), 0f, 1f),
            Math.Clamp(biome.Climate.Humidity + (subBiome?.HumidityOffset ?? 0f), 0f, 1f),
            weather.Wind,
            weather.Kind);
    }
}
