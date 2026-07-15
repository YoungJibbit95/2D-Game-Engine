namespace Game.Core.Audio;

public enum AudioBus
{
    Music,
    Ambient,
    Sfx,
    Ui
}

public enum AudioMixerCommandKind
{
    SetMasterGain,
    SetBusGain,
    DuckBus,
    ReleaseBus
}

public readonly record struct AudioBusGains(float Music, float Ambient, float Sfx, float Ui)
{
    public static AudioBusGains Full => new(1f, 1f, 1f, 1f);

    public float Get(AudioBus bus)
    {
        return bus switch
        {
            AudioBus.Music => Music,
            AudioBus.Ambient => Ambient,
            AudioBus.Sfx => Sfx,
            AudioBus.Ui => Ui,
            _ => throw new ArgumentOutOfRangeException(nameof(bus))
        };
    }

    public AudioBusGains With(AudioBus bus, float gain)
    {
        return bus switch
        {
            AudioBus.Music => this with { Music = gain },
            AudioBus.Ambient => this with { Ambient = gain },
            AudioBus.Sfx => this with { Sfx = gain },
            AudioBus.Ui => this with { Ui = gain },
            _ => throw new ArgumentOutOfRangeException(nameof(bus))
        };
    }

    public static void Validate(AudioBusGains gains, string parameterName)
    {
        ValidateGain(gains.Music, parameterName);
        ValidateGain(gains.Ambient, parameterName);
        ValidateGain(gains.Sfx, parameterName);
        ValidateGain(gains.Ui, parameterName);
    }

    internal static void ValidateGain(float gain, string parameterName)
    {
        if (!float.IsFinite(gain) || gain is < 0f or > 1f)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Audio gains must be finite and between zero and one.");
        }
    }
}

public readonly record struct AudioMixerCommand(
    AudioMixerCommandKind Kind,
    AudioBus Bus,
    float Value,
    float DurationSeconds)
{
    public static AudioMixerCommand SetMaster(float gain)
    {
        return new AudioMixerCommand(AudioMixerCommandKind.SetMasterGain, AudioBus.Music, gain, 0f);
    }

    public static AudioMixerCommand SetBus(AudioBus bus, float gain)
    {
        return new AudioMixerCommand(AudioMixerCommandKind.SetBusGain, bus, gain, 0f);
    }

    public static AudioMixerCommand Duck(AudioBus bus, float gain, float attackSeconds)
    {
        return new AudioMixerCommand(AudioMixerCommandKind.DuckBus, bus, gain, attackSeconds);
    }

    public static AudioMixerCommand Release(AudioBus bus, float releaseSeconds)
    {
        return new AudioMixerCommand(AudioMixerCommandKind.ReleaseBus, bus, 1f, releaseSeconds);
    }

    public void Validate()
    {
        if (!Enum.IsDefined(Kind) || !Enum.IsDefined(Bus))
        {
            throw new InvalidDataException("Audio mixer command contains an unknown kind or bus.");
        }

        AudioBusGains.ValidateGain(Value, nameof(Value));
        if (!float.IsFinite(DurationSeconds) || DurationSeconds < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(DurationSeconds));
        }

        if (Kind == AudioMixerCommandKind.SetMasterGain && Bus != AudioBus.Music)
        {
            throw new InvalidDataException("Master gain commands use the canonical Music bus field.");
        }

        if (Kind is AudioMixerCommandKind.SetMasterGain or AudioMixerCommandKind.SetBusGain &&
            DurationSeconds != 0f)
        {
            throw new InvalidDataException("Direct gain commands cannot contain a transition duration.");
        }

        if (Kind == AudioMixerCommandKind.ReleaseBus && Value != 1f)
        {
            throw new InvalidDataException("Release commands always target a full ducking multiplier.");
        }
    }
}

public readonly record struct AudioMixerSnapshot(
    float MasterGain,
    AudioBusGains BusGains,
    AudioBusGains DuckingGains,
    AudioBusGains EffectiveGains)
{
    public float GetEffectiveGain(AudioBus bus)
    {
        return EffectiveGains.Get(bus);
    }

    public void Validate()
    {
        AudioBusGains.ValidateGain(MasterGain, nameof(MasterGain));
        AudioBusGains.Validate(BusGains, nameof(BusGains));
        AudioBusGains.Validate(DuckingGains, nameof(DuckingGains));
        AudioBusGains.Validate(EffectiveGains, nameof(EffectiveGains));
        foreach (var bus in Enum.GetValues<AudioBus>())
        {
            var expected = MasterGain * BusGains.Get(bus) * DuckingGains.Get(bus);
            if (MathF.Abs(expected - EffectiveGains.Get(bus)) > 0.0001f)
            {
                throw new InvalidDataException("Effective audio gain does not match the mixer inputs.");
            }
        }
    }
}

public sealed class AudioMixerModel
{
    private const int BusCount = 4;
    private readonly float[] _busGains = [1f, 1f, 1f, 1f];
    private readonly float[] _duckGains = [1f, 1f, 1f, 1f];
    private readonly float[] _duckStarts = [1f, 1f, 1f, 1f];
    private readonly float[] _duckTargets = [1f, 1f, 1f, 1f];
    private readonly float[] _duckElapsed = new float[BusCount];
    private readonly float[] _duckDurations = new float[BusCount];
    private float _masterGain = 1f;

    public void Apply(AudioMixerCommand command)
    {
        command.Validate();
        var index = (int)command.Bus;
        switch (command.Kind)
        {
            case AudioMixerCommandKind.SetMasterGain:
                _masterGain = command.Value;
                break;
            case AudioMixerCommandKind.SetBusGain:
                _busGains[index] = command.Value;
                break;
            case AudioMixerCommandKind.DuckBus:
            case AudioMixerCommandKind.ReleaseBus:
                _duckStarts[index] = _duckGains[index];
                _duckTargets[index] = command.Value;
                _duckElapsed[index] = 0f;
                _duckDurations[index] = command.DurationSeconds;
                if (command.DurationSeconds == 0f)
                {
                    _duckGains[index] = command.Value;
                }

                break;
            default:
                throw new InvalidDataException("Unsupported audio mixer command.");
        }
    }

    public void Update(float elapsedSeconds)
    {
        if (!float.IsFinite(elapsedSeconds) || elapsedSeconds < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
        }

        for (var index = 0; index < BusCount; index++)
        {
            var duration = _duckDurations[index];
            if (duration <= 0f || _duckGains[index] == _duckTargets[index])
            {
                continue;
            }

            _duckElapsed[index] = MathF.Min(duration, _duckElapsed[index] + elapsedSeconds);
            var progress = _duckElapsed[index] / duration;
            _duckGains[index] = Lerp(_duckStarts[index], _duckTargets[index], progress);
        }
    }

    public AudioMixerSnapshot Capture()
    {
        var buses = new AudioBusGains(_busGains[0], _busGains[1], _busGains[2], _busGains[3]);
        var ducks = new AudioBusGains(_duckGains[0], _duckGains[1], _duckGains[2], _duckGains[3]);
        var effective = new AudioBusGains(
            _masterGain * buses.Music * ducks.Music,
            _masterGain * buses.Ambient * ducks.Ambient,
            _masterGain * buses.Sfx * ducks.Sfx,
            _masterGain * buses.Ui * ducks.Ui);
        return new AudioMixerSnapshot(_masterGain, buses, ducks, effective);
    }

    private static float Lerp(float from, float to, float progress)
    {
        return from + (to - from) * progress;
    }
}
