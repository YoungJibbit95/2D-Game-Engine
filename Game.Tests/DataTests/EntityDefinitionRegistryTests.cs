using Game.Core.Entities;
using Xunit;

namespace Game.Tests.DataTests;

public sealed class EntityDefinitionRegistryTests
{
    [Fact]
    public void Loader_ReadsEntityJson()
    {
        const string json = """
        {
          "id": "slime",
          "displayName": "Slime",
          "texture": "entities/slime",
          "maxHealth": 20,
          "width": 16,
          "height": 14,
          "aiBehavior": "slime",
          "lootTable": "slime_basic",
          "contactDamage": 12,
          "contactKnockback": 220,
          "onContactEffects": [
            { "effect": "poisoned", "chance": 0.5, "durationSeconds": 2.5 }
          ]
        }
        """;

        var entity = new EntityDefinitionJsonLoader().LoadDefinitionFromJson(json);

        Assert.Equal("slime", entity.Id);
        Assert.Equal("entities/slime", entity.TexturePath);
        Assert.Equal("slime_basic", entity.LootTableId);
        Assert.Equal(12, entity.ContactDamage);
        Assert.Equal(220, entity.ContactKnockback);
        var effect = Assert.Single(entity.OnContactEffects);
        Assert.Equal("poisoned", effect.EffectId);
        Assert.Equal(0.5f, effect.Chance);
    }

    [Fact]
    public void Registry_MapsEntityById()
    {
        var registry = EntityDefinitionRegistry.Create(new[]
        {
            new EntityDefinition
            {
                Id = "slime",
                DisplayName = "Slime",
                TexturePath = "entities/slime",
                MaxHealth = 20
            }
        });

        Assert.True(registry.TryGetById("SLIME", out _));
    }
}
