using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;

namespace Game.Client.Audio;

public readonly record struct SoundEffectRegistryTelemetry(
    int RegisteredAssets,
    long SuccessfulLoads,
    long FailedLoads,
    long DuplicateRegistrations,
    string? LastFailedAssetId);

public sealed class SoundEffectRegistry : IAudioVoiceFactory
{
    private readonly Dictionary<string, SoundEffect> _effects = new(StringComparer.OrdinalIgnoreCase);
    private long _successfulLoads;
    private long _failedLoads;
    private long _duplicates;
    private string? _lastFailedAssetId;

    public SoundEffectRegistryTelemetry Telemetry => new(
        _effects.Count,
        _successfulLoads,
        _failedLoads,
        _duplicates,
        _lastFailedAssetId);

    public bool Register(string audioId, SoundEffect effect, bool replace = false)
    {
        ValidateId(audioId, nameof(audioId));
        ArgumentNullException.ThrowIfNull(effect);
        if (!replace && _effects.ContainsKey(audioId))
        {
            _duplicates++;
            return false;
        }

        _effects[audioId] = effect;
        return true;
    }

    public bool TryLoad(ContentManager content, string audioId, string assetName, bool replace = false)
    {
        ArgumentNullException.ThrowIfNull(content);
        ValidateId(audioId, nameof(audioId));
        ValidateId(assetName, nameof(assetName));
        if (!replace && _effects.ContainsKey(audioId))
        {
            _duplicates++;
            return false;
        }

        try
        {
            var effect = content.Load<SoundEffect>(assetName);
            _effects[audioId] = effect;
            _successfulLoads++;
            return true;
        }
        catch (Exception exception) when (IsRecoverableLoadFailure(exception))
        {
            _failedLoads++;
            _lastFailedAssetId = audioId;
            return false;
        }
    }

    public bool Contains(string audioId)
    {
        return !string.IsNullOrWhiteSpace(audioId) && _effects.ContainsKey(audioId);
    }

    public bool TryCreateVoice(string audioId, out IAudioVoice? voice)
    {
        if (string.IsNullOrWhiteSpace(audioId) || !_effects.TryGetValue(audioId, out var effect))
        {
            voice = null;
            return false;
        }

        try
        {
            voice = new MonoGameAudioVoice(effect.CreateInstance());
            return true;
        }
        catch (Exception exception) when (IsRecoverablePlaybackFailure(exception))
        {
            voice = null;
            return false;
        }
    }

    public void Clear()
    {
        _effects.Clear();
    }

    private static bool IsRecoverableLoadFailure(Exception exception)
    {
        return exception is ContentLoadException or InvalidOperationException or NotSupportedException;
    }

    private static bool IsRecoverablePlaybackFailure(Exception exception)
    {
        return exception is NoAudioHardwareException or InstancePlayLimitException or
            InvalidOperationException or ObjectDisposedException;
    }

    private static void ValidateId(string id, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Audio IDs must not be empty.", parameterName);
        }
    }

    private sealed class MonoGameAudioVoice : IAudioVoice
    {
        private readonly SoundEffectInstance _instance;

        public MonoGameAudioVoice(SoundEffectInstance instance)
        {
            _instance = instance;
        }

        public bool IsStopped => _instance.IsDisposed || _instance.State == SoundState.Stopped;

        public bool IsLooped
        {
            get => _instance.IsLooped;
            set => _instance.IsLooped = value;
        }

        public float Volume
        {
            get => _instance.Volume;
            set => _instance.Volume = value;
        }

        public float Pan
        {
            get => _instance.Pan;
            set => _instance.Pan = value;
        }

        public float Pitch
        {
            get => _instance.Pitch;
            set => _instance.Pitch = value;
        }

        public void Play()
        {
            _instance.Play();
        }

        public void Stop()
        {
            if (!_instance.IsDisposed)
            {
                _instance.Stop(immediate: true);
            }
        }

        public void Dispose()
        {
            _instance.Dispose();
        }
    }
}
