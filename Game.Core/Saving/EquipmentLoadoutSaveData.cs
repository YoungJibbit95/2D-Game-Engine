namespace Game.Core.Saving;

public sealed record EquipmentLoadoutSaveData
{
    public IReadOnlyList<EquipmentSlotSaveData> Slots { get; init; } = Array.Empty<EquipmentSlotSaveData>();
}

public sealed record EquipmentSlotSaveData
{
    public string SlotId { get; init; } = string.Empty;

    public string ItemId { get; init; } = string.Empty;
}
