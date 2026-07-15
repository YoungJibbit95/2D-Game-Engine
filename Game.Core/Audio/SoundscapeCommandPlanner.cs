using Game.Core.Runtime;
using Game.Core.Weather;

namespace Game.Core.Audio;

public enum AudioPresentationCommandKind
{
    TransitionLoop,
    StopLoop,
    PlayStinger
}

public readonly record struct AudioPresentationCommand(
    AudioPresentationCommandKind Kind,
    AudioLoopChannel Channel,
    AudioBus Bus,
    string? AudioId,
    float Volume,
    float FadeSeconds,
    int Priority);

public readonly record struct SoundscapePlannerTelemetry(
    long FramesPlanned,
    long LoopTransitions,
    long LoopStops,
    long Stingers,
    long MissingDefinitions,
    string? LastMissingSoundscapeId);

public sealed class SoundscapeCommandPlanner
{
    public const int MaximumCommandsPerFrame = 8;
    private readonly SoundscapeResolver _resolver;
    private readonly string?[] _loopIds = new string?[4];
    private readonly float[] _loopVolumes = new float[4];
    private WeatherKind _lastWeather;
    private string? _lastEventId;
    private bool _initialized;
    private long _frames;
    private long _transitions;
    private long _stops;
    private long _stingers;
    private long _missingDefinitions;
    private string? _lastMissingSoundscapeId;

    public SoundscapeCommandPlanner(SoundscapeResolver resolver)
    {
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
    }

    public SoundscapePlannerTelemetry Telemetry => new(
        _frames,
        _transitions,
        _stops,
        _stingers,
        _missingDefinitions,
        _lastMissingSoundscapeId);

    public int Plan(
        in LivingWorldFrameSnapshot livingWorld,
        in WorldTimeFrameSnapshot worldTime,
        Span<AudioPresentationCommand> destination)
    {
        if (destination.Length < MaximumCommandsPerFrame)
        {
            throw new ArgumentException($"At least {MaximumCommandsPerFrame} command slots are required.", nameof(destination));
        }

        _frames++;
        var desired = _resolver.Resolve(livingWorld, worldTime);
        var count = 0;
        if (!desired.DefinitionFound)
        {
            _missingDefinitions++;
            _lastMissingSoundscapeId = desired.SoundscapeId;
        }

        count = PlanLoop(
            AudioLoopChannel.Music,
            AudioBus.Music,
            desired.MusicLoopId,
            desired.MusicVolume,
            desired.CrossfadeSeconds,
            100,
            destination,
            count);
        count = PlanLoop(
            AudioLoopChannel.AmbientPrimary,
            AudioBus.Ambient,
            desired.AmbientLoopId,
            desired.AmbientVolume,
            desired.CrossfadeSeconds,
            80,
            destination,
            count);
        count = PlanLoop(
            AudioLoopChannel.AmbientWeather,
            AudioBus.Ambient,
            desired.WeatherLoopId,
            desired.WeatherVolume,
            MathF.Min(desired.CrossfadeSeconds, 1f),
            70,
            destination,
            count);
        count = PlanLoop(
            AudioLoopChannel.AmbientEvent,
            AudioBus.Ambient,
            desired.WorldEventLoopId,
            desired.EventVolume,
            desired.CrossfadeSeconds,
            90,
            destination,
            count);

        if (_initialized && livingWorld.Weather != _lastWeather && desired.WeatherStingerId is not null)
        {
            destination[count++] = new AudioPresentationCommand(
                AudioPresentationCommandKind.PlayStinger,
                AudioLoopChannel.AmbientWeather,
                AudioBus.Ambient,
                desired.WeatherStingerId,
                desired.WeatherVolume,
                0f,
                85);
            _stingers++;
        }

        var activeEventId = livingWorld.IsWorldEventActive ? livingWorld.WorldEventId : null;
        if (activeEventId is not null &&
            (!_initialized || !string.Equals(activeEventId, _lastEventId, StringComparison.OrdinalIgnoreCase)) &&
            desired.WorldEventStingerId is not null)
        {
            destination[count++] = new AudioPresentationCommand(
                AudioPresentationCommandKind.PlayStinger,
                AudioLoopChannel.AmbientEvent,
                AudioBus.Sfx,
                desired.WorldEventStingerId,
                MathF.Max(0.25f, desired.EventVolume),
                0f,
                95);
            _stingers++;
        }

        _lastWeather = livingWorld.Weather;
        _lastEventId = activeEventId;
        _initialized = true;
        return count;
    }

    public void Reset()
    {
        Array.Clear(_loopIds);
        Array.Clear(_loopVolumes);
        _lastWeather = default;
        _lastEventId = null;
        _initialized = false;
    }

    private int PlanLoop(
        AudioLoopChannel channel,
        AudioBus bus,
        string? desiredId,
        float volume,
        float fadeSeconds,
        int priority,
        Span<AudioPresentationCommand> destination,
        int count)
    {
        var index = (int)channel;
        if (string.Equals(_loopIds[index], desiredId, StringComparison.OrdinalIgnoreCase) &&
            (desiredId is null || MathF.Abs(_loopVolumes[index] - volume) <= 0.01f))
        {
            return count;
        }

        if (desiredId is null)
        {
            if (_loopIds[index] is not null)
            {
                destination[count++] = new AudioPresentationCommand(
                    AudioPresentationCommandKind.StopLoop,
                    channel,
                    bus,
                    null,
                    0f,
                    fadeSeconds,
                    priority);
                _stops++;
            }
        }
        else
        {
            destination[count++] = new AudioPresentationCommand(
                AudioPresentationCommandKind.TransitionLoop,
                channel,
                bus,
                desiredId,
                volume,
                fadeSeconds,
                priority);
            _transitions++;
        }

        _loopIds[index] = desiredId;
        _loopVolumes[index] = desiredId is null ? 0f : volume;
        return count;
    }
}
