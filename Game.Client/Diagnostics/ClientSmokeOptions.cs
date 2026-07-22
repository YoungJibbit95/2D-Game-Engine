using Game.Client.Rendering;
using Game.Client.Rendering.Diagnostics;
using Game.Client.Rendering.Performance;
using Game.Core.Diagnostics;
using Game.Core.Weather;
using System.Text.Json;
using System.Globalization;

namespace Game.Client.Diagnostics;

public sealed record ClientSmokeOptions(
    int Frames,
    string? ScreenshotPath,
    int TimeoutSeconds,
    ClientSmokeStartState StartState = ClientSmokeStartState.MainMenu,
    bool OpenConsole = false,
    string? ForcedBiomeId = null,
    int Width = 640,
    int Height = 360,
    int WarmupFrames = 0,
    int FrameRateLimit = 0,
    bool OpenPause = false,
    bool UseConfiguredVideoSettings = false,
    bool IncludeDebugOverlays = false,
    bool ScriptedTraversal = false,
    double? ForcedTimeOfDay = null,
    WeatherKind? ForcedWeather = null,
    float ForcedWeatherIntensity = 0.75f,
    bool OpenCrafting = false)
{
    public const int DefaultTimeoutSeconds = 20;

    public static ClientSmokeOptions? Parse(string[] args)
    {
        if (!args.Contains("--smoke", StringComparer.OrdinalIgnoreCase))
        {
            return null;
        }

        var frames = 3;
        var timeoutSeconds = DefaultTimeoutSeconds;
        string? output = null;
        var startState = ClientSmokeStartState.MainMenu;
        var openConsole = false;
        string? forcedBiomeId = null;
        var width = 640;
        var height = 360;
        var warmupFrames = 0;
        var frameRateLimit = 0;
        var openPause = false;
        var openCrafting = false;
        var useConfiguredVideoSettings = false;
        var includeDebugOverlays = false;
        var scriptedTraversal = false;
        double? forcedTimeOfDay = null;
        WeatherKind? forcedWeather = null;
        var forcedWeatherIntensity = 0.75f;
        var forcedWeatherIntensitySpecified = false;
        for (var index = 0; index < args.Length; index++)
        {
            if (string.Equals(args[index], "--frames", StringComparison.OrdinalIgnoreCase))
            {
                var value = RequireValue(args, ref index, "--frames");
                if (!int.TryParse(value, out frames) || frames is < 1 or > 3600)
                {
                    throw new ArgumentException("--frames must be an integer from 1 through 3600.");
                }
            }
            else if (string.Equals(args[index], "--warmup-frames", StringComparison.OrdinalIgnoreCase))
            {
                var value = RequireValue(args, ref index, "--warmup-frames");
                if (!int.TryParse(value, out warmupFrames) || warmupFrames is < 0 or > 1800)
                {
                    throw new ArgumentException("--warmup-frames must be an integer from 0 through 1800.");
                }
            }
            else if (string.Equals(args[index], "--resolution", StringComparison.OrdinalIgnoreCase))
            {
                var value = RequireValue(args, ref index, "--resolution");
                var separator = value.IndexOf('x');
                if (separator < 0)
                {
                    separator = value.IndexOf('X');
                }
                if (separator <= 0 ||
                    !int.TryParse(value.AsSpan(0, separator), out width) ||
                    !int.TryParse(value.AsSpan(separator + 1), out height) ||
                    width is < 640 or > 7680 ||
                    height is < 360 or > 4320)
                {
                    throw new ArgumentException("--resolution must be WIDTHxHEIGHT within 640x360 and 7680x4320.");
                }
            }
            else if (string.Equals(args[index], "--frame-limit", StringComparison.OrdinalIgnoreCase))
            {
                var value = RequireValue(args, ref index, "--frame-limit");
                if (!int.TryParse(value, out frameRateLimit) ||
                    frameRateLimit != 0 && frameRateLimit is < 30 or > 360)
                {
                    throw new ArgumentException("--frame-limit must be 0 or an integer from 30 through 360.");
                }
            }
            else if (string.Equals(args[index], "--output", StringComparison.OrdinalIgnoreCase))
            {
                output = RequireValue(args, ref index, "--output");
            }
            else if (string.Equals(args[index], "--start-state", StringComparison.OrdinalIgnoreCase))
            {
                var value = RequireValue(args, ref index, "--start-state");
                startState = value.ToLowerInvariant() switch
                {
                    "main-menu" => ClientSmokeStartState.MainMenu,
                    "world-select" => ClientSmokeStartState.WorldSelect,
                    "playing" => ClientSmokeStartState.Playing,
                    _ => throw new ArgumentException("--start-state must be main-menu, world-select, or playing.")
                };
            }
            else if (string.Equals(args[index], "--timeout-seconds", StringComparison.OrdinalIgnoreCase))
            {
                var value = RequireValue(args, ref index, "--timeout-seconds");
                if (!int.TryParse(value, out timeoutSeconds) || timeoutSeconds is < 1 or > 300)
                {
                    throw new ArgumentException("--timeout-seconds must be an integer from 1 through 300.");
                }
            }
            else if (string.Equals(args[index], "--open-console", StringComparison.OrdinalIgnoreCase))
            {
                openConsole = true;
            }
            else if (string.Equals(args[index], "--open-pause", StringComparison.OrdinalIgnoreCase))
            {
                openPause = true;
            }
            else if (string.Equals(args[index], "--open-crafting", StringComparison.OrdinalIgnoreCase))
            {
                openCrafting = true;
            }
            else if (string.Equals(args[index], "--use-configured-video", StringComparison.OrdinalIgnoreCase))
            {
                useConfiguredVideoSettings = true;
            }
            else if (string.Equals(args[index], "--include-debug-overlays", StringComparison.OrdinalIgnoreCase))
            {
                includeDebugOverlays = true;
            }
            else if (string.Equals(args[index], "--scripted-traversal", StringComparison.OrdinalIgnoreCase))
            {
                scriptedTraversal = true;
            }
            else if (string.Equals(args[index], "--scene-biome", StringComparison.OrdinalIgnoreCase))
            {
                var value = RequireValue(args, ref index, "--scene-biome");
                forcedBiomeId = NormalizeContentId(value, "--scene-biome");
                forcedBiomeId = forcedBiomeId switch
                {
                    "mushroom" => "mushroom_cave",
                    "crystal" => "crystal_depths",
                    _ => forcedBiomeId
                };
            }
            else if (string.Equals(args[index], "--scene-time", StringComparison.OrdinalIgnoreCase))
            {
                var value = RequireValue(args, ref index, "--scene-time");
                forcedTimeOfDay = value.ToLowerInvariant() switch
                {
                    "midnight" or "night" => 0d,
                    "dawn" => 0.25d,
                    "day" or "noon" => 0.5d,
                    "dusk" => 0.75d,
                    _ when double.TryParse(
                        value,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out var normalizedTime) && normalizedTime is >= 0d and < 1d => normalizedTime,
                    _ => throw new ArgumentException(
                        "--scene-time must be dawn, day, noon, dusk, night, midnight, or a normalized value from 0 through less than 1.")
                };
            }
            else if (string.Equals(args[index], "--scene-weather", StringComparison.OrdinalIgnoreCase))
            {
                var value = RequireValue(args, ref index, "--scene-weather");
                forcedWeather = value.ToLowerInvariant() switch
                {
                    "clear" => WeatherKind.Clear,
                    "rain" => WeatherKind.Rain,
                    "storm" => WeatherKind.Storm,
                    "fog" => WeatherKind.Fog,
                    "snow" => WeatherKind.Snow,
                    "blizzard" => WeatherKind.Blizzard,
                    _ => throw new ArgumentException(
                        "--scene-weather must be clear, rain, storm, fog, snow, or blizzard.")
                };
            }
            else if (string.Equals(args[index], "--scene-weather-intensity", StringComparison.OrdinalIgnoreCase))
            {
                var value = RequireValue(args, ref index, "--scene-weather-intensity");
                if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out forcedWeatherIntensity) ||
                    !float.IsFinite(forcedWeatherIntensity) ||
                    forcedWeatherIntensity is < 0f or > 1f)
                {
                    throw new ArgumentException("--scene-weather-intensity must be a finite value from 0 through 1.");
                }

                forcedWeatherIntensitySpecified = true;
            }
        }

        if (forcedBiomeId is not null && startState != ClientSmokeStartState.Playing)
        {
            throw new ArgumentException("--scene-biome requires --start-state playing.");
        }

        if (forcedTimeOfDay is not null && startState != ClientSmokeStartState.Playing)
        {
            throw new ArgumentException("--scene-time requires --start-state playing.");
        }

        if (forcedWeather is not null && startState != ClientSmokeStartState.Playing)
        {
            throw new ArgumentException("--scene-weather requires --start-state playing.");
        }

        if (forcedWeatherIntensitySpecified && forcedWeather is null)
        {
            throw new ArgumentException("--scene-weather-intensity requires --scene-weather.");
        }

        if (openPause && startState != ClientSmokeStartState.Playing)
        {
            throw new ArgumentException("--open-pause requires --start-state playing.");
        }

        if (openCrafting && startState != ClientSmokeStartState.Playing)
        {
            throw new ArgumentException("--open-crafting requires --start-state playing.");
        }

        if (openCrafting && (openPause || openConsole))
        {
            throw new ArgumentException("--open-crafting cannot be combined with --open-pause or --open-console.");
        }

        if (warmupFrames >= frames)
        {
            throw new ArgumentException("--warmup-frames must be smaller than --frames.");
        }

        return new ClientSmokeOptions(
            frames,
            output,
            timeoutSeconds,
            startState,
            openConsole,
            forcedBiomeId,
            width,
            height,
            warmupFrames,
            frameRateLimit,
            openPause,
            useConfiguredVideoSettings,
            includeDebugOverlays,
            scriptedTraversal,
            forcedTimeOfDay,
            forcedWeather,
            forcedWeatherIntensity,
            openCrafting);
    }

    private static string NormalizeContentId(string value, string option)
    {
        var normalized = value.Trim().ToLowerInvariant().Replace('-', '_');
        if (normalized.Length is < 1 or > 64 ||
            normalized[0] == '_' ||
            normalized[^1] == '_' ||
            normalized.Any(character => character != '_' && !char.IsAsciiLetterOrDigit(character)))
        {
            throw new ArgumentException($"{option} must be a valid content ID.");
        }

        return normalized;
    }

    private static string RequireValue(string[] args, ref int index, string option)
    {
        if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
        {
            throw new ArgumentException($"{option} requires a value.");
        }

        var value = args[++index];
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{option} requires a non-empty value.");
        }

        return value;
    }
}

