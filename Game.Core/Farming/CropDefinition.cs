using Game.Core.Data;

namespace Game.Core.Farming;

public sealed record CropDefinition
{
    public required string Id { get; init; }

    public required string DisplayName { get; init; }

    public required string TexturePath { get; init; }

    public required string SeedItemId { get; init; }

    public required string HarvestItemId { get; init; }

    public int BaseYield { get; init; } = 1;

    public int ExtraYieldChancePercent { get; init; }

    public int RegrowDays { get; init; }

    public bool RequiresWater { get; init; } = true;

    public IReadOnlyList<int> GrowthStageDays { get; init; } = Array.Empty<int>();

    public IReadOnlyList<FarmSeason> Seasons { get; init; } = Array.Empty<FarmSeason>();

    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();

    public int TotalGrowthDays => GrowthStageDays.Sum();

    public bool CanGrowIn(FarmSeason season)
    {
        return Seasons.Count == 0 ||
               Seasons.Contains(FarmSeason.Any) ||
               Seasons.Contains(season);
    }

    public bool HasTag(string tag)
    {
        return DefinitionTags.HasTag(Tags, tag);
    }
}
