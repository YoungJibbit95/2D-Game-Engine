using Game.Core.Data;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Game.Core.Animations;

public sealed class SpriteAnimationJsonLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public SpriteAnimationRegistry LoadRegistryFromDirectory(string directoryPath)
    {
        return SpriteAnimationRegistry.Create(LoadClipsFromDirectory(directoryPath));
    }

    public IReadOnlyList<SpriteAnimationClip> LoadClipsFromDirectory(string directoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        if (!Directory.Exists(directoryPath))
        {
            return Array.Empty<SpriteAnimationClip>();
        }

        return Directory
            .EnumerateFiles(directoryPath, "*.json", SearchOption.AllDirectories)
            .Order(StringComparer.OrdinalIgnoreCase)
            .SelectMany(LoadClipsFromFile)
            .ToArray();
    }

    public IReadOnlyList<SpriteAnimationClip> LoadClipsFromFile(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        return LoadClipsFromJson(File.ReadAllText(filePath), filePath);
    }

    public IReadOnlyList<SpriteAnimationClip> LoadClipsFromJson(string json)
    {
        return LoadClipsFromJson(json, "inline json");
    }

    private static IReadOnlyList<SpriteAnimationClip> LoadClipsFromJson(string json, string source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var manifest = JsonSerializer.Deserialize<SpriteAnimationManifestDto>(json, Options);
        if (manifest?.Animations is { Count: > 0 })
        {
            return manifest.Animations.Select(animation => animation.ToClip()).ToArray();
        }

        var single = JsonSerializer.Deserialize<SpriteAnimationClipDto>(json, Options);
        if (single is null)
        {
            throw new JsonException($"Animation definition was empty: {source}");
        }

        return new[] { single.ToClip() };
    }

    private sealed record SpriteAnimationManifestDto
    {
        public List<SpriteAnimationClipDto> Animations { get; init; } = new();
    }

    private sealed record SpriteAnimationClipDto
    {
        public string? Id { get; init; }

        public string? DisplayName { get; init; }

        public SpriteAnimationLoopMode LoopMode { get; init; } = SpriteAnimationLoopMode.Loop;

        public string? SpriteId { get; init; }

        [JsonPropertyName("sprite")]
        public string? Sprite { get; init; }

        public int FrameStart { get; init; }

        public int FrameCount { get; init; }

        public float FrameDurationSeconds { get; init; } = 0.1f;

        [JsonPropertyName("frameDuration")]
        public float? FrameDuration { get; init; }

        public List<SpriteAnimationFrameDto> Frames { get; init; } = new();

        public string[] Tags { get; init; } = Array.Empty<string>();

        public SpriteAnimationClip ToClip()
        {
            return new SpriteAnimationClip
            {
                Id = Id ?? string.Empty,
                DisplayName = DisplayName ?? Id ?? string.Empty,
                LoopMode = LoopMode,
                Frames = ResolveFrames().ToArray(),
                Tags = DefinitionTags.Normalize(Tags)
            };
        }

        private IEnumerable<SpriteAnimationFrame> ResolveFrames()
        {
            if (Frames.Count > 0)
            {
                return Frames.Select(frame => frame.ToFrame(SpriteId ?? Sprite));
            }

            var spriteId = SpriteId ?? Sprite ?? string.Empty;
            var duration = FrameDuration ?? FrameDurationSeconds;
            return Enumerable.Range(0, FrameCount).Select(offset => new SpriteAnimationFrame
            {
                SpriteId = spriteId,
                FrameIndex = FrameStart + offset,
                DurationSeconds = duration
            });
        }
    }

    private sealed record SpriteAnimationFrameDto
    {
        public string? SpriteId { get; init; }

        [JsonPropertyName("sprite")]
        public string? Sprite { get; init; }

        public int FrameIndex { get; init; }

        [JsonPropertyName("frame")]
        public int? Frame { get; init; }

        public float DurationSeconds { get; init; } = 0.1f;

        public int? DurationMs { get; init; }

        public int OffsetX { get; init; }

        public int OffsetY { get; init; }

        public string? EventId { get; init; }

        public SpriteAnimationFrame ToFrame(string? defaultSpriteId)
        {
            return new SpriteAnimationFrame
            {
                SpriteId = SpriteId ?? Sprite ?? defaultSpriteId ?? string.Empty,
                FrameIndex = Frame ?? FrameIndex,
                DurationSeconds = DurationMs.HasValue ? DurationMs.Value / 1000f : DurationSeconds,
                OffsetX = OffsetX,
                OffsetY = OffsetY,
                EventId = EventId
            };
        }
    }
}
