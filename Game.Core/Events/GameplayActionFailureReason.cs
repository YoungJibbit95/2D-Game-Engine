namespace Game.Core.Events;

public enum GameplayActionFailureReason
{
    None,
    NoSelectedItem,
    ItemNotFound,
    NoAction,
    Cooldown,
    OutOfReach,
    Occupied,
    InsufficientToolPower,
    InsufficientMana,
    InsufficientAmmo,
    InsufficientStamina,
    InsufficientItem,
    InvalidItem,
    InvalidTarget,
    UnsupportedPlacement,
    NoBenefit,
    ActorUnavailable
}
