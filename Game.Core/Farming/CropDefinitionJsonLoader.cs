using Game.Core.Data;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Game.Core.Farming;

public sealed class CropDefinitionJsonLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public CropRegistry LoadRegistryFromDirectory(string directoryPath)
    {
        return CropRegistry.Create(LoadDefinitionsFromDirectory(directoryPath));
    }

    public IReadOnlyList<CropDefinition> LoadDefinitionsFromDirectory(string directoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);

        if (!Directory.Exists(directoryPath))
        {
            return Array.Empty<CropDefinition>();
        }

        return Directory
            .EnumerateFiles(directoryPath, "*.json", SearchOption.AllDirectories)
            .Order(StringComparer.OrdinalIgnoreCase)
            .Select(LoadDefinitionFromFile)
            .ToArray();
    }

    public CropDefinition LoadDefinitionFromFile(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        return LoadDefinitionFromJson(File.ReadAllText(filePath), filePath);
    }

    public CropDefinition LoadDefinitionFromJson(string json)
    {
        return LoadDefinitionFromJson(json, "inline json");
    }

    private static CropDefinition LoadDefinitionFromJson(string json, string source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        var definition = JsonSerializer.Deserialize<CropDefinitionDto>(json, Options);
        if (definition is null)
        {
            throw new JsonException($"Crop definition was empty: {source}");
        }

        return definition.ToDefinition();
    }

    private sealed record CropDefinitionDto
    {
        public string? Id { get; init; }

        public string? DisplayName { get; init; }

        public string? TexturePath { get; init; }

        [JsonPropertyName("texture")]
        public string? Texture { get; init; }

        public string? SeedItemId { get; init; }

        [JsonPropertyName("seedItem")]
        public string? SeedItem { get; init; }

        public string? HarvestItemId { get; init; }

        [JsonPropertyName("harvestItem")]
        public string? HarvestItem { get; init; }

        public int BaseYield { get; init; } = 1;

        public int ExtraYieldChancePercent { get; init; }

        public int RegrowDays { get; init; }

        public bool RequiresWater { get; init; } = true;

        public int[] GrowthStageDays { get; init; } = Array.Empty<int>();

        public FarmSeason[] Seasons { get; init; } = Array.Empty<FarmSeason>();

        public string[] Tags { get; init; } = Array.Empty<string>();

        public CropDefinition ToDefinition()
        {
            return new CropDefinition
            {
                Id = Id ?? string.Empty,
                DisplayName = DisplayName ?? string.Empty,
                TexturePath = TexturePath ?? Texture ?? string.Empty,
                SeedItemId = SeedItemId ?? SeedItem ?? string.Empty,
                HarvestItemId = HarvestItemId ?? HarvestItem ?? string.Empty,
                BaseYield = BaseYield,
                ExtraYieldChancePercent = ExtraYieldChancePercent,
                RegrowDays = RegrowDays,
                RequiresWater = RequiresWater,
                GrowthStageDays = GrowthStageDays,
                Seasons = Seasons,
                Tags = DefinitionTags.Normalize(Tags)
            };
        }
    }
}
