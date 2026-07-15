using Game.Core.Audio;
using Microsoft.Xna.Framework.Audio;
using System.Numerics;

namespace Game.Client.Audio;

public sealed class AudioManager : IDisposable
{
    private readonly IAudioVoiceFactory _voiceFactory;
    private readonly AudioManagerOptions _options;
    private readonly VoiceSlot[] _slots;
    private readonly Dictionary<string, double> _lastPlaybackById = new(StringComparer.OrdinalIgnoreCase);
    private readonly AudioMixerModel _mixer = new();
    private double _clockSeconds;
    private long _sequence;
    private long _started;
    private long _completed;
    private long _missing;
    private long _cooldownRejections;
    private long _budgetRejections;
    private long _invalidRequests;
    private long _playbackFailures;
    private long _voiceSteals;
    private long _loopTransitions;
    private string? _lastMissingAssetId;
    private bool _disposed;

    public AudioManager(IAudioVoiceFactory voiceFactory, AudioManagerOptions? options = null)
    {
        _voiceFactory = voiceFactory ?? throw new ArgumentNullException(nameof(voiceFactory));
        _options = options ?? new AudioManagerOptions();
        AudioManagerOptions.Validate(_options);
        _slots = new VoiceSlot[_options.MaximumVoices];
        for (var index = 0; index < _slots.Length; index++)
        {
            _slots[index] = new VoiceSlot();
        }
    }

    public AudioMixerSnapshot MixerSnapshot => _mixer.Capture();

    public void ApplyMixerCommand(AudioMixerCommand command)
    {
        ThrowIfDisposed();
        _mixer.Apply(command);
    }

    public void SetBusGains(float master, AudioBusGains buses)
    {
        ApplyMixerCommand(AudioMixerCommand.SetMaster(master));
        ApplyMixerCommand(AudioMixerCommand.SetBus(AudioBus.Music, buses.Music));
        ApplyMixerCommand(AudioMixerCommand.SetBus(AudioBus.Ambient, buses.Ambient));
        ApplyMixerCommand(AudioMixerCommand.SetBus(AudioBus.Sfx, buses.Sfx));
        ApplyMixerCommand(AudioMixerCommand.SetBus(AudioBus.Ui, buses.Ui));
    }

    public AudioPlayStatus PlayOneShot(in AudioPlayRequest request)
    {
        ThrowIfDisposed();
        if (!IsValid(request))
        {
            _invalidRequests++;
            return AudioPlayStatus.InvalidRequest;
        }

        if (request.CooldownSeconds > 0f &&
            _lastPlaybackById.TryGetValue(request.AudioId, out var lastPlayback) &&
            _clockSeconds - lastPlayback < request.CooldownSeconds)
        {
            _cooldownRejections++;
            return AudioPlayStatus.Cooldown;
        }

        var status = TryStartVoice(
            request.AudioId,
            request.Bus,
            request.Volume,
            request.Pitch,
            request.Priority,
            isLooped: false,
            loopChannel: null,
            request.IsSpatial,
            request.SourcePosition,
            request.MaximumDistance,
            initialGain: request.Volume,
            out _);
        if (status == AudioPlayStatus.Started)
        {
            _lastPlaybackById[request.AudioId] = _clockSeconds;
        }

        return status;
    }

    public AudioPlayStatus TransitionLoop(in AudioLoopRequest request)
    {
        ThrowIfDisposed();
        if (!IsValid(request))
        {
            _invalidRequests++;
            return AudioPlayStatus.InvalidRequest;
        }

        for (var index = 0; index < _slots.Length; index++)
        {
            var slot = _slots[index];
            if (slot.Active &&
                slot.LoopChannel == request.Channel &&
                string.Equals(slot.AudioId, request.AudioId, StringComparison.OrdinalIgnoreCase) &&
                !slot.StopWhenSilent)
            {
                slot.BeginFade(request.Volume, request.CrossfadeSeconds);
                return AudioPlayStatus.Started;
            }
        }

        var status = TryStartVoice(
            request.AudioId,
            request.Bus,
            request.Volume,
            pitch: 0f,
            request.Priority,
            isLooped: true,
            request.Channel,
            isSpatial: false,
            sourcePosition: default,
            maximumDistance: 1f,
            initialGain: request.CrossfadeSeconds > 0f ? 0f : request.Volume,
            out var incoming);
        if (status != AudioPlayStatus.Started || incoming is null)
        {
            return status;
        }

        for (var index = 0; index < _slots.Length; index++)
        {
            var slot = _slots[index];
            if (!ReferenceEquals(slot, incoming) && slot.Active && slot.LoopChannel == request.Channel)
            {
                slot.StopWhenSilent = true;
                slot.BeginFade(0f, request.CrossfadeSeconds);
            }
        }

        incoming.BeginFade(request.Volume, request.CrossfadeSeconds);
        _loopTransitions++;
        return AudioPlayStatus.Started;
    }

