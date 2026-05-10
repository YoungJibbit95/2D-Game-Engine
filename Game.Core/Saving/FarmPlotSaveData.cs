namespace Game.Core.Saving;

public sealed record FarmPlotSaveData
{
    public int TileX { get; init; }

    public int TileY { get; init; }

    public bool IsTilled { get; init; }

    public bool IsWatered { get; init; }

    public FarmCropSaveData? Crop { get; init; }
}
