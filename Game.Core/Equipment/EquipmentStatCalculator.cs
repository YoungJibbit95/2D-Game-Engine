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
        }

        return new PlayerStatBlock(
            MaxHealth: Math.Max(1, maxHealth),
            Defense: Math.Max(0, defense),
            MovementSpeedMultiplier: Math.Max(0.1f, movement),
            MeleeDamageMultiplier: Math.Max(0.1f, melee),
            RangedDamageMultiplier: Math.Max(0.1f, ranged),
            MiningSpeedMultiplier: Math.Max(0.1f, mining));
    }
}
