using Game.Core.Items;

namespace Game.Core.Equipment;

public sealed class EquipmentStatCalculator
{
    public PlayerStatBlock Calculate(PlayerStatBlock baseStats, EquipmentLoadout loadout, IItemDefinitionProvider items)
    {
        ArgumentNullException.ThrowIfNull(loadout);
        ArgumentNullException.ThrowIfNull(items);

        var maxHealth = baseStats.MaxHealth;
        var defense = baseStats.Defense;
        var movement = baseStats.MovementSpeedMultiplier;
        var melee = baseStats.MeleeDamageMultiplier;
        var ranged = baseStats.RangedDamageMultiplier;
        var mining = baseStats.MiningSpeedMultiplier;
        var maxMana = baseStats.MaxMana;
        var magic = baseStats.MagicDamageMultiplier;
        var manaCost = baseStats.ManaCostMultiplier;
        var manaRegen = baseStats.ManaRegenMultiplier;
        var canDoubleJump = baseStats.CanDoubleJump;
        var canWallJump = baseStats.CanWallJump;
        var canFly = baseStats.CanFly;
        var canGlide = baseStats.CanGlide;

        foreach (var stack in loadout.Slots.Values)
        {
            if (stack.IsEmpty)
            {
                continue;
            }

            var definition = items.GetById(stack.ItemId);
            maxHealth += definition.MaxHealthBonus;
            defense += definition.Defense;
            movement += definition.MovementSpeedBonus;
            melee += definition.MeleeDamageBonus;
            ranged += definition.RangedDamageBonus;
            mining += definition.MiningSpeedBonus;
            maxMana += definition.MaxManaBonus;
            magic += definition.MagicDamageBonus;
            manaCost -= definition.ManaCostReduction;
            manaRegen += definition.ManaRegenBonus;
            canDoubleJump |= definition.CanDoubleJump || definition.Mobility?.HasDoubleJump == true;
            canWallJump |= definition.CanWallJump || definition.Mobility?.CanWallJump == true;
            canFly |= definition.CanFly || definition.Mobility?.HasFlight == true;
            canGlide |= definition.CanGlide || definition.Mobility?.HasGlide == true;
        }

        return new PlayerStatBlock(
            MaxHealth: Math.Max(1, maxHealth),
            Defense: Math.Max(0, defense),
            MovementSpeedMultiplier: Math.Max(0.1f, movement),
            MeleeDamageMultiplier: Math.Max(0.1f, melee),
            RangedDamageMultiplier: Math.Max(0.1f, ranged),
            MiningSpeedMultiplier: Math.Max(0.1f, mining),
            MaxMana: Math.Max(0, maxMana),
            MagicDamageMultiplier: Math.Max(0.1f, magic),
            ManaCostMultiplier: Math.Clamp(manaCost, 0.1f, 3f),
            ManaRegenMultiplier: Math.Max(0f, manaRegen),
            CanDoubleJump: canDoubleJump,
            CanWallJump: canWallJump,
            CanFly: canFly,
            CanGlide: canGlide);
    }
}
