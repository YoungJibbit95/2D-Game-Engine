using Game.Core.Inventory;
using Game.Core.Randomness;

namespace Game.Core.Loot;

public sealed class LootRoller
{
    private readonly Random _random;

    public LootRoller(Random random)
    {
        ArgumentNullException.ThrowIfNull(random);
        _random = random;
    }

    public LootRoller(DeterministicRandomStream stream)
        : this(new DeterministicRandomAdapter(stream))
    {
    }

    public IReadOnlyList<ItemStack> Roll(LootTableDefinition table)
    {
        return Roll(table, LootKillContext.Empty);
    }

    public IReadOnlyList<ItemStack> Roll(LootTableDefinition table, LootKillContext context)
    {
        ArgumentNullException.ThrowIfNull(table);

        var source = new SystemRandomSource(_random);
        return RollCore(table, context, ref source);
    }

    public IReadOnlyList<ItemStack> RollDeterministic(
        LootTableDefinition table,
        LootKillContext context,
        LootRollKey key)
    {
        ArgumentNullException.ThrowIfNull(table);
        var source = new DeterministicRandomSource(CreateStableSeed(table, context, key));
        return RollCore(table, context, ref source);
    }

    private static IReadOnlyList<ItemStack> RollCore<TRandom>(
        LootTableDefinition table,
        LootKillContext context,
        ref TRandom random)
        where TRandom : struct, ILootRandomSource
    {
        ValidateModifiers(context);

        var drops = new List<ItemStack>();
        var weighted = new List<LootEntryDefinition>();

        foreach (var entry in table.Entries)
        {
            if (!MatchesContext(entry, context))
            {
                continue;
            }

            if (entry.Guaranteed)
            {
                AddDrop(drops, entry, context.QuantityMultiplier, ref random);
            }
            else if (entry.Weight > 0)
            {
                weighted.Add(entry);
            }
            else if (random.NextSingle() < ResolveChance(entry, context))
            {
                AddDrop(drops, entry, context.QuantityMultiplier, ref random);
            }
        }

        RollWeighted(table, weighted, drops, context, ref random);

        return drops;
    }

    private static void RollWeighted<TRandom>(
        LootTableDefinition table,
        List<LootEntryDefinition> candidates,
        List<ItemStack> drops,
        LootKillContext context,
        ref TRandom random)
        where TRandom : struct, ILootRandomSource
    {
        for (var roll = 0; roll < table.WeightedRolls && candidates.Count > 0; roll++)
        {
            var totalWeight = 0f;
            for (var index = 0; index < candidates.Count; index++)
            {
                totalWeight += candidates[index].Weight;
            }

            if (totalWeight <= 0)
            {
                return;
            }

            var selection = random.NextSingle() * totalWeight;
            var selectedIndex = candidates.Count - 1;
            for (var index = 0; index < candidates.Count; index++)
            {
                selection -= candidates[index].Weight;
                if (selection < 0)
                {
                    selectedIndex = index;
                    break;
                }
            }

            var selected = candidates[selectedIndex];
            if (random.NextSingle() < ResolveChance(selected, context))
            {
                AddDrop(drops, selected, context.QuantityMultiplier, ref random);
            }

            if (!table.AllowDuplicateWeightedEntries)
            {
                candidates.RemoveAt(selectedIndex);
            }
        }
    }

    private static void AddDrop<TRandom>(
        List<ItemStack> drops,
        LootEntryDefinition entry,
        float quantityMultiplier,
        ref TRandom random)
        where TRandom : struct, ILootRandomSource
    {
        var baseCount = random.Next(entry.Min, entry.Max + 1);
        var scaled = baseCount * quantityMultiplier;
        var count = (int)MathF.Floor(scaled);
        var fractional = scaled - count;
        if (fractional > 0f && random.NextSingle() < fractional)
        {
            count++;
        }

        if (count > 0)
        {
            drops.Add(new ItemStack(entry.ItemId, count));
        }
    }

    private static float ResolveChance(LootEntryDefinition entry, in LootKillContext context)
    {
        var multiplier = entry.IsRare ? context.RareChanceMultiplier : 1f;
        return Math.Clamp(entry.Chance * multiplier, 0f, 1f);
    }

