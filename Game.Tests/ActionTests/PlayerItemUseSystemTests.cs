using Game.Core.Actions;
using Game.Core.Biomes;
using Game.Core.Combat;
using Game.Core.Crafting;
using Game.Core.Data;
using Game.Core.Entities;
using Game.Core.Inventory;
using Game.Core.Items;
using Game.Core.Loot;
using Game.Core.Physics;
using Game.Core.Projectiles;
using Game.Core.Spawning;
using Game.Core.Tiles;
using Game.Core.World;
using System.Numerics;
using Xunit;

namespace Game.Tests.ActionTests;

public sealed class PlayerItemUseSystemTests
{
    [Fact]
    public void UseSelectedItem_PlaceableTileBuildsAndConsumesHotbarItem()
    {
        var content = CreateContent();
        var world = new World(16, 16, WorldMetadata.CreateDefault(seed: 1));
        var player = new PlayerEntity(new Vector2(32, 32), new TileCollisionResolver());
        var inventory = new PlayerInventory(content.Items);
        inventory.Hotbar.Slots[0].SetStack(new ItemStack("dirt_block", 2));

        var result = new PlayerItemUseSystem().UseSelectedItem(
            world,
            content,
            player,
            inventory,
            new EntityManager(),
            new TilePos(3, 3),
            new Vector2(48, 48),
            0.1f);

        Assert.Equal(PlayerItemUseKind.Build, result.Kind);
        Assert.True(result.PlacedTile);
        Assert.Equal(KnownTileIds.Dirt, world.GetTile(3, 3).TileId);
        Assert.Equal(1, inventory.CountItem("dirt_block"));
    }

    [Fact]
    public void UseSelectedItem_PickaxeMinesAndCreatesDroppedItem()
    {
        var content = CreateContent();
        var world = new World(16, 16, WorldMetadata.CreateDefault(seed: 1));
        world.SetTile(3, 3, KnownTileIds.Dirt);
        var player = new PlayerEntity(new Vector2(32, 32), new TileCollisionResolver());
        var inventory = new PlayerInventory(content.Items);
        inventory.Hotbar.Slots[0].SetStack(new ItemStack("copper_pickaxe", 1));
        var entities = new EntityManager(spatialCellSize: 16);

        var result = new PlayerItemUseSystem().UseSelectedItem(
            world,
            content,
            player,
            inventory,
            entities,
            new TilePos(3, 3),
            new Vector2(48, 48),
            2f);

        Assert.Equal(PlayerItemUseKind.Mine, result.Kind);
        Assert.True(world.GetTile(3, 3).IsAir);
        var drop = Assert.IsType<DroppedItemEntity>(Assert.Single(entities.Entities));
        Assert.Equal(new ItemStack("dirt_block", 1), drop.Stack);
    }

    [Fact]
    public void UseSelectedItem_MeleeWeaponDamagesEnemy()
    {
        var content = CreateContent();
        var world = new World(16, 16, WorldMetadata.CreateDefault(seed: 1));
        var player = new PlayerEntity(Vector2.Zero, new TileCollisionResolver());
        var inventory = new PlayerInventory(content.Items);
        inventory.Hotbar.Slots[0].SetStack(new ItemStack("copper_sword", 1));
        var entities = new EntityManager(spatialCellSize: 16);
        var enemy = new EntityFactory(new TileCollisionResolver()).CreateEnemy(CreateSlimeDefinition(), new Vector2(18, 0), currentHealth: 20);
        entities.Add(enemy);

        var result = new PlayerItemUseSystem().UseSelectedItem(
            world,
            content,
            player,
            inventory,
            entities,
            TilePos.Zero,
            new Vector2(64, 0),
            0.1f);

        Assert.Equal(PlayerItemUseKind.Melee, result.Kind);
        Assert.Equal(1, result.Melee.Hits);
        Assert.Equal(14, enemy.Health.Current);
    }

    [Fact]
    public void UseSelectedItem_RangedWeaponSpawnsProjectileAndConsumesAmmo()
    {
        var content = CreateContent();
        var world = new World(16, 16, WorldMetadata.CreateDefault(seed: 1));
        var player = new PlayerEntity(Vector2.Zero, new TileCollisionResolver());
        var inventory = new PlayerInventory(content.Items);
        inventory.Hotbar.Slots[0].SetStack(new ItemStack("wooden_bow", 1));
        inventory.Hotbar.Slots[1].SetStack(new ItemStack("wooden_arrow", 3));
        var entities = new EntityManager(spatialCellSize: 16);

        var result = new PlayerItemUseSystem().UseSelectedItem(
            world,
            content,
            player,
            inventory,
            entities,
            TilePos.Zero,
            player.Body.Center + new Vector2(80, 0),
            0.1f);

        Assert.Equal(PlayerItemUseKind.Shoot, result.Kind);
        var projectile = Assert.IsType<ProjectileEntity>(Assert.Single(entities.Entities));
        Assert.Same(projectile, result.Projectile);
        Assert.Equal("wooden_arrow", projectile.ProjectileId);
        Assert.Equal(2, inventory.CountItem("wooden_arrow"));
        Assert.True(projectile.Velocity.X > 0);
    }

