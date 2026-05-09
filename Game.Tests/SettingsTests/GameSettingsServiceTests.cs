using Game.Core.Settings;
using Xunit;

namespace Game.Tests.SettingsTests;

public sealed class GameSettingsServiceTests
{
    [Fact]
    public void LoadOrCreate_CreatesDefaultSettingsFile()
    {
        var path = CreateTempPath();
        var service = new GameSettingsService();

        var settings = service.LoadOrCreate(path);

        Assert.True(File.Exists(path));
        Assert.Equal(1280, settings.Video.Width);
        Assert.True(settings.Video.VSync);
    }

    [Fact]
    public void SaveAndLoad_RoundTripsSettings()
    {
        var path = CreateTempPath();
        var service = new GameSettingsService();
        var settings = GameSettings.CreateDefault() with
        {
            Video = new VideoSettings
            {
                Width = 1920,
                Height = 1080,
                Fullscreen = true,
                VSync = false
            },
            Rendering = new RenderingSettings
            {
                DrawLiquids = false,
                DrawLightingOverlay = false,
                DrawDebugOverlays = true,
                PostProcessingEnabled = true,
                PixelSnap = true,
                BloomStrength = 0.25f,
                VignetteStrength = 0.15f,
                ColorGradeIntensity = 0.5f,
                ParticleQuality = 3
            },
            Audio = new AudioSettings
            {
                MasterVolume = 0.75f,
                MusicVolume = 0.5f,
                SfxVolume = 0.25f,
                UiVolume = 0.9f
            },
            Gameplay = new GameplaySettings
            {
                AutosaveMinutes = 3,
                MaxActiveEnemies = 64,
                CameraZoom = 2.5f,
                InteractionReachPixels = 128f,
                ShowInteractionTarget = false,
                HoldToMine = true,
                PauseOnFocusLost = true,
                EnemySpawnRateMultiplier = 1.25f
            },
            World = new WorldSettings
            {
                InfiniteHorizontalGeneration = true,
                WorldProfileId = "small",
                ChunkLoadMargin = 2,
                ChunkUnloadMargin = 5,
                KeepDirtyChunksLoaded = true,
                PreloadFullVerticalSlice = false
            },
            Input = new InputSettings
            {
                MouseSensitivity = 1.2f,
                InvertHotbarScroll = true,
                KeyBindings = new KeyBindingSettings
                {
                    MoveLeft = "J,Left",
                    MoveRight = "L,Right",
                    Jump = "I,Space",
                    Pause = "Escape"
                }
            },
            Debug = new DebugSettings
            {
                ShowDebugOverlay = false,
                ShowGrid = true
            }
        };

        service.Save(path, settings);
        var loaded = service.Load(path);

        Assert.Equal(settings, loaded);
    }

    [Fact]
    public void Save_RejectsInvalidSettings()
    {
        var path = CreateTempPath();
        var service = new GameSettingsService();
        var settings = GameSettings.CreateDefault() with
        {
            Video = new VideoSettings
            {
                Width = 320,
                Height = 200
            }
        };

        Assert.Throws<InvalidDataException>(() => service.Save(path, settings));
    }

    [Fact]
    public void Save_RejectsInvalidExtendedSettings()
    {
        var path = CreateTempPath();
        var service = new GameSettingsService();
        var settings = GameSettings.CreateDefault() with
        {
            Rendering = new RenderingSettings { ParticleQuality = 9 },
            Gameplay = new GameplaySettings { CameraZoom = 12 },
            World = new WorldSettings { WorldProfileId = "", ChunkLoadMargin = 5, ChunkUnloadMargin = 2 },
            Input = new InputSettings { MouseSensitivity = 0 }
        };

        Assert.Throws<InvalidDataException>(() => service.Save(path, settings));
    }

    private static string CreateTempPath()
    {
        return Path.Combine(Path.GetTempPath(), "TerrariaLikeTests", Guid.NewGuid().ToString("N"), "settings.json");
    }
}
