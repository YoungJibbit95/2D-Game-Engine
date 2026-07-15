using Game.Core.Runtime;
using Game.Core.Weather;

namespace Game.Core.Audio;

public enum AudioLoopChannel
{
    Music,
    AmbientPrimary,
    AmbientWeather,
    AmbientEvent
}

public sealed record SoundscapeEventDefinition
{
    public string? LoopId { get; init; }

    public string? StartStingerId { get; init; }

    public float Volume { get; init; } = 1f;
}

public sealed record SoundscapeDefinition
{
    public required string Id { get; init; }

    public string? DayMusicLoopId { get; init; }

    public string? NightMusicLoopId { get; init; }

    public string? UndergroundMusicLoopId { get; init; }

    public string? SurfaceAmbientLoopId { get; init; }

    public string? UndergroundAmbientLoopId { get; init; }

    public string? RainLoopId { get; init; }

    public string? StormLoopId { get; init; }

    public string? FogLoopId { get; init; }

    public string? RainStartStingerId { get; init; }

    public string? StormStartStingerId { get; init; }

    public IReadOnlyDictionary<string, SoundscapeEventDefinition> WorldEvents { get; init; } =
        new Dictionary<string, SoundscapeEventDefinition>(StringComparer.OrdinalIgnoreCase);

    public float MusicVolume { get; init; } = 1f;

    public float AmbientVolume { get; init; } = 1f;

    public float WeatherVolume { get; init; } = 0.75f;

    public float CrossfadeSeconds { get; init; } = 1.5f;

    public static void Validate(SoundscapeDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (string.IsNullOrWhiteSpace(definition.Id))
        {
            throw new InvalidDataException("Soundscape definitions require an ID.");
        }

        ValidateOptionalId(definition.DayMusicLoopId);
        ValidateOptionalId(definition.NightMusicLoopId);
        ValidateOptionalId(definition.UndergroundMusicLoopId);
        ValidateOptionalId(definition.SurfaceAmbientLoopId);
        ValidateOptionalId(definition.UndergroundAmbientLoopId);
        ValidateOptionalId(definition.RainLoopId);
        ValidateOptionalId(definition.StormLoopId);
        ValidateOptionalId(definition.FogLoopId);
        ValidateOptionalId(definition.RainStartStingerId);
        ValidateOptionalId(definition.StormStartStingerId);
        ValidateGain(definition.MusicVolume, nameof(MusicVolume));
        ValidateGain(definition.AmbientVolume, nameof(AmbientVolume));
        ValidateGain(definition.WeatherVolume, nameof(WeatherVolume));
        if (!float.IsFinite(definition.CrossfadeSeconds) || definition.CrossfadeSeconds < 0f)
        {
            throw new InvalidDataException("Soundscape crossfade duration must be finite and non-negative.");
        }

        foreach (var pair in definition.WorldEvents)
        {
            if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value is null)
            {
                throw new InvalidDataException("Soundscape world-event entries require IDs and definitions.");
            }

            ValidateOptionalId(pair.Value.LoopId);
            ValidateOptionalId(pair.Value.StartStingerId);
            ValidateGain(pair.Value.Volume, nameof(SoundscapeEventDefinition.Volume));
        }
    }

    private static void ValidateOptionalId(string? id)
    {
        if (id is not null && string.IsNullOrWhiteSpace(id))
        {
            throw new InvalidDataException("Optional audio IDs must be null or non-empty.");
        }
    }

    private static void ValidateGain(float value, string name)
    {
        if (!float.IsFinite(value) || value is < 0f or > 1f)
        {
            throw new InvalidDataException($"{name} must be between zero and one.");
        }
    }
}

public sealed class SoundscapeCatalog
{
    private readonly Dictionary<string, SoundscapeDefinition> _definitions =
        new(StringComparer.OrdinalIgnoreCase);

    public int Count => _definitions.Count;

    public void Register(SoundscapeDefinition definition, bool replace = false)
    {
        SoundscapeDefinition.Validate(definition);
        if (!replace && _definitions.ContainsKey(definition.Id))
        {
            throw new InvalidDataException($"Duplicate soundscape ID '{definition.Id}'.");
        }

        _definitions[definition.Id] = definition;
    }

