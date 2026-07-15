using Game.Core.Audio;
using Game.Core.Settings;

namespace Game.Client.Audio;

public static class AudioSettingsAdapter
{
    public static void Apply(AudioManager manager, AudioSettings settings)
    {
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentNullException.ThrowIfNull(settings);
        manager.SetBusGains(
            settings.MasterVolume,
            new AudioBusGains(
                settings.MusicVolume,
                settings.SfxVolume,
                settings.SfxVolume,
                settings.UiVolume));
    }
}
