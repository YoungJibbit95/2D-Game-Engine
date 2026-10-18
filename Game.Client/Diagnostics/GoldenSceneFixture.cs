using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Client.Configuration;
using Game.Core.Weather;

namespace Game.Client.Diagnostics;

public sealed record GoldenSceneFixture
{
    public int Version { get; init; } = 1;
    public required string SceneId { get; init; }
    public ClientSmokeStartState StartState { get; init; } = ClientSmokeStartState.Playing;
    public int Seed { get; init; } = 1337;
    public string BiomeId { get; init; } = "forest";
    public double TimeOfDay { get; init; } = 0.5;
    public WeatherKind Weather { get; init; } = WeatherKind.Clear;
    public bool DynamicLights { get; init; }
    public int Width { get; init; } = 960;
    public int Height { get; init; } = 540;
    public float Zoom { get; init; } = 2;
    public float? CameraX { get; init; }
    public float? CameraY { get; init; }
    public int Frames { get; init; } = 180;
    public int WarmupFrames { get; init; } = 60;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public LightingDiagnosticOptions? Diagnostic { get; init; }

    public static GoldenSceneFixture Load(string path)
    {
        var fixture = JsonSerializer.Deserialize<GoldenSceneFixture>(File.ReadAllText(path), new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            Converters = { new JsonStringEnumConverter() }
        }) ?? throw new InvalidDataException("Golden scene fixture is empty.");
        fixture.Validate();
        return fixture;
    }

    public void Validate()
    {
        if (Version != 1 || string.IsNullOrWhiteSpace(SceneId) || SceneId.Length > 64 ||
            SceneId.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '_') ||
            StartState is not (ClientSmokeStartState.Playing or ClientSmokeStartState.WorldSelect) ||
            !double.IsFinite(TimeOfDay) || TimeOfDay is < 0 or >= 1 || !Enum.IsDefined(Weather) ||
            Width < 640 || Width > (Diagnostic is null ? 1920 : 2560) ||
            Height < 360 || Height > (Diagnostic is null ? 1080 : 1440) ||
            !float.IsFinite(Zoom) || Zoom is < 1 or > 4 ||
            CameraX.HasValue != CameraY.HasValue ||
            (CameraX is { } x && !float.IsFinite(x)) || (CameraY is { } y && !float.IsFinite(y)) ||
            Frames < 120 || Frames > (Diagnostic is null ? 512 : 4096) || WarmupFrames < 1 || Frames - WarmupFrames < 60 ||
            (StartState == ClientSmokeStartState.Playing && BiomeId is not ("forest" or "crystal_depths")))
        {
            throw new InvalidDataException("Invalid version, scene identity, state, camera, clock or sample window in golden fixture.");
        }
        Diagnostic?.Validate(this);
    }

    public static void RequireEmptyUserRoot(string? root)
    {
        if (string.IsNullOrWhiteSpace(root) || !Path.IsPathFullyQualified(root) ||
            (Directory.Exists(root) && Directory.EnumerateFileSystemEntries(root).Any()))
        {
            throw new InvalidDataException($"Golden scenes require a fresh absolute {ClientPaths.UserDataRootEnvironmentVariable} directory.");
        }
    }

    internal static string ContentFingerprint(string root)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(path => Path.GetExtension(path) is ".json" or ".png")
            .OrderBy(path => Path.GetRelativePath(root, path).Replace('\\', '/'), StringComparer.Ordinal))
        {
            var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            if (relative.StartsWith("art_direction/", StringComparison.Ordinal) ||
                relative.StartsWith("asset_briefs/", StringComparison.Ordinal))
            {
                continue;
            }
            hash.AppendData(Encoding.UTF8.GetBytes(relative + "\n"));
            if (Path.GetExtension(file) == ".json")
            {
                // Git line endings and JSON whitespace are not content changes.
                using var document = JsonDocument.Parse(File.ReadAllBytes(file), new JsonDocumentOptions
                {
                    AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip
                });
                hash.AppendData(JsonSerializer.SerializeToUtf8Bytes(document.RootElement));
            }
            else
            {
                hash.AppendData(File.ReadAllBytes(file));
            }
        }
        return Convert.ToHexString(hash.GetHashAndReset());
    }
}
