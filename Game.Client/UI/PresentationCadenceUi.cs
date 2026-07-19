using Game.Core.Settings;

namespace Game.Client.UI;

public static class PresentationCadenceUi
{
    public const string Eco = "ECO";
    public const string Balanced = "BALANCED";
    public const string Fast = "FAST";
    public const string Custom = "CUSTOM";

    public static IReadOnlyList<string> Presets { get; } = [Eco, Balanced, Fast];

    public static string ResolvePreset(RenderingSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return (settings.LightingUpdateRateHz, settings.ReflectionUpdateRateHz, settings.AtmosphereUpdateRateHz, settings.SceneCaptureUpdateRateHz) switch
        {
            (30, 15, 15, 20) => Eco,
            (45, 24, 24, 30) => Balanced,
            (120, 60, 60, 60) => Fast,
            _ => Custom
        };
    }

    public static RenderingSettings ApplyPreset(RenderingSettings settings, string preset)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return preset.ToUpperInvariant() switch
        {
            Eco => settings with
            {
                LightingUpdateRateHz = 30,
                ReflectionUpdateRateHz = 15,
                AtmosphereUpdateRateHz = 15,
                SceneCaptureUpdateRateHz = 20
            },
            Balanced => settings with
            {
                LightingUpdateRateHz = 45,
                ReflectionUpdateRateHz = 24,
                AtmosphereUpdateRateHz = 24,
                SceneCaptureUpdateRateHz = 30
            },
            Fast => settings with
            {
                LightingUpdateRateHz = 120,
                ReflectionUpdateRateHz = 60,
                AtmosphereUpdateRateHz = 60,
                SceneCaptureUpdateRateHz = 60
            },
            _ => settings
        };
    }
}
