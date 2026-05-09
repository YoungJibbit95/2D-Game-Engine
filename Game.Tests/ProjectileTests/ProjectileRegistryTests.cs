using Game.Core.Projectiles;
using Xunit;

namespace Game.Tests.ProjectileTests;

public sealed class ProjectileRegistryTests
{
    [Fact]
    public void Loader_ReadsProjectileJson()
    {
        const string json = """
        {
          "id": "wooden_arrow",
          "texture": "projectiles/wooden_arrow",
          "speed": 320,
          "damage": 5,
          "gravity": 0.2,
          "pierce": 0,
          "lifetime": 5.0,
          "onHitEffects": [
            { "effect": "poisoned", "chance": 1.0, "durationSeconds": 4.0 }
          ]
        }
        """;

        var projectile = new ProjectileDefinitionJsonLoader().LoadDefinitionFromJson(json);

        Assert.Equal("wooden_arrow", projectile.Id);
        Assert.Equal("projectiles/wooden_arrow", projectile.TexturePath);
        Assert.Equal(5, projectile.Damage);
        var effect = Assert.Single(projectile.OnHitEffects);
        Assert.Equal("poisoned", effect.EffectId);
        Assert.Equal(4.0f, effect.DurationSeconds);
    }

    [Fact]
    public void Registry_MapsProjectileById()
    {
        var registry = ProjectileRegistry.Create(new[]
        {
            new ProjectileDefinition
            {
                Id = "wooden_arrow",
                TexturePath = "projectiles/wooden_arrow",
                Speed = 320,
                Damage = 5,
                Lifetime = 5
            }
        });

        Assert.True(registry.TryGetById("WOODEN_ARROW", out _));
    }
}
