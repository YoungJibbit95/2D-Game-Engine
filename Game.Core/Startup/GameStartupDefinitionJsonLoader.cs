using Game.Core.Data;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Game.Core.Startup;

public sealed class GameStartupDefinitionJsonLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public GameStartupRegistry LoadRegistryFromDirectory(string directoryPath)
    {
        return GameStartupRegistry.Create(LoadDefinitionsFromDirectory(directoryPath));
    }

    public IReadOnlyList<GameStartupDefinition> LoadDefinitionsFromDirectory(string directoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);

        if (!Directory.Exists(directoryPath))
        {
            return Array.Empty<GameStartupDefinition>();
        }

        return Directory
            .EnumerateFiles(directoryPath, "*.json", SearchOption.AllDirectories)
            .Order(StringComparer.OrdinalIgnoreCase)
            .Select(LoadDefinitionFromFile)
            .ToArray();
    }

    public GameStartupDefinition LoadDefinitionFromFile(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        return LoadDefinitionFromJson(File.ReadAllText(filePath), filePath);
    }

    public GameStartupDefinition LoadDefinitionFromJson(string json)
    {
        return LoadDefinitionFromJson(json, "inline json");
    }

    private static GameStartupDefinition LoadDefinitionFromJson(string json, string source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        var dto = JsonSerializer.Deserialize<GameStartupDefinitionDto>(json, Options);
        if (dto is null)
        {
            throw new JsonException($"Game startup definition was empty: {source}");
        }

        return dto.ToDefinition();
    }

    private sealed record GameStartupDefinitionDto
    {
        public string? Id { get; init; }

        public string? DisplayName { get; init; }

        public string? WorldProfileId { get; init; }

        public string? StartupMapId { get; init; }

        public int SelectedHotbarSlot { get; init; }

        public StarterItemDefinitionDto[] StarterItems { get; init; } = Array.Empty<StarterItemDefinitionDto>();

        public string[] Tags { get; init; } = Array.Empty<string>();

        public GameStartupDefinition ToDefinition()
        {
            return new GameStartupDefinition
            {
                Id = Id ?? string.Empty,
                DisplayName = DisplayName ?? string.Empty,
                WorldProfileId = WorldProfileId,
                StartupMapId = StartupMapId,
                SelectedHotbarSlot = SelectedHotbarSlot,
                StarterItems = StarterItems.Select(item => item.ToDefinition()).ToArray(),
                Tags = DefinitionTags.Normalize(Tags)
            };
        }
    }

    private sealed record StarterItemDefinitionDto
    {
        public string? ItemId { get; init; }

        public int Count { get; init; } = 1;

        public StarterInventoryTarget Target { get; init; }

        public int? Slot { get; init; }

        public int SortOrder { get; init; }

        public StarterItemDefinition ToDefinition()
        {
            return new StarterItemDefinition
            {
                ItemId = ItemId ?? string.Empty,
                Count = Count,
                Target = Target,
                Slot = Slot,
                SortOrder = SortOrder
            };
        }
    }
}
