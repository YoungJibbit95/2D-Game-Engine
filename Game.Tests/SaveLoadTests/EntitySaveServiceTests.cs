using Game.Core.Biomes;
using Game.Core.Crafting;
using Game.Core.Data;
using Game.Core.Entities;
using Game.Core.Inventory;
using Game.Core.Items;
using Game.Core.Loot;
using Game.Core.Physics;
using Game.Core.Projectiles;
using Game.Core.Saving;
using Game.Core.Spawning;
using Game.Core.Tiles;
using System.Numerics;
using Xunit;

namespace Game.Tests.SaveLoadTests;

public sealed class EntitySaveServiceTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), "terraria-like-entity-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void SaveAndLoad_RoundTripsEnemyProjectileAndDroppedItemRuntimeData()
    {
        var path = Path.Combine(_tempDirectory, "entities.json");
        var manager = new EntityManager();
        var enemy = new EntityFactory(new TileCollisionResolver()).CreateEnemy(CreateSlimeDefinition(), new Vector2(10, 20), currentHealth: 8);
        enemy.Body.Velocity = new Vector2(1, 2);
        var projectile = new ProjectileEntity("wooden_arrow", new Vector2(30, 40), new Vector2(50, 0), 5, 0.2f, 1, 5f, ownerEntityId: 99, age: 1.25f);
        var drop = new DroppedItemEntity(new ItemStack("gel", 3), new Vector2(60, 70), new TileCollisionResolver());
        drop.Body.Velocity = new Vector2(-3, 4);
        manager.Add(enemy);
        manager.Add(projectile);
        manager.Add(drop);

        var service = new EntitySaveService();
        service.Save(manager.Entities, path);

        var loaded = service.Load(path, CreateContent());

        Assert.Equal(3, loaded.Count);
        var loadedEnemy = Assert.IsType<EnemyEntity>(loaded[0]);
        Assert.Equal(enemy.Id, loadedEnemy.Id);
        Assert.Equal(8, loadedEnemy.Health.Current);
        Assert.Equal(new Vector2(1, 2), loadedEnemy.Body.Velocity);

        var loadedProjectile = Assert.IsType<ProjectileEntity>(loaded[1]);
        Assert.Equal(projectile.Id, loadedProjectile.Id);
        Assert.Equal("wooden_arrow", loadedProjectile.ProjectileId);
        Assert.Equal(1.25f, loadedProjectile.Age);

        var loadedDrop = Assert.IsType<DroppedItemEntity>(loaded[2]);
        Assert.Equal(drop.Id, loadedDrop.Id);
        Assert.Equal(new ItemStack("gel", 3), loadedDrop.Stack);
        Assert.Equal(new Vector2(-3, 4), loadedDrop.Body.Velocity);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    private static GameContentDatabase CreateContent()
    {
        return new GameContentDatabase(
            TileRegistry.Create(Array.Empty<TileDefinition>()),
            ItemRegistry.Create(new[]
            {
                new ItemDefinition
                {
                    Id = "gel",
                    DisplayName = "Gel",
                    Type = ItemType.Material,
                    TexturePath = "items/gel",
                    MaxStack = 999
                }
            }),
            RecipeRegistry.Create(Array.Empty<RecipeDefinition>()),
            LootTableRegistry.Create(Array.Empty<LootTableDefinition>()),
            BiomeRegistry.Create(Array.Empty<BiomeDefinition>()),
            ProjectileRegistry.Create(new[]
            {
                new ProjectileDefinition
                {
                    Id = "wooden_arrow",
                    TexturePath = "projectiles/wooden_arrow",
                    Speed = 320,
                    Damage = 5,
                    Gravity = 0.2f,
                    Pierce = 0,
                    Lifetime = 5
                }
            }),
            EntityDefinitionRegistry.Create(new[] { CreateSlimeDefinition() }),
            SpawnRuleRegistry.Create(Array.Empty<SpawnRuleDefinition>()));
    }

    private static EntityDefinition CreateSlimeDefinition()
    {
        return new EntityDefinition
        {
            Id = "slime",
            DisplayName = "Slime",
            TexturePath = "entities/slime",
            MaxHealth = 20,
            AiBehavior = "slime",
            LootTableId = "slime_basic"
        };
    }
}
