using Game.Core.Audio;
using Xunit;

namespace Game.Tests.AudioTests;

public sealed class AudioMixerTests
{
    [Fact]
    public void Mixer_AppliesBusGainsAndDeterministicDuckEnvelope()
    {
        var mixer = new AudioMixerModel();
        mixer.Apply(AudioMixerCommand.SetMaster(0.8f));
        mixer.Apply(AudioMixerCommand.SetBus(AudioBus.Music, 0.5f));
        mixer.Apply(AudioMixerCommand.Duck(AudioBus.Music, 0.25f, 2f));

        mixer.Update(1f);
        var midpoint = mixer.Capture();

        Assert.Equal(0.625f, midpoint.DuckingGains.Music, 3);
        Assert.Equal(0.25f, midpoint.EffectiveGains.Music, 3);
        midpoint.Validate();

        mixer.Update(1f);
        Assert.Equal(0.1f, mixer.Capture().EffectiveGains.Music, 3);

        mixer.Apply(AudioMixerCommand.Release(AudioBus.Music, 0.5f));
        mixer.Update(0.25f);
        Assert.Equal(0.625f, mixer.Capture().DuckingGains.Music, 3);
    }

    [Fact]
    public void Mixer_RejectsInvalidCommandsAndInconsistentSnapshots()
    {
        var mixer = new AudioMixerModel();

        Assert.Throws<ArgumentOutOfRangeException>(() => mixer.Apply(AudioMixerCommand.SetMaster(float.NaN)));
        Assert.Throws<ArgumentOutOfRangeException>(() => mixer.Apply(AudioMixerCommand.Duck(AudioBus.Sfx, -1f, 0f)));
        Assert.Throws<ArgumentOutOfRangeException>(() => mixer.Update(-0.01f));

        var invalid = new AudioMixerSnapshot(1f, AudioBusGains.Full, AudioBusGains.Full, new AudioBusGains(0f, 1f, 1f, 1f));
        Assert.Throws<InvalidDataException>(invalid.Validate);
    }

    [Fact]
    public void CrossfadeEnvelope_UsesRepeatableEqualPowerGains()
    {
        var first = new DeterministicCrossfadeEnvelope();
        var second = new DeterministicCrossfadeEnvelope();
        first.Begin(2f);
        second.Begin(2f);

        var firstMidpoint = first.Advance(1f);
        var secondMidpoint = second.Advance(1f);

        Assert.Equal(firstMidpoint, secondMidpoint);
        Assert.Equal(0.5f, firstMidpoint.Progress, 3);
        Assert.Equal(MathF.Sqrt(0.5f), firstMidpoint.OutgoingGain, 3);
        Assert.Equal(MathF.Sqrt(0.5f), firstMidpoint.IncomingGain, 3);
        Assert.False(firstMidpoint.IsComplete);
        Assert.True(first.Advance(1f).IsComplete);
    }
}
