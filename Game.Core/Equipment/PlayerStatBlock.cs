namespace Game.Core.Equipment;

public readonly record struct PlayerStatBlock(
    int MaxHealth,
    int Defense,
    float MovementSpeedMultiplier,
    float MeleeDamageMultiplier,
    float RangedDamageMultiplier,
    float MiningSpeedMultiplier)
{
    public static PlayerStatBlock Base { get; } = new(
        MaxHealth: 100,
        Defense: 0,
        MovementSpeedMultiplier: 1f,
        MeleeDamageMultiplier: 1f,
        RangedDamageMultiplier: 1f,
        MiningSpeedMultiplier: 1f);
}
