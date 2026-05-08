using Game.Core.Combat;
using Game.Core.Entities;
using Game.Core.Physics;
using Game.Core.World;
using System.Numerics;
using Xunit;

namespace Game.Tests.DataTests;

public sealed class EntityFactoryTests
{
    [Fact]
    public void CreateEnemy_UsesDefinitionRuntimeData()
    {
        var definition = new EntityDefinition
        {
            Id = "slime",
            DisplayName = "Slime",
            TexturePath = "entities/slime",
            MaxHealth = 20,
            Width = 16,
            Height = 14,
            AiBehavior = "slime",
            LootTableId = "slime_basic"
        };

        var enemy = new EntityFactory(new TileCollisionResolver()).CreateEnemy(definition, new Vector2(10, 20));

        Assert.Equal("slime", enemy.DefinitionId);
        Assert.Equal("slime_basic", enemy.LootTableId);
        Assert.Equal(20, enemy.Health.Current);
        Assert.Equal(16, enemy.Body.Size.X);
    }

    [Fact]
    public void EnemyEntity_BecomesInactiveWhenDead()
    {
        var enemy = new EntityFactory(new TileCollisionResolver()).CreateEnemy(new EntityDefinition
        {
            Id = "slime",
            DisplayName = "Slime",
            TexturePath = "entities/slime",
            MaxHealth = 10
        }, Vector2.Zero);

        enemy.ApplyDamage(new DamageInfo(10, DamageType.Melee, null, Vector2.Zero, 0));
        enemy.Update(new World(16, 16, WorldMetadata.CreateDefault(seed: 1)), 0.016f);

        Assert.False(enemy.IsActive);
    }
}
