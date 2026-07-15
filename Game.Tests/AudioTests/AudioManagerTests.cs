using Game.Client.Audio;
using Game.Core.Audio;
using Game.Core.Settings;
using System.Numerics;
using Xunit;

namespace Game.Tests.AudioTests;

public sealed class AudioManagerTests
{
    [Fact]
    public void MissingAsset_IsSilentAndVisibleInTelemetry()
    {
        using var manager = new AudioManager(new FakeVoiceFactory());

        var status = manager.PlayOneShot(new AudioPlayRequest("missing", AudioBus.Sfx));
        var telemetry = manager.CaptureTelemetry();

        Assert.Equal(AudioPlayStatus.MissingAsset, status);
        Assert.Equal(0, telemetry.ActiveVoices);
        Assert.Equal(1, telemetry.MissingAssets);
        Assert.Equal("missing", telemetry.LastMissingAssetId);
    }

    [Fact]
    public void CooldownAndBusBudget_AreEnforcedWithPriorityVoiceStealing()
    {
        var factory = new FakeVoiceFactory("low", "high", "cooldown");
        using var manager = new AudioManager(factory, CreateTwoVoiceOptions());

        Assert.Equal(
            AudioPlayStatus.Started,
            manager.PlayOneShot(new AudioPlayRequest("cooldown", AudioBus.Sfx, Priority: 10, CooldownSeconds: 1f)));
        Assert.Equal(
            AudioPlayStatus.Cooldown,
            manager.PlayOneShot(new AudioPlayRequest("cooldown", AudioBus.Sfx, Priority: 10, CooldownSeconds: 1f)));
        Assert.Equal(
            AudioPlayStatus.Started,
            manager.PlayOneShot(new AudioPlayRequest("low", AudioBus.Sfx, Priority: 5)));

        var lowVoice = factory.Last("low");
        Assert.Equal(
            AudioPlayStatus.Started,
            manager.PlayOneShot(new AudioPlayRequest("high", AudioBus.Sfx, Priority: 90)));

        Assert.True(lowVoice.StopCalled);
        Assert.Equal(1, manager.CaptureTelemetry().VoiceSteals);
        Assert.Equal(1, manager.CaptureTelemetry().CooldownRejections);
    }

    [Fact]
    public void SpatialVoice_UsesDistanceAttenuationPanAndMixerGain()
    {
        var factory = new FakeVoiceFactory("torch");
        using var manager = new AudioManager(factory);
        manager.SetBusGains(0.8f, new AudioBusGains(1f, 1f, 0.5f, 1f));
        _ = manager.PlayOneShot(new AudioPlayRequest(
            "torch",
            AudioBus.Sfx,
            IsSpatial: true,
            SourcePosition: new Vector2(50f, 0f),
            MaximumDistance: 100f));

        manager.Update(0.016f, new AudioListener(Vector2.Zero));
        var voice = factory.Last("torch");

        Assert.Equal(0.1f, voice.Volume, 3);
        Assert.Equal(0.5f, voice.Pan, 3);
    }

    [Fact]
    public void LoopTransition_CrossfadesAndRetiresOutgoingVoice()
    {
        var factory = new FakeVoiceFactory("day", "night");
        using var manager = new AudioManager(factory);
        _ = manager.TransitionLoop(new AudioLoopRequest(
            "day",
            AudioLoopChannel.Music,
            AudioBus.Music,
            CrossfadeSeconds: 0f));
        manager.Update(0f, new AudioListener(Vector2.Zero));

        _ = manager.TransitionLoop(new AudioLoopRequest(
            "night",
            AudioLoopChannel.Music,
            AudioBus.Music,
            CrossfadeSeconds: 1f));
        manager.Update(0.5f, new AudioListener(Vector2.Zero));

        var day = factory.Last("day");
        var night = factory.Last("night");
        Assert.Equal(0.5f, day.Volume, 3);
        Assert.Equal(0.5f, night.Volume, 3);
        Assert.False(day.StopCalled);

        manager.Update(0.6f, new AudioListener(Vector2.Zero));
        Assert.True(day.StopCalled);
        Assert.False(night.StopCalled);
        Assert.Equal(2, manager.CaptureTelemetry().LoopTransitions);
    }

    [Fact]
    public void HigherPriorityCannotBeDisplacedByLowerPriorityRequest()
    {
        var factory = new FakeVoiceFactory("first", "second");
        using var manager = new AudioManager(factory, new AudioManagerOptions
        {
            MaximumVoices = 1,
            MaximumMusicVoices = 1,
            MaximumAmbientVoices = 1,
            MaximumSfxVoices = 1,
            MaximumUiVoices = 1
        });
        _ = manager.PlayOneShot(new AudioPlayRequest("first", AudioBus.Sfx, Priority: 100));

        var status = manager.PlayOneShot(new AudioPlayRequest("second", AudioBus.Sfx, Priority: 10));

        Assert.Equal(AudioPlayStatus.VoiceBudget, status);
        Assert.False(factory.Last("first").StopCalled);
        Assert.Equal(1, manager.CaptureTelemetry().BudgetRejections);
    }