    [Fact]
    public void UseSelectedItem_RangedWeaponRespectsUseCooldown()
    {
        var content = CreateContent();
        var world = new World(16, 16, WorldMetadata.CreateDefault(seed: 1));
        var player = new PlayerEntity(Vector2.Zero, new TileCollisionResolver());
        var inventory = new PlayerInventory(content.Items);
        inventory.Hotbar.Slots[0].SetStack(new ItemStack("wooden_bow", 1));
        inventory.Hotbar.Slots[1].SetStack(new ItemStack("wooden_arrow", 3));
        var entities = new EntityManager(spatialCellSize: 16);
        var use = new PlayerItemUseSystem();

        var first = use.UseSelectedItem(world, content, player, inventory, entities, TilePos.Zero, player.Body.Center + new Vector2(80, 0), 0.1f);
        var blocked = use.UseSelectedItem(world, content, player, inventory, entities, TilePos.Zero, player.Body.Center + new Vector2(80, 0), 0.1f);
        use.Update(0.5f);
        var second = use.UseSelectedItem(world, content, player, inventory, entities, TilePos.Zero, player.Body.Center + new Vector2(80, 0), 0.1f);

        Assert.Equal(PlayerItemUseKind.Shoot, first.Kind);
        Assert.Equal(PlayerItemUseKind.None, blocked.Kind);
        Assert.Equal(PlayerItemUseKind.Shoot, second.Kind);
        Assert.Equal(2, entities.Entities.Count);
        Assert.Equal(1, inventory.CountItem("wooden_arrow"));
    }

    private static GameContentDatabase CreateContent()
    {
        return new GameContentDatabase(
            TileRegistry.Create(new[]
            {
                new TileDefinition
                {
                    NumericId = KnownTileIds.Dirt,
                    Id = "dirt",
                    DisplayName = "Dirt",
                    TexturePath = "tiles/dirt",
                    Solid = true,
                    BlocksLight = true,
                    Hardness = 1,
                    DropItemId = "dirt_block"
                }
            }),
            ItemRegistry.Create(new[]
            {
                new ItemDefinition
                {
                    Id = "dirt_block",
                    DisplayName = "Dirt Block",
                    Type = ItemType.PlaceableTile,
                    TexturePath = "items/dirt_block",
                    MaxStack = 999,
                    PlacesTileId = "dirt"
                },
                new ItemDefinition
                {
                    Id = "copper_pickaxe",
                    DisplayName = "Copper Pickaxe",
                    Type = ItemType.ToolPickaxe,
                    TexturePath = "items/copper_pickaxe",
                    MaxStack = 1,
                    UseTime = 0.35f,
                    ToolPower = 35,
                    Damage = 3,
                    Knockback = 10
                },
                new ItemDefinition
                {
                    Id = "copper_sword",
                    DisplayName = "Copper Sword",
                    Type = ItemType.WeaponMelee,
                    TexturePath = "items/copper_sword",
                    MaxStack = 1,
                    UseTime = 0.25f,
                    Damage = 6,
                    Knockback = 30
                },
                new ItemDefinition
                {
                    Id = "wooden_bow",
                    DisplayName = "Wooden Bow",
                    Type = ItemType.WeaponRanged,
                    TexturePath = "items/wooden_bow",
                    MaxStack = 1,
                    UseTime = 0.45f,
                    Damage = 4,
                    Knockback = 10,
                    Actions = new[]
                    {
                        new ItemActionDefinition
                        {
                            Kind = ItemActionKind.Shoot,
                            ProjectileId = "wooden_arrow",
                            AmmoItemId = "wooden_arrow",
                            AmmoCost = 1
                        }
                    }
                },
                new ItemDefinition
                {
                    Id = "wooden_arrow",
                    DisplayName = "Wooden Arrow",
                    Type = ItemType.Ammo,
                    TexturePath = "items/wooden_arrow",
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
            MaxHealth = 20
        };
    }
}
