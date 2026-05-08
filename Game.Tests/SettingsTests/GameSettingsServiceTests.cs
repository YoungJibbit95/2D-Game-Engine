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
            Audio = new AudioSettings
            {
                MasterVolume = 0.75f,
                MusicVolume = 0.5f,
                SfxVolume = 0.25f,
                UiVolume = 0.9f
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

    private static string CreateTempPath()
    {
        return Path.Combine(Path.GetTempPath(), "TerrariaLikeTests", Guid.NewGuid().ToString("N"), "settings.json");
    }
}