    private static void ValidateModifiers(in LootKillContext context)
    {
        if (!float.IsFinite(context.QuantityMultiplier) || context.QuantityMultiplier < 0f ||
            !float.IsFinite(context.RareChanceMultiplier) || context.RareChanceMultiplier < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(context), "Loot modifiers must be finite and non-negative.");
        }
    }

    private static bool MatchesContext(LootEntryDefinition entry, LootKillContext context)
    {
        if (entry.RequiredKillerFaction is not null && entry.RequiredKillerFaction != context.KillerFaction)
        {
            return false;
        }

        if (entry.DamageTypes.Count > 0 &&
            (context.DamageType is null || !entry.DamageTypes.Contains(context.DamageType.Value)))
        {
            return false;
        }

        if (entry.RequiresNight is not null && entry.RequiresNight != context.IsNight)
        {
            return false;
        }

        if (entry.MinVictimDepth is not null && (context.VictimDepth is null || context.VictimDepth < entry.MinVictimDepth))
        {
            return false;
        }

        if (entry.MaxVictimDepth is not null && (context.VictimDepth is null || context.VictimDepth > entry.MaxVictimDepth))
        {
            return false;
        }

        for (var index = 0; index < entry.RequiredVictimTags.Count; index++)
        {
            if (!context.VictimTags.Any(tag => string.Equals(tag, entry.RequiredVictimTags[index], StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }
        }

        return true;
    }

    private static uint CreateStableSeed(LootTableDefinition table, LootKillContext context, LootRollKey key)
    {
        var hash = 2166136261u;
        AddInt(ref hash, key.WorldSeed);
        AddLong(ref hash, key.DeathSequence);
        AddInt(ref hash, key.VictimEntityId);
        AddString(ref hash, table.Id);
        AddInt(ref hash, context.KillerEntityId ?? -1);
        AddInt(ref hash, context.KillerFaction is null ? -1 : (int)context.KillerFaction.Value);
        AddInt(ref hash, context.DamageType is null ? -1 : (int)context.DamageType.Value);
        AddInt(ref hash, context.IsNight is null ? -1 : context.IsNight.Value ? 1 : 0);
        AddInt(ref hash, context.VictimDepth ?? -1);
        foreach (var tag in context.VictimTags.Order(StringComparer.OrdinalIgnoreCase))
        {
            AddString(ref hash, tag);
        }

        return hash == 0 ? 0x9E3779B9u : hash;
    }

    private static void AddString(ref uint hash, string value)
    {
        for (var index = 0; index < value.Length; index++)
        {
            AddInt(ref hash, char.ToUpperInvariant(value[index]));
        }
    }

    private static void AddLong(ref uint hash, long value)
    {
        AddInt(ref hash, unchecked((int)value));
        AddInt(ref hash, unchecked((int)(value >> 32)));
    }

    private static void AddInt(ref uint hash, int value)
    {
        hash ^= unchecked((uint)value);
        hash *= 16777619u;
    }

    private interface ILootRandomSource
    {
        float NextSingle();

        int Next(int minInclusive, int maxExclusive);
    }

    private readonly struct SystemRandomSource : ILootRandomSource
    {
        private readonly Random _random;

        public SystemRandomSource(Random random)
        {
            _random = random;
        }

        public float NextSingle() => _random.NextSingle();

        public int Next(int minInclusive, int maxExclusive) => _random.Next(minInclusive, maxExclusive);
    }

    private struct DeterministicRandomSource : ILootRandomSource
    {
        private uint _state;

        public DeterministicRandomSource(uint seed)
        {
            _state = seed;
        }

        public float NextSingle()
        {
            return (NextUInt() >> 8) * (1f / 16777216f);
        }

        public int Next(int minInclusive, int maxExclusive)
        {
            var range = maxExclusive - minInclusive;
            if (range <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxExclusive));
            }

            return minInclusive + (int)(NextUInt() % (uint)range);
        }

        private uint NextUInt()
        {
            var value = _state;
            value ^= value << 13;
            value ^= value >> 17;
            value ^= value << 5;
            _state = value;
            return value;
        }
    }
}
