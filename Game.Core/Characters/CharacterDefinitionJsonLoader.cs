using Game.Core.Data;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Game.Core.Characters;

public sealed class CharacterDefinitionJsonLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public CharacterDefinitionRegistry LoadRegistryFromDirectory(string directoryPath)
    {
        return CharacterDefinitionRegistry.Create(LoadDefinitionsFromDirectory(directoryPath));
    }

    public IReadOnlyList<CharacterDefinition> LoadDefinitionsFromDirectory(string directoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        if (!Directory.Exists(directoryPath))
        {
            return Array.Empty<CharacterDefinition>();
        }

        return Directory
            .EnumerateFiles(directoryPath, "*.json", SearchOption.AllDirectories)
            .Order(StringComparer.OrdinalIgnoreCase)
            .Select(LoadDefinitionFromFile)
            .ToArray();
    }

    public CharacterDefinition LoadDefinitionFromFile(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        return LoadDefinitionFromJson(File.ReadAllText(filePath), filePath);
    }

    public CharacterDefinition LoadDefinitionFromJson(string json)
    {
        return LoadDefinitionFromJson(json, "inline json");
    }

    private static CharacterDefinition LoadDefinitionFromJson(string json, string source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var dto = JsonSerializer.Deserialize<CharacterDefinitionDto>(json, Options);
        if (dto is null)
        {
            throw new JsonException($"Character definition was empty: {source}");
        }

        return dto.ToDefinition();
    }

    private sealed record CharacterDefinitionDto
    {
        public string? Id { get; init; }

        public string? DisplayName { get; init; }

        public float Width { get; init; } = 12f;

        public float Height { get; init; } = 28f;

        public CharacterAppearance? DefaultAppearance { get; init; }

        public CharacterAnimationSetDto? AnimationSet { get; init; }

        public string[] Tags { get; init; } = Array.Empty<string>();

        public CharacterDefinition ToDefinition()
        {
            return new CharacterDefinition
            {
                Id = Id ?? string.Empty,
                DisplayName = DisplayName ?? Id ?? string.Empty,
                Width = Width,
                Height = Height,
                DefaultAppearance = DefaultAppearance ?? new CharacterAppearance(),
                AnimationSet = AnimationSet?.ToDefinition(Id ?? string.Empty) ?? new CharacterAnimationSetDefinition
                {
                    Id = $"{Id}.animations",
                    StateClips = new Dictionary<CharacterAnimationState, string>()
                },
                Tags = DefinitionTags.Normalize(Tags)
            };
        }
    }

    private sealed record CharacterAnimationSetDto
    {
        public string? Id { get; init; }

        public string? DisplayName { get; init; }

        public string? DefaultClipId { get; init; }

        public Dictionary<CharacterAnimationState, string> StateClips { get; init; } = new();

        [JsonPropertyName("states")]
        public Dictionary<CharacterAnimationState, string>? States { get; init; }

        public CharacterAnimationSetDefinition ToDefinition(string characterId)
        {
            return new CharacterAnimationSetDefinition
            {
                Id = Id ?? $"{characterId}.animations",
                DisplayName = DisplayName ?? string.Empty,
                DefaultClipId = DefaultClipId,
                StateClips = new Dictionary<CharacterAnimationState, string>(States ?? StateClips)
            };
        }
    }
}
