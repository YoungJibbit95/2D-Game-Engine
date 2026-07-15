using Game.Core.Data;
using Game.Core.Projectiles;
using Xunit;

namespace Game.Tests.ProjectileTests;

public sealed class AdvancedProjectileRegistryTests
{
    [Fact]
    public void Loader_ReadsAdvancedRuntimeAndCollisionPolicy()
    {
        const string json = """
        {
          "id": "seeking_orb",
          "texture": "projectiles/seeking_orb",
          "speed": 180,
          "damage": 12,
          "dragPerSecond": 0.2,
          "homingTurnRateRadiansPerSecond": 3.14,
          "homingRange": 240,
          "pierce": 2,
          "bounceCount": 3,
          "bounceRestitution": 0.75,
          "collisionRadius": 5,
          "tileCollisionBehavior": "Bounce",
          "entityCollisionBehavior": "Damage",
          "friendlyFire": true,
          "hitOncePerTarget": true,
          "knockback": 45,
          "criticalChance": 0.25,
          "criticalMultiplier": 1.75,
          "lifetime": 6
        }
        """;

        var definition = new ProjectileDefinitionJsonLoader().LoadDefinitionFromJson(json);
        _ = ProjectileRegistry.Create(new[] { definition });

        Assert.Equal(0.2f, definition.DragPerSecond);
        Assert.Equal(3.14f, definition.HomingTurnRateRadiansPerSecond);
        Assert.Equal(240, definition.HomingRange);
        Assert.Equal(3, definition.BounceCount);
        Assert.Equal(ProjectileTileCollisionBehavior.Bounce, definition.TileCollisionBehavior);
        Assert.True(definition.FriendlyFire);
        Assert.Equal(1.75f, definition.CriticalMultiplier);
    }

    [Fact]
    public void Registry_RejectsInvalidBounceRestitution()
    {
        var definition = new ProjectileDefinition
        {
            Id = "bad-bounce",
            TexturePath = "projectiles/bad",
            Speed = 100,
            Damage = 5,
            BounceRestitution = 1.1f,
            Lifetime = 2
        };

        Assert.Throws<RegistryValidationException>(() => ProjectileRegistry.Create(new[] { definition }));
    }
}
