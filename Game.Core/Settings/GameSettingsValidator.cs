namespace Game.Core.Settings;

public sealed class GameSettingsValidator
{
    public SettingsValidationResult Validate(GameSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var result = new SettingsValidationResult();
        ValidateVideo(settings.Video, result);
        ValidateAudio(settings.Audio, result);
        ValidateGameplay(settings.Gameplay, result);
        return result;
    }

    private static void ValidateVideo(VideoSettings video, SettingsValidationResult result)
    {
        if (video.Width < 640 || video.Width > 7680)
        {
            result.Add("video.width", "Width must be between 640 and 7680.");
        }

        if (video.Height < 360 || video.Height > 4320)
        {
            result.Add("video.height", "Height must be between 360 and 4320.");
        }
    }

    private static void ValidateAudio(AudioSettings audio, SettingsValidationResult result)
    {
        ValidateVolume(audio.MasterVolume, "audio.masterVolume", result);
        ValidateVolume(audio.MusicVolume, "audio.musicVolume", result);
        ValidateVolume(audio.SfxVolume, "audio.sfxVolume", result);
        ValidateVolume(audio.UiVolume, "audio.uiVolume", result);
    }

    private static void ValidateGameplay(GameplaySettings gameplay, SettingsValidationResult result)
    {
        if (gameplay.AutosaveMinutes < 0.5f || gameplay.AutosaveMinutes > 60f)
        {
            result.Add("gameplay.autosaveMinutes", "Autosave interval must be between 0.5 and 60 minutes.");
        }

        if (gameplay.MaxActiveEnemies < 0 || gameplay.MaxActiveEnemies > 512)
        {
            result.Add("gameplay.maxActiveEnemies", "Max active enemies must be between 0 and 512.");
        }
    }

    private static void ValidateVolume(float value, string path, SettingsValidationResult result)
    {
        if (value < 0 || value > 1)
        {
            result.Add(path, "Volume must be in the range 0..1.");
        }
    }
}
