using Game.Core.Loot;
using Xunit;

namespace Game.Tests.LootTests;

public sealed class LootTableTests
{
    [Fact]
    public void Loader_ReadsLootTableJson()
    {
        const string json = """
        {
          "id": "slime_basic",
          "entries": [
            { "itemId": "gel", "min": 1, "max": 3, "chance": 1.0 }
          ]
        }
        """;

        var table = new LootTableJsonLoader().LoadDefinitionFromJson(json);

        Assert.Equal("slime_basic", table.Id);
        Assert.Equal("gel", table.Entries[0].ItemId);
    }

    [Fact]
    public void Roll_AlwaysReturnsGuaranteedDrops()
    {
        var table = new LootTableDefinition
        {
            Id = "test",
            Entries = new[]
            {
                new LootEntryDefinition { ItemId = "gel", Min = 2, Max = 2, Chance = 1f }
            }
        };

        var drops = new LootRoller(new Random(1)).Roll(table);

        Assert.Single(drops);
        Assert.Equal("gel", drops[0].ItemId);
        Assert.Equal(2, drops[0].Count);
    }
}
