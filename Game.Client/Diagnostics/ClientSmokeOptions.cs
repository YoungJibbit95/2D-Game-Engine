using Game.Client.Rendering;
using System.Text.Json;

namespace Game.Client.Diagnostics;

public sealed record ClientSmokeOptions(
    int Frames,
    string? ScreenshotPath,
    int TimeoutSeconds,
    ClientSmokeStartState StartState = ClientSmokeStartState.MainMenu,
    bool OpenConsole = false,
    string? ForcedBiomeId = null)
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
        for (var index = 0; index < args.Length; index++)
        {
            if (string.Equals(args[index], "--frames", StringComparison.OrdinalIgnoreCase))
            {
                var value = RequireValue(args, ref index, "--frames");
                if (!int.TryParse(value, out frames) || frames is < 1 or > 120)
                {
                    throw new ArgumentException("--frames must be an integer from 1 through 120.");
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
        }

        if (forcedBiomeId is not null && startState != ClientSmokeStartState.Playing)
        {
            throw new ArgumentException("--scene-biome requires --start-state playing.");
        }

        return new ClientSmokeOptions(frames, output, timeoutSeconds, startState, openConsole, forcedBiomeId);
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
