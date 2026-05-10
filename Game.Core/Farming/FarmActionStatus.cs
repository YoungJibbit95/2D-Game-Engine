namespace Game.Core.Farming;

public enum FarmActionStatus
{
    None,
    Completed,
    OutOfBounds,
    NotTillable,
    AlreadyTilled,
    NotTilled,
    AlreadyOccupied,
    NoCrop,
    CropNotMature,
    WrongSeason,
    MissingSeed,
    InventoryFull,
    UnknownCrop
}
