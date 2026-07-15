using Game.Core.Combat;
using Game.Core.Entities;
using Game.Core.Loot;
using Game.Core.Randomness;
using Xunit;

namespace Game.Tests.LootTests;

public sealed class DeterministicLootTests
{
    [Fact]
    public void Roll_SameSeedAndContextProducesSameDrops()
    {
        var table = CreateMixedTable();
        var context = new LootKillContext(10, EntityFaction.Friendly, DamageType.Melee, true, 140, new[] { "cave", "arachnid" });

        var first = new LootRoller(new Random(1234)).Roll(table, context);
        var second = new LootRoller(new Random(1234)).Roll(table, context);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Roll_ReturnsGuaranteedAndWeightedEntries()
    {
        var drops = new LootRoller(new Random(2)).Roll(CreateMixedTable(), LootKillContext.Empty);

        Assert.Contains(drops, drop => drop.ItemId == "gel" && drop.Count == 2);
        Assert.Equal(2, drops.Count);
        Assert.Contains(drops, drop => drop.ItemId is "copper_coin" or "wood");
    }

    [Fact]
    public void Roll_AppliesKillContextConditions()
    {
        var table = new LootTableDefinition
        {
            Id = "context",
            Entries = new[]
            {
                new LootEntryDefinition
                {
                    ItemId = "poison_arrow",
                    Min = 1,
                    Max = 1,
                    Guaranteed = true,
                    RequiredKillerFaction = EntityFaction.Friendly,
                    DamageTypes = new[] { DamageType.Melee },
                    RequiresNight = true,
                    MinVictimDepth = 100,
                    RequiredVictimTags = new[] { "arachnid" }
                }
            }
        };

        var matching = new LootKillContext(1, EntityFaction.Friendly, DamageType.Melee, true, 120, new[] { "arachnid" });
        var wrongDamage = matching with { DamageType = DamageType.Ranged };

        Assert.Single(new LootRoller(new Random(1)).Roll(table, matching));
        Assert.Empty(new LootRoller(new Random(1)).Roll(table, wrongDamage));
    }

    [Fact]
    public void Loader_ReadsWeightedGuaranteedAndContextFields()
    {
        const string json = """
        {
          "id": "spider",
          "weightedRolls": 2,
          "entries": [
            { "itemId": "gel", "min": 1, "max": 2, "guaranteed": true },
            {
              "itemId": "poison_arrow",
              "min": 1,
              "max": 3,
              "weight": 2.5,
              "isRare": true,
              "requiredKillerFaction": "friendly",
              "damageTypes": ["melee"]
            }
          ]
        }
        """;

        var table = new LootTableJsonLoader().LoadDefinitionFromJson(json);

        Assert.Equal(2, table.WeightedRolls);
        Assert.True(table.Entries[0].Guaranteed);
        Assert.Equal(2.5f, table.Entries[1].Weight);
        Assert.True(table.Entries[1].IsRare);
        Assert.Equal(EntityFaction.Friendly, table.Entries[1].RequiredKillerFaction);
        Assert.Equal(DamageType.Melee, Assert.Single(table.Entries[1].DamageTypes));
    }

    [Fact]
    public void RollDeterministic_IsIndependentFromAmbientRollOrderAndTagOrder()
    {
        var table = CreateMixedTable();
        var firstContext = new LootKillContext(7, EntityFaction.Friendly, DamageType.Melee, true, 140, new[] { "elite", "crystal" });
        var reorderedContext = firstContext with { VictimTags = new[] { "crystal", "elite" } };
        var key = new LootRollKey(4242, 19, 33);
        var firstRoller = new LootRoller(new Random(1));
        var secondRoller = new LootRoller(new Random(999));

        firstRoller.Roll(table, firstContext);
        firstRoller.Roll(table, firstContext);
        var first = firstRoller.RollDeterministic(table, firstContext, key);
        var second = secondRoller.RollDeterministic(table, reorderedContext, key);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Constructor_AcceptsNamedDeterministicStream()
    {
        var firstRegistry = new SessionRandomRegistry(9123);
        var secondRegistry = new SessionRandomRegistry(9123);
        var first = new LootRoller(firstRegistry.GetStream("loot.sequence")).Roll(CreateMixedTable());
        var second = new LootRoller(secondRegistry.GetStream("loot.sequence")).Roll(CreateMixedTable());

        Assert.Equal(first, second);
    }

    [Fact]
    public void Roll_EventModifiersScaleQuantityAndOnlyRareEntryChance()
    {
        var table = new LootTableDefinition
        {
            Id = "event-modified",
            Entries =
            [
                new LootEntryDefinition
                {
                    ItemId = "gel",
                    Min = 2,
                    Max = 2,
                    Guaranteed = true
                },
                new LootEntryDefinition
                {
                    ItemId = "wood",
                    Min = 1,
                    Max = 1,
                    Chance = 1f
                },
                new LootEntryDefinition
                {
                    ItemId = "mana_crystal",
                    Min = 1,
                    Max = 1,
                    Chance = 1f,
                    IsRare = true
                }
            ]
        };
        var suppressedRare = LootKillContext.Empty with
        {
            QuantityMultiplier = 1f,
            RareChanceMultiplier = 0f
        };
        var doubled = suppressedRare with
        {
            QuantityMultiplier = 2f,
            RareChanceMultiplier = 1f
        };

        var withoutRare = new LootRoller(new Random(5)).Roll(table, suppressedRare);
        var withDoubleQuantity = new LootRoller(new Random(5)).Roll(table, doubled);

        Assert.Contains(new Game.Core.Inventory.ItemStack("gel", 2), withoutRare);
        Assert.Contains(new Game.Core.Inventory.ItemStack("wood", 1), withoutRare);
        Assert.DoesNotContain(withoutRare, value => value.ItemId == "mana_crystal");
        Assert.Contains(new Game.Core.Inventory.ItemStack("gel", 4), withDoubleQuantity);
        Assert.Contains(new Game.Core.Inventory.ItemStack("wood", 2), withDoubleQuantity);
        Assert.Contains(new Game.Core.Inventory.ItemStack("mana_crystal", 2), withDoubleQuantity);
    }

    [Fact]
    public void Roll_RejectsInvalidRuntimeModifiers()
    {
        var invalid = LootKillContext.Empty with { RareChanceMultiplier = float.NaN };

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new LootRoller(new Random(1)).Roll(CreateMixedTable(), invalid));
    }

    private static LootTableDefinition CreateMixedTable()
    {
        return new LootTableDefinition
        {
            Id = "mixed",
            WeightedRolls = 1,
            Entries = new[]
            {
                new LootEntryDefinition { ItemId = "gel", Min = 2, Max = 2, Guaranteed = true },
                new LootEntryDefinition { ItemId = "copper_coin", Min = 1, Max = 4, Weight = 3 },
                new LootEntryDefinition { ItemId = "wood", Min = 1, Max = 2, Weight = 1 }
            }
        };
    }
}
