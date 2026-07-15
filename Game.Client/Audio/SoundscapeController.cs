using Game.Core.Audio;
using Game.Core.Runtime;
using System.Numerics;

namespace Game.Client.Audio;

public readonly record struct SoundscapeControllerTelemetry(
    SoundscapePlannerTelemetry Planner,
    AudioManagerTelemetry Audio,
    string? DesiredMusicLoopId,
    string? DesiredAmbientLoopId,
    string? DesiredWeatherLoopId,
    string? DesiredEventLoopId);

public sealed class SoundscapeController
{
    private readonly SoundscapeCommandPlanner _planner;
    private readonly AudioManager _audio;
    private readonly AudioPresentationCommand[] _commands =
        new AudioPresentationCommand[SoundscapeCommandPlanner.MaximumCommandsPerFrame];
    private string? _desiredMusic;
    private string? _desiredAmbient;
    private string? _desiredWeather;
    private string? _desiredEvent;

    public SoundscapeController(SoundscapeCommandPlanner planner, AudioManager audio)
    {
        _planner = planner ?? throw new ArgumentNullException(nameof(planner));
        _audio = audio ?? throw new ArgumentNullException(nameof(audio));
    }

    public void Update(
        in LivingWorldFrameSnapshot livingWorld,
        in WorldTimeFrameSnapshot worldTime,
        float elapsedSeconds,
        Vector2 listenerPosition)
    {
        var count = _planner.Plan(livingWorld, worldTime, _commands);
        for (var index = 0; index < count; index++)
        {
            ref readonly var command = ref _commands[index];
            Execute(command);
        }

        _audio.Update(elapsedSeconds, new AudioListener(listenerPosition));
    }

    public SoundscapeControllerTelemetry CaptureTelemetry()
    {
        return new SoundscapeControllerTelemetry(
            _planner.Telemetry,
            _audio.CaptureTelemetry(),
            _desiredMusic,
            _desiredAmbient,
            _desiredWeather,
            _desiredEvent);
    }

    public void Reset(float fadeSeconds = 0.25f)
    {
        _planner.Reset();
        for (var channel = AudioLoopChannel.Music; channel <= AudioLoopChannel.AmbientEvent; channel++)
        {
            _audio.StopLoop(channel, fadeSeconds);
        }

        _desiredMusic = null;
        _desiredAmbient = null;
        _desiredWeather = null;
        _desiredEvent = null;
    }

    private void Execute(in AudioPresentationCommand command)
    {
        switch (command.Kind)
        {
            case AudioPresentationCommandKind.TransitionLoop:
                if (command.AudioId is not null)
                {
                    _audio.TransitionLoop(new AudioLoopRequest(
                        command.AudioId,
                        command.Channel,
                        command.Bus,
                        command.Volume,
                        command.Priority,
                        command.FadeSeconds));
                    SetDesired(command.Channel, command.AudioId);
                }

                break;
            case AudioPresentationCommandKind.StopLoop:
                _audio.StopLoop(command.Channel, command.FadeSeconds);
                SetDesired(command.Channel, null);
                break;
            case AudioPresentationCommandKind.PlayStinger:
                if (command.AudioId is not null)
                {
                    _audio.PlayOneShot(new AudioPlayRequest(
                        command.AudioId,
                        command.Bus,
                        command.Volume,
                        Priority: command.Priority,
                        CooldownSeconds: 0.1f));
                }

                break;
            default:
                throw new InvalidDataException("Unknown soundscape presentation command.");
        }
    }

    private void SetDesired(AudioLoopChannel channel, string? audioId)
    {
        switch (channel)
        {
            case AudioLoopChannel.Music:
                _desiredMusic = audioId;
                break;
            case AudioLoopChannel.AmbientPrimary:
                _desiredAmbient = audioId;
                break;
            case AudioLoopChannel.AmbientWeather:
                _desiredWeather = audioId;
                break;
            case AudioLoopChannel.AmbientEvent:
                _desiredEvent = audioId;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(channel));
        }
    }
}
