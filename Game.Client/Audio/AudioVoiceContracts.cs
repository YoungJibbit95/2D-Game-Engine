using Game.Core.Audio;
using System.Numerics;

namespace Game.Client.Audio;

public interface IAudioVoice : IDisposable
{
    bool IsStopped { get; }

    bool IsLooped { get; set; }

    float Volume { get; set; }

    float Pan { get; set; }

    float Pitch { get; set; }

    void Play();

    void Stop();
}

public interface IAudioVoiceFactory
{
    bool TryCreateVoice(string audioId, out IAudioVoice? voice);
}

public enum AudioPlayStatus
{
    Started,
    MissingAsset,
    Cooldown,
    VoiceBudget,
    InvalidRequest,
    PlaybackFailure
}

public readonly record struct AudioListener(Vector2 Position);

public readonly record struct AudioPlayRequest(
    string AudioId,
    AudioBus Bus,
    float Volume = 1f,
    float Pitch = 0f,
    int Priority = 50,
    float CooldownSeconds = 0f,
    bool IsSpatial = false,
    Vector2 SourcePosition = default,
    float MaximumDistance = 960f);

public readonly record struct AudioLoopRequest(
    string AudioId,
    AudioLoopChannel Channel,
    AudioBus Bus,
    float Volume = 1f,
    int Priority = 80,
    float CrossfadeSeconds = 1.5f);

public sealed record AudioManagerOptions
{
    public int MaximumVoices { get; init; } = 32;

    public int MaximumMusicVoices { get; init; } = 2;

    public int MaximumAmbientVoices { get; init; } = 8;

    public int MaximumSfxVoices { get; init; } = 18;

    public int MaximumUiVoices { get; init; } = 4;

    public float MinimumAudibleGain { get; init; } = 0.001f;

    public static void Validate(AudioManagerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.MaximumVoices <= 0 ||
            options.MaximumMusicVoices <= 0 ||
            options.MaximumAmbientVoices <= 0 ||
            options.MaximumSfxVoices <= 0 ||
            options.MaximumUiVoices <= 0 ||
            options.MaximumMusicVoices > options.MaximumVoices ||
            options.MaximumAmbientVoices > options.MaximumVoices ||
            options.MaximumSfxVoices > options.MaximumVoices ||
            options.MaximumUiVoices > options.MaximumVoices ||
            !float.IsFinite(options.MinimumAudibleGain) ||
            options.MinimumAudibleGain is < 0f or > 1f)
        {
            throw new InvalidDataException("Audio manager options contain invalid voice or gain budgets.");
        }
    }
}

public readonly record struct AudioManagerTelemetry(
    int ActiveVoices,
    int MusicVoices,
    int AmbientVoices,
    int SfxVoices,
    int UiVoices,
    long StartedVoices,
    long CompletedVoices,
    long MissingAssets,
    long CooldownRejections,
    long BudgetRejections,
    long InvalidRequests,
    long PlaybackFailures,
    long VoiceSteals,
    long LoopTransitions,
    string? LastMissingAssetId,
    AudioMixerSnapshot Mixer);
