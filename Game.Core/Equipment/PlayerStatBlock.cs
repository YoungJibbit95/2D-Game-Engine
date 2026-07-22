namespace Game.Core.Equipment;

public readonly record struct PlayerStatBlock(
    int MaxHealth,
    int Defense,
    float MovementSpeedMultiplier,
    float MeleeDamageMultiplier,
    float RangedDamageMultiplier,
    float MiningSpeedMultiplier,
    int MaxMana = 20,
    float MagicDamageMultiplier = 1f,
    float ManaCostMultiplier = 1f,
    float ManaRegenMultiplier = 1f,
    bool CanDoubleJump = false,
    bool CanWallJump = false,
    bool CanFly = false,
    bool CanGlide = false)
{
    public static PlayerStatBlock Base { get; } = new(
        MaxHealth: 100,
        Defense: 0,
        MovementSpeedMultiplier: 1f,
        MeleeDamageMultiplier: 1f,
        RangedDamageMultiplier: 1f,
        MiningSpeedMultiplier: 1f,
        MaxMana: 20,
        MagicDamageMultiplier: 1f,
        ManaCostMultiplier: 1f,
        ManaRegenMultiplier: 1f,
        CanDoubleJump: false,
        CanWallJump: false,
        CanFly: false,
        CanGlide: false);
}
