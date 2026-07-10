namespace Game.Core.Saving;

public enum PlayerLoadWarningKind
{
    InvalidEquipmentEntry,
    InvalidEquipmentSlot,
    MissingEquipmentItemId,
    UnknownEquipmentItem,
    IncompatibleEquipmentItem,
    DuplicateEquipmentSlot,
    InvalidStatusEffectEntry,
    MissingStatusEffectId,
    UnknownStatusEffect,
    InvalidStatusEffectDuration,
    DuplicateStatusEffect
}

public sealed record PlayerLoadWarning(
    PlayerLoadWarningKind Kind,
    string? SavedId,
    string Message);
