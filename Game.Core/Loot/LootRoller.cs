using Game.Core.Inventory;

namespace Game.Core.Loot;

public sealed class LootRoller
{
    private readonly Random _random;

    public LootRoller(Random random)
    {
        _random = random;
    }

    public IReadOnlyList<ItemStack> Roll(LootTableDefinition table)
    {
        ArgumentNullException.ThrowIfNull(table);

        var drops = new List<ItemStack>();

        foreach (var entry in table.Entries)
        {
            if (_random.NextSingle() > entry.Chance)
            {
                continue;
            }

            drops.Add(new ItemStack(entry.ItemId, _random.Next(entry.Min, entry.Max + 1)));
        }

        return drops;
    }
}