    public void StopLoop(AudioLoopChannel channel, float fadeSeconds)
    {
        ThrowIfDisposed();
        if (!float.IsFinite(fadeSeconds) || fadeSeconds < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(fadeSeconds));
        }

        for (var index = 0; index < _slots.Length; index++)
        {
            var slot = _slots[index];
            if (slot.Active && slot.LoopChannel == channel)
            {
                slot.StopWhenSilent = true;
                slot.BeginFade(0f, fadeSeconds);
            }
        }
    }

    public void Update(float elapsedSeconds, in AudioListener listener)
    {
        ThrowIfDisposed();
        if (!float.IsFinite(elapsedSeconds) || elapsedSeconds < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
        }

        _clockSeconds += elapsedSeconds;
        _mixer.Update(elapsedSeconds);
        var mixer = _mixer.Capture();
        for (var index = 0; index < _slots.Length; index++)
        {
            var slot = _slots[index];
            if (!slot.Active)
            {
                continue;
            }

            if (slot.Voice is null || slot.Voice.IsStopped)
            {
                Release(slot, completed: true);
                continue;
            }

            slot.UpdateFade(elapsedSeconds);
            var spatialGain = 1f;
            var pan = 0f;
            if (slot.IsSpatial)
            {
                var delta = slot.SourcePosition - listener.Position;
                var distance = delta.Length();
                var normalized = Math.Clamp(distance / slot.MaximumDistance, 0f, 1f);
                spatialGain = (1f - normalized) * (1f - normalized);
                pan = Math.Clamp(delta.X / slot.MaximumDistance, -1f, 1f);
            }

            try
            {
                slot.Voice.Volume = Math.Clamp(
                    slot.CurrentGain * spatialGain * mixer.GetEffectiveGain(slot.Bus),
                    0f,
                    1f);
                slot.Voice.Pan = pan;
            }
            catch (Exception exception) when (IsRecoverablePlaybackFailure(exception))
            {
                _playbackFailures++;
                Release(slot, completed: false);
                continue;
            }

            if (slot.StopWhenSilent && slot.CurrentGain <= _options.MinimumAudibleGain && slot.FadeComplete)
            {
                Release(slot, completed: true);
            }
        }
    }

    public AudioManagerTelemetry CaptureTelemetry()
    {
        var music = 0;
        var ambient = 0;
        var sfx = 0;
        var ui = 0;
        for (var index = 0; index < _slots.Length; index++)
        {
            if (!_slots[index].Active)
            {
                continue;
            }

            switch (_slots[index].Bus)
            {
                case AudioBus.Music:
                    music++;
                    break;
                case AudioBus.Ambient:
                    ambient++;
                    break;
                case AudioBus.Sfx:
                    sfx++;
                    break;
                case AudioBus.Ui:
                    ui++;
                    break;
                default:
                    throw new InvalidDataException("Unknown audio bus in active voice slot.");
            }
        }

        return new AudioManagerTelemetry(
            music + ambient + sfx + ui,
            music,
            ambient,
            sfx,
            ui,
            _started,
            _completed,
            _missing,
            _cooldownRejections,
            _budgetRejections,
            _invalidRequests,
            _playbackFailures,
            _voiceSteals,
            _loopTransitions,
            _lastMissingAssetId,
            _mixer.Capture());
    }

    public void StopAll()
    {
        for (var index = 0; index < _slots.Length; index++)
        {
            if (_slots[index].Active)
            {
                Release(_slots[index], completed: false);
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        StopAll();
        _disposed = true;
    }

    private AudioPlayStatus TryStartVoice(
        string audioId,
        AudioBus bus,
        float volume,
        float pitch,
        int priority,
        bool isLooped,
        AudioLoopChannel? loopChannel,
        bool isSpatial,
        Vector2 sourcePosition,
        float maximumDistance,
        float initialGain,
        out VoiceSlot? startedSlot)
    {
        startedSlot = null;
        IAudioVoice? voice;
        try
        {
            if (!_voiceFactory.TryCreateVoice(audioId, out voice) || voice is null)
            {
                _missing++;
                _lastMissingAssetId = audioId;
                return AudioPlayStatus.MissingAsset;
            }
        }
        catch (Exception exception) when (IsRecoverablePlaybackFailure(exception))
        {
            _playbackFailures++;
            return AudioPlayStatus.PlaybackFailure;
        }

        var slot = FindSlot(bus, priority);
        if (slot is null)
        {
            voice.Dispose();
            _budgetRejections++;
            return AudioPlayStatus.VoiceBudget;
        }

        if (slot.Active)
        {
            Release(slot, completed: false);
            _voiceSteals++;
        }

        try
        {
            voice.IsLooped = isLooped;
            voice.Pitch = pitch;
            voice.Pan = 0f;
            voice.Volume = 0f;
            voice.Play();
        }
        catch (Exception exception) when (IsRecoverablePlaybackFailure(exception))
        {
            voice.Dispose();
            _playbackFailures++;
            return AudioPlayStatus.PlaybackFailure;
        }

        slot.Activate(
            voice,
            audioId,
            bus,
            priority,
            ++_sequence,
            isLooped,
            loopChannel,
            isSpatial,
            sourcePosition,
            maximumDistance,
            initialGain,
            volume);
        _started++;
        startedSlot = slot;
        return AudioPlayStatus.Started;
    }

    private VoiceSlot? FindSlot(AudioBus bus, int priority)
    {
        VoiceSlot? inactive = null;
        VoiceSlot? candidate = null;
        var busCount = 0;
        for (var index = 0; index < _slots.Length; index++)
        {
            var slot = _slots[index];
            if (!slot.Active)
            {
                inactive ??= slot;
                continue;
            }

            if (slot.Bus == bus)
            {
                busCount++;
            }
        }

        var busAtCapacity = busCount >= GetBusLimit(bus);
        if (!busAtCapacity && inactive is not null)
        {
            return inactive;
        }

        for (var index = 0; index < _slots.Length; index++)
        {
            var slot = _slots[index];
            if (!slot.Active || busAtCapacity && slot.Bus != bus || slot.Priority > priority)
            {
                continue;
            }

            if (candidate is null ||
                slot.Priority < candidate.Priority ||
                slot.Priority == candidate.Priority && slot.Sequence < candidate.Sequence)
            {
                candidate = slot;
            }
        }

        return candidate;
    }

    private int GetBusLimit(AudioBus bus)
    {
        return bus switch
        {
            AudioBus.Music => _options.MaximumMusicVoices,
            AudioBus.Ambient => _options.MaximumAmbientVoices,
            AudioBus.Sfx => _options.MaximumSfxVoices,
            AudioBus.Ui => _options.MaximumUiVoices,
            _ => throw new ArgumentOutOfRangeException(nameof(bus))
        };
    }

    private void Release(VoiceSlot slot, bool completed)
    {
        if (!slot.Active)
        {
            return;
        }

        try
        {
            slot.Voice?.Stop();
        }
        catch (Exception exception) when (IsRecoverablePlaybackFailure(exception))
        {
            _playbackFailures++;
        }
        finally
        {
            slot.Voice?.Dispose();
            slot.Reset();
        }

        if (completed)
        {
            _completed++;
        }
    }

    private static bool IsValid(in AudioPlayRequest request)
    {
        return !string.IsNullOrWhiteSpace(request.AudioId) &&
            Enum.IsDefined(request.Bus) &&
            float.IsFinite(request.Volume) && request.Volume is >= 0f and <= 1f &&
            float.IsFinite(request.Pitch) && request.Pitch is >= -1f and <= 1f &&
            request.Priority is >= 0 and <= 100 &&
            float.IsFinite(request.CooldownSeconds) && request.CooldownSeconds >= 0f &&
            (!request.IsSpatial ||
                float.IsFinite(request.SourcePosition.X) &&
                float.IsFinite(request.SourcePosition.Y) &&
                float.IsFinite(request.MaximumDistance) &&
                request.MaximumDistance > 0f);
    }

    private static bool IsValid(in AudioLoopRequest request)
    {
        return !string.IsNullOrWhiteSpace(request.AudioId) &&
            Enum.IsDefined(request.Channel) &&
            Enum.IsDefined(request.Bus) &&
            float.IsFinite(request.Volume) && request.Volume is >= 0f and <= 1f &&
            request.Priority is >= 0 and <= 100 &&
            float.IsFinite(request.CrossfadeSeconds) && request.CrossfadeSeconds >= 0f;
    }

    private static bool IsRecoverablePlaybackFailure(Exception exception)
    {
        return exception is NoAudioHardwareException or InstancePlayLimitException or
            InvalidOperationException or ObjectDisposedException or NotSupportedException;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private sealed class VoiceSlot
    {
        public bool Active { get; private set; }

        public IAudioVoice? Voice { get; private set; }

        public string? AudioId { get; private set; }

        public AudioBus Bus { get; private set; }

        public int Priority { get; private set; }

        public long Sequence { get; private set; }

        public bool IsLooped { get; private set; }

        public AudioLoopChannel? LoopChannel { get; private set; }

        public bool IsSpatial { get; private set; }

        public Vector2 SourcePosition { get; private set; }

        public float MaximumDistance { get; private set; }

        public float CurrentGain { get; private set; }

        public bool StopWhenSilent { get; set; }

        public bool FadeComplete => _fadeElapsed >= _fadeDuration;

        private float _fadeStart;
        private float _fadeTarget;
        private float _fadeElapsed;
        private float _fadeDuration;

        public void Activate(
            IAudioVoice voice,
            string audioId,
            AudioBus bus,
            int priority,
            long sequence,
            bool isLooped,
            AudioLoopChannel? loopChannel,
            bool isSpatial,
            Vector2 sourcePosition,
            float maximumDistance,
            float initialGain,
            float targetGain)
        {
            Active = true;
            Voice = voice;
            AudioId = audioId;
            Bus = bus;
            Priority = priority;
            Sequence = sequence;
            IsLooped = isLooped;
            LoopChannel = loopChannel;
            IsSpatial = isSpatial;
            SourcePosition = sourcePosition;
            MaximumDistance = maximumDistance;
            CurrentGain = initialGain;
            StopWhenSilent = false;
            _fadeStart = initialGain;
            _fadeTarget = targetGain;
            _fadeElapsed = 0f;
            _fadeDuration = 0f;
        }

        public void BeginFade(float targetGain, float durationSeconds)
        {
            _fadeStart = CurrentGain;
            _fadeTarget = targetGain;
            _fadeElapsed = 0f;
            _fadeDuration = durationSeconds;
            if (durationSeconds == 0f)
            {
                CurrentGain = targetGain;
            }
        }

        public void UpdateFade(float elapsedSeconds)
        {
            if (_fadeDuration <= 0f || _fadeElapsed >= _fadeDuration)
            {
                return;
            }

            _fadeElapsed = MathF.Min(_fadeDuration, _fadeElapsed + elapsedSeconds);
            var progress = _fadeElapsed / _fadeDuration;
            CurrentGain = _fadeStart + (_fadeTarget - _fadeStart) * progress;
        }

        public void Reset()
        {
            Active = false;
            Voice = null;
            AudioId = null;
            Priority = 0;
            Sequence = 0;
            IsLooped = false;
            LoopChannel = null;
            IsSpatial = false;
            SourcePosition = default;
            MaximumDistance = 0f;
            CurrentGain = 0f;
            StopWhenSilent = false;
            _fadeStart = 0f;
            _fadeTarget = 0f;
            _fadeElapsed = 0f;
            _fadeDuration = 0f;
        }
    }
}
