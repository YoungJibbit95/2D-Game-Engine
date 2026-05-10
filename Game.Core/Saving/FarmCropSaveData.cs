namespace Game.Core.Saving;

public sealed record FarmCropSaveData
{
    public required string CropId { get; init; }

    public int PlantedDay { get; init; }

    public int DaysUntilHarvest { get; init; }

    public int HarvestCount { get; init; }
}
