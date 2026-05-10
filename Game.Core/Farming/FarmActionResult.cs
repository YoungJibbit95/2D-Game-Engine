using Game.Core.Inventory;
using Game.Core.World;

namespace Game.Core.Farming;

public sealed record FarmActionResult(
    FarmActionStatus Status,
    TilePos Position,
    ItemStack Item = default,
    string? CropId = null)
{
    public static FarmActionResult Completed(TilePos position, ItemStack item = default, string? cropId = null)
    {
        return new FarmActionResult(FarmActionStatus.Completed, position, item, cropId);
    }

    public static FarmActionResult Failed(FarmActionStatus status, TilePos position, string? cropId = null)
    {
        return new FarmActionResult(status, position, ItemStack.Empty, cropId);
    }
}
