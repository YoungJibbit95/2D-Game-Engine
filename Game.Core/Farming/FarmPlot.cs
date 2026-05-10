using Game.Core.World;

namespace Game.Core.Farming;

public sealed class FarmPlot
{
    public FarmPlot(TilePos position)
    {
        Position = position;
    }

    public TilePos Position { get; }

    public bool IsTilled { get; set; }

    public bool IsWatered { get; set; }

    public CropInstance? Crop { get; set; }

    public bool HasCrop => Crop is not null;
}
