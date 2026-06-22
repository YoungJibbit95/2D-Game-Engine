using Game.Core.Actions;
using Game.Core.Biomes;
using Game.Core.Combat;
using Game.Core.Crafting;
using Game.Core.Data;
using Game.Core.Entities;
using Game.Core.Farming;
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

    [Fact]
    public void UseSelectedItem_MagicWeaponSpawnsMagicProjectileAndConsumesMana()
    {
        var content = CreateContent();
        var world = new World(16, 16, WorldMetadata.CreateDefault(seed: 1));
        var player = new PlayerEntity(Vector2.Zero, new TileCollisionResolver(), maxMana: 20, currentMana: 12);
        var inventory = new PlayerInventory(content.Items);
        inventory.Hotbar.Slots[0].SetStack(new ItemStack("spark_wand", 1));
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

        Assert.Equal(PlayerItemUseKind.Cast, result.Kind);
        Assert.Equal(6, player.Mana);
        var projectile = Assert.IsType<ProjectileEntity>(Assert.Single(entities.Entities));
        Assert.Equal(DamageType.Magic, projectile.DamageType);
        Assert.Equal(7, projectile.Damage);
    }

    [Fact]
    public void UseSelectedItem_FarmingActionsTillWaterAndPlant()
    {
        var content = CreateContent();
        var world = new World(16, 16, WorldMetadata.CreateDefault(seed: 1));
        world.SetTile(4, 4, KnownTileIds.Dirt);
        var player = new PlayerEntity(new Vector2(64, 64), new TileCollisionResolver());
        var inventory = new PlayerInventory(content.Items);
        var plots = new FarmPlotManager();
        inventory.Hotbar.Slots[0].SetStack(new ItemStack("copper_hoe", 1));
        inventory.Hotbar.Slots[1].SetStack(new ItemStack("watering_can", 1));
        inventory.Hotbar.Slots[2].SetStack(new ItemStack("parsnip_seeds", 2));
        var use = new PlayerItemUseSystem();

        var till = use.UseSelectedItem(world, content, player, inventory, new EntityManager(), new TilePos(4, 4), new Vector2(72, 72), 0.1f, farmPlots: plots);
        use.Update(1f);
        inventory.SelectHotbarSlot(1);
        var water = use.UseSelectedItem(world, content, player, inventory, new EntityManager(), new TilePos(4, 4), new Vector2(72, 72), 0.1f, farmPlots: plots);
        use.Update(1f);
        inventory.SelectHotbarSlot(2);
        var plant = use.UseSelectedItem(world, content, player, inventory, new EntityManager(), new TilePos(4, 4), new Vector2(72, 72), 0.1f, farmPlots: plots, farmSeason: FarmSeason.Spring, currentDay: 3);

        Assert.Equal(PlayerItemUseKind.Till, till.Kind);
        Assert.Equal(PlayerItemUseKind.Water, water.Kind);
        Assert.Equal(PlayerItemUseKind.Plant, plant.Kind);
        Assert.True(plots.TryGetPlot(new TilePos(4, 4), out var plot));
        Assert.True(plot.IsTilled);
        Assert.True(plot.IsWatered);
        Assert.NotNull(plot.Crop);
        Assert.Equal("parsnip", plot.Crop!.CropId);
        Assert.Equal(1, inventory.CountItem("parsnip_seeds"));
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
                    DropItemId = "dirt_block",
                    MergeGroup = "soil",
                    Tags = new[] { "soil", "farmable" }
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
                },
                new ItemDefinition
                {
                    Id = "spark_wand",
                    DisplayName = "Spark Wand",
                    Type = ItemType.WeaponMagic,
                    TexturePath = "items/spark_wand",
                    MaxStack = 1,
                    UseTime = 0.35f,
                    Damage = 3,
                    ManaCost = 6,
                    Actions = new[]
                    {
                        new ItemActionDefinition
                        {
                            Kind = ItemActionKind.Cast,
                            ProjectileId = "spark_bolt"
                        }
                    }
                },
                new ItemDefinition
                {
                    Id = "copper_hoe",
                    DisplayName = "Copper Hoe",
                    Type = ItemType.ToolHoe,
                    TexturePath = "items/copper_hoe",
                    MaxStack = 1,
                    UseTime = 0.25f
                },
                new ItemDefinition
                {
                    Id = "watering_can",
                    DisplayName = "Watering Can",
                    Type = ItemType.ToolWateringCan,
                    TexturePath = "items/watering_can",
                    MaxStack = 1,
                    UseTime = 0.25f
                },
                new ItemDefinition
                {
                    Id = "parsnip_seeds",
                    DisplayName = "Parsnip Seeds",
                    Type = ItemType.Seed,
                    TexturePath = "items/parsnip_seeds",
                    MaxStack = 99,
                    UseTime = 0.1f
                },
                new ItemDefinition
                {
                    Id = "parsnip",
                    DisplayName = "Parsnip",
                    Type = ItemType.Consumable,
                    TexturePath = "items/parsnip",
                    MaxStack = 99
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
                },
                new ProjectileDefinition
                {
                    Id = "spark_bolt",
                    TexturePath = "projectiles/spark_bolt",
                    Speed = 250,
                    Damage = 4,
                    DamageType = DamageType.Magic,
                    Gravity = 0,
                    Pierce = 1,
                    Lifetime = 3
                }
            }),
            EntityDefinitionRegistry.Create(new[] { CreateSlimeDefinition() }),
            SpawnRuleRegistry.Create(Array.Empty<SpawnRuleDefinition>()))
        {
            Crops = CropRegistry.Create(new[]
            {
                new CropDefinition
                {
                    Id = "parsnip",
                    DisplayName = "Parsnip",
                    TexturePath = "crops/parsnip",
                    SeedItemId = "parsnip_seeds",
                    HarvestItemId = "parsnip",
                    GrowthStageDays = new[] { 1, 1 },
                    Seasons = new[] { FarmSeason.Spring }
                }
            })
        };
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