    public bool TryGet(string id, out SoundscapeDefinition? definition)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            definition = null;
            return false;
        }

        return _definitions.TryGetValue(id, out definition);
    }
}

public readonly record struct ResolvedSoundscape(
    string SoundscapeId,
    bool DefinitionFound,
    string? MusicLoopId,
    string? AmbientLoopId,
    string? WeatherLoopId,
    string? WeatherStingerId,
    string? WorldEventLoopId,
    string? WorldEventStingerId,
    float MusicVolume,
    float AmbientVolume,
    float WeatherVolume,
    float EventVolume,
    float CrossfadeSeconds,
    float CaveReverb);

public sealed class SoundscapeResolver
{
    private readonly SoundscapeCatalog _catalog;

    public SoundscapeResolver(SoundscapeCatalog catalog)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    }

    public ResolvedSoundscape Resolve(
        in LivingWorldFrameSnapshot livingWorld,
        in WorldTimeFrameSnapshot worldTime)
    {
        if (!_catalog.TryGet(livingWorld.SoundscapeId, out var definition) || definition is null)
        {
            return new ResolvedSoundscape(
                livingWorld.SoundscapeId,
                false,
                null,
                null,
                null,
                null,
                null,
                null,
                0f,
                0f,
                0f,
                0f,
                0f,
                livingWorld.Presentation.CaveReverb);
        }

        var music = livingWorld.IsUnderground
            ? definition.UndergroundMusicLoopId ?? definition.NightMusicLoopId ?? definition.DayMusicLoopId
            : worldTime.IsNight
                ? definition.NightMusicLoopId ?? definition.DayMusicLoopId
                : definition.DayMusicLoopId ?? definition.NightMusicLoopId;
        var ambient = livingWorld.IsUnderground
            ? definition.UndergroundAmbientLoopId ?? definition.SurfaceAmbientLoopId
            : definition.SurfaceAmbientLoopId;
        var weatherLoop = livingWorld.IsUnderground ? null : ResolveWeatherLoop(definition, livingWorld.Weather);
        var weatherStinger = livingWorld.IsUnderground ? null : ResolveWeatherStinger(definition, livingWorld.Weather);
        SoundscapeEventDefinition? eventDefinition = null;
        if (livingWorld.IsWorldEventActive && livingWorld.WorldEventId is not null)
        {
            TryResolveEvent(definition.WorldEvents, livingWorld.WorldEventId, out eventDefinition);
        }

        return new ResolvedSoundscape(
            definition.Id,
            true,
            music,
            ambient,
            weatherLoop,
            weatherStinger,
            eventDefinition?.LoopId,
            eventDefinition?.StartStingerId,
            definition.MusicVolume,
            definition.AmbientVolume,
            definition.WeatherVolume * livingWorld.WeatherIntensity,
            (eventDefinition?.Volume ?? 0f) * livingWorld.WorldEventIntensity,
            definition.CrossfadeSeconds,
            livingWorld.Presentation.CaveReverb);
    }

    private static string? ResolveWeatherLoop(SoundscapeDefinition definition, WeatherKind weather)
    {
        return weather switch
        {
            WeatherKind.Rain => definition.RainLoopId,
            WeatherKind.Storm => definition.StormLoopId ?? definition.RainLoopId,
            WeatherKind.Fog => definition.FogLoopId,
            _ => null
        };
    }

    private static string? ResolveWeatherStinger(SoundscapeDefinition definition, WeatherKind weather)
    {
        return weather switch
        {
            WeatherKind.Rain => definition.RainStartStingerId,
            WeatherKind.Storm => definition.StormStartStingerId ?? definition.RainStartStingerId,
            _ => null
        };
    }

    private static bool TryResolveEvent(
        IReadOnlyDictionary<string, SoundscapeEventDefinition> definitions,
        string eventId,
        out SoundscapeEventDefinition? definition)
    {
        if (definitions.TryGetValue(eventId, out definition))
        {
            return true;
        }

        foreach (var pair in definitions)
        {
            if (string.Equals(pair.Key, eventId, StringComparison.OrdinalIgnoreCase))
            {
                definition = pair.Value;
                return true;
            }
        }

        definition = null;
        return false;
    }
}