    [Fact]
    public void ActiveVoiceUpdate_DoesNotAllocateInSteadyState()
    {
        var factory = new FakeVoiceFactory("loop");
        using var manager = new AudioManager(factory);
        _ = manager.TransitionLoop(new AudioLoopRequest(
            "loop",
            AudioLoopChannel.AmbientPrimary,
            AudioBus.Ambient,
            CrossfadeSeconds: 0f));
        var listener = new AudioListener(Vector2.Zero);
        manager.Update(0.016f, listener);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var iteration = 0; iteration < 1_000; iteration++)
        {
            manager.Update(0.016f, listener);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Fact]
    public void SoundscapeController_ProducesDesiredStateWhenRegistryIsEmpty()
    {
        var catalog = new SoundscapeCatalog();
        catalog.Register(new SoundscapeDefinition
        {
            Id = "forest",
            DayMusicLoopId = "music.forest.day",
            SurfaceAmbientLoopId = "ambient.forest",
            RainLoopId = "ambient.rain"
        });
        using var manager = new AudioManager(new FakeVoiceFactory());
        var controller = new SoundscapeController(
            new SoundscapeCommandPlanner(new SoundscapeResolver(catalog)),
            manager);

        controller.Update(
            SoundscapePlannerTests.CreateLiving(),
            SoundscapePlannerTests.CreateTime(false),
            0.016f,
            Vector2.Zero);
        var telemetry = controller.CaptureTelemetry();

        Assert.Equal("music.forest.day", telemetry.DesiredMusicLoopId);
        Assert.Equal("ambient.forest", telemetry.DesiredAmbientLoopId);
        Assert.Equal("ambient.rain", telemetry.DesiredWeatherLoopId);
        Assert.Equal(3, telemetry.Audio.MissingAssets);
        Assert.Equal(0, telemetry.Audio.ActiveVoices);
    }

    [Fact]
    public void SettingsAdapter_MapsExistingCategoriesIncludingAmbientFallback()
    {
        using var manager = new AudioManager(new FakeVoiceFactory());

        AudioSettingsAdapter.Apply(manager, new AudioSettings
        {
            MasterVolume = 0.5f,
            MusicVolume = 0.4f,
            SfxVolume = 0.6f,
            UiVolume = 0.8f
        });
        var mixer = manager.MixerSnapshot;

        Assert.Equal(0.2f, mixer.EffectiveGains.Music, 3);
        Assert.Equal(0.3f, mixer.EffectiveGains.Ambient, 3);
        Assert.Equal(0.3f, mixer.EffectiveGains.Sfx, 3);
        Assert.Equal(0.4f, mixer.EffectiveGains.Ui, 3);
    }

    private static AudioManagerOptions CreateTwoVoiceOptions()
    {
        return new AudioManagerOptions
        {
            MaximumVoices = 2,
            MaximumMusicVoices = 1,
            MaximumAmbientVoices = 2,
            MaximumSfxVoices = 2,
            MaximumUiVoices = 1
        };
    }

    private sealed class FakeVoiceFactory : IAudioVoiceFactory
    {
        private readonly HashSet<string> _known;
        private readonly Dictionary<string, List<FakeVoice>> _created = new(StringComparer.OrdinalIgnoreCase);

        public FakeVoiceFactory(params string[] known)
        {
            _known = new HashSet<string>(known, StringComparer.OrdinalIgnoreCase);
        }

        public bool TryCreateVoice(string audioId, out IAudioVoice? voice)
        {
            if (!_known.Contains(audioId))
            {
                voice = null;
                return false;
            }

            var fake = new FakeVoice();
            if (!_created.TryGetValue(audioId, out var voices))
            {
                voices = [];
                _created.Add(audioId, voices);
            }

            voices.Add(fake);
            voice = fake;
            return true;
        }

        public FakeVoice Last(string audioId)
        {
            return _created[audioId][^1];
        }
    }

    private sealed class FakeVoice : IAudioVoice
    {
        public bool IsStopped { get; set; }

        public bool IsLooped { get; set; }

        public float Volume { get; set; }

        public float Pan { get; set; }

        public float Pitch { get; set; }

        public bool StopCalled { get; private set; }

        public void Play()
        {
            IsStopped = false;
        }

        public void Stop()
        {
            StopCalled = true;
            IsStopped = true;
        }

        public void Dispose()
        {
        }
    }
}
