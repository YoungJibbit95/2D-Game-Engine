namespace Game.Core.Farming;

public sealed class CropInstance
{
    public CropInstance(string cropId, int plantedDay, int daysUntilHarvest, int harvestCount = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cropId);

        if (daysUntilHarvest < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(daysUntilHarvest), "Days until harvest must not be negative.");
        }

        if (harvestCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(harvestCount), "Harvest count must not be negative.");
        }

        CropId = cropId;
        PlantedDay = plantedDay;
        DaysUntilHarvest = daysUntilHarvest;
        HarvestCount = harvestCount;
    }

    public string CropId { get; }

    public int PlantedDay { get; }

    public int DaysUntilHarvest { get; private set; }

    public int HarvestCount { get; private set; }

    public bool IsMature => DaysUntilHarvest == 0;

    public void AdvanceGrowthDay()
    {
        if (DaysUntilHarvest > 0)
        {
            DaysUntilHarvest--;
        }
    }

    public void RestartRegrow(int regrowDays)
    {
        if (regrowDays <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(regrowDays), "Regrow days must be positive.");
        }

        HarvestCount++;
        DaysUntilHarvest = regrowDays;
    }

    public void RecordFinalHarvest()
    {
        HarvestCount++;
    }

    public int GetGrowthStageIndex(CropDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (!string.Equals(CropId, definition.Id, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Crop definition does not match crop instance.", nameof(definition));
        }

        if (IsMature)
        {
            return Math.Max(0, definition.GrowthStageDays.Count - 1);
        }

        var totalDays = definition.TotalGrowthDays;
        var elapsed = Math.Clamp(totalDays - DaysUntilHarvest, 0, totalDays);
        var cumulative = 0;
        for (var index = 0; index < definition.GrowthStageDays.Count; index++)
        {
            cumulative += definition.GrowthStageDays[index];
            if (elapsed < cumulative)
            {
                return index;
            }
        }

        return Math.Max(0, definition.GrowthStageDays.Count - 1);
    }
}
