namespace Game.Core.Biomes;

public sealed class BiomeMap
{
    private readonly List<BiomeRegion> _regions = new();
    private readonly string _fallbackBiomeId;

    public BiomeMap(string fallbackBiomeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fallbackBiomeId);
        _fallbackBiomeId = fallbackBiomeId;
    }

    public void AddRegion(int startTileX, int endTileXInclusive, string biomeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(biomeId);

        if (endTileXInclusive < startTileX)
        {
            throw new ArgumentException("Biome region end must be greater than or equal to start.");
        }

        _regions.Add(new BiomeRegion(startTileX, endTileXInclusive, biomeId));
    }

    public string GetBiomeAt(int tileX, int tileY)
    {
        for (var index = _regions.Count - 1; index >= 0; index--)
        {
            var region = _regions[index];
            if (tileX >= region.StartTileX && tileX <= region.EndTileXInclusive)
            {
                return region.BiomeId;
            }
        }

        return _fallbackBiomeId;
    }

    public IReadOnlyList<BiomeRegionSnapshot> GetRegions()
    {
        return _regions
            .Select(region => new BiomeRegionSnapshot(region.StartTileX, region.EndTileXInclusive, region.BiomeId))
            .ToArray();
    }

    private readonly record struct BiomeRegion(int StartTileX, int EndTileXInclusive, string BiomeId);
}

public readonly record struct BiomeRegionSnapshot(int StartTileX, int EndTileXInclusive, string BiomeId);