public enum ClientSmokeStartState
{
    MainMenu,
    WorldSelect,
    Playing
}

public sealed record ClientSmokeResult(
    bool Passed,
    int CapturedFrame,
    int Width,
    int Height,
    int NonBlackPixels,
    int DistinctColors,
    IReadOnlyList<string> RenderedSpriteIds,
    IReadOnlyList<string> ValidatedSourceSpriteIds,
    IReadOnlyList<string> VisibleTargetSpriteIds,
    int TextureResourceCount,
    long TextureFileLoadCount,
    int TextureFrameCount,
    int TexturePlaceholderResourceCount,
    int TextureInvalidResourceCount,
    double TotalTextureResourceLoadMilliseconds,
    long TotalTextureResourceLoadAllocatedBytes,
    long EstimatedDecodedTextureBytes,
    int ExpectedTextureFrameCount,
    int PlaceholderAssetCount,
    string? ProjectId,
    string? ProjectManifestPath,
    string? GraphicsAdapterDescription,
    string? ScreenshotPath,
    string? Failure)
{
    public IReadOnlyList<PerformanceMetricSnapshot> PerformanceMetrics { get; init; } =
        Array.Empty<PerformanceMetricSnapshot>();

    public FrameTimeTelemetrySnapshot FrameTiming { get; init; }

    public RendererMetricsTelemetrySnapshot RendererMetrics { get; init; } =
        RendererMetricsTelemetrySnapshot.NotCaptured;

    public ClientGameplaySmokeTelemetry Gameplay { get; init; } =
        ClientGameplaySmokeTelemetry.NotCaptured;

    public static ClientSmokeResult CaptureFailed(
        int frame,
        string? screenshotPath,
        Exception exception,
        string? projectId = null,
        string? projectManifestPath = null,
        TextureRegistryTelemetry textureTelemetry = default,
        int expectedTextureFrameCount = 0,
        int placeholderAssetCount = 0)
    {
        return new ClientSmokeResult(
            false,
            frame,
            0,
            0,
            0,
            0,
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            textureTelemetry.ResourceCount,
            textureTelemetry.FileLoadCount,
            textureTelemetry.FrameCount,
            textureTelemetry.PlaceholderResourceCount,
            textureTelemetry.InvalidResourceCount,
            textureTelemetry.TotalResourceLoadMilliseconds,
            textureTelemetry.TotalResourceLoadAllocatedBytes,
            textureTelemetry.EstimatedDecodedTextureBytes,
            expectedTextureFrameCount,
            placeholderAssetCount,
            projectId,
            projectManifestPath,
            null,
            screenshotPath,
            exception.Message);
    }

    public static ClientSmokeResult TimedOut(
        int frame,
        string? screenshotPath,
        int timeoutSeconds,
        string? projectId,
        string? projectManifestPath,
        TextureRegistryTelemetry textureTelemetry = default,
        int expectedTextureFrameCount = 0,
        int placeholderAssetCount = 0)
    {
        return CaptureFailed(
            frame,
            screenshotPath,
            new TimeoutException($"Client smoke exceeded its {timeoutSeconds}-second wall-clock deadline."),
            projectId,
            projectManifestPath,
            textureTelemetry,
            expectedTextureFrameCount,
            placeholderAssetCount);
    }

    public void WriteJsonForScreenshot()
    {
        if (string.IsNullOrWhiteSpace(ScreenshotPath))
        {
            return;
        }

        var reportPath = Path.ChangeExtension(Path.GetFullPath(ScreenshotPath), ".json");
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
        File.WriteAllText(
            reportPath,
            JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine);
    }
}
