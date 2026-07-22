using Game.Core.Actions;
using Game.Core.Biomes;
using Game.Core.Combat;
using Game.Core.Crafting;
using Game.Core.Data;
using Game.Core.Effects;
using Game.Core.Entities;
using Game.Core.Events;
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

public sealed class PlayerItemUseFeedbackTests
{
    [Fact]
    public void UseSelectedItem_MiningReturnsProgressBeforeCompletion()
    {
        var content = CreateContent();
        var world = CreateWorld();
        world.SetTile(2, 2, KnownTileIds.Dirt);
        var player = CreatePlayer();
        var inventory = new PlayerInventory(content.Items);
        inventory.Hotbar.Slots[0].SetStack(new ItemStack("pickaxe", 1));

        var result = new PlayerItemUseSystem().UseSelectedItem(
            world,
            content,
            player,
            inventory,
            new EntityManager(),
            new TilePos(2, 2),
            new Vector2(40, 40),
            0.2f);

        Assert.True(result.InProgress);
        Assert.Equal(PlayerItemUseKind.None, result.Kind);
        Assert.Equal(PlayerItemUseKind.Mine, result.AttemptedKind);
        Assert.Equal(GameplayActionSuccessReason.ActionStarted, result.SuccessReason);
        Assert.Equal(0.704f, result.ActionProgress, precision: 3);
        Assert.Equal(result.ActionProgress, result.Mining.Progress);
    }

    [Fact]
    public void UseSelectedItem_CooldownReturnsTelemetryAndPublishesSingleBlockedEvent()
    {
        var content = CreateContent();
        var world = CreateWorld();
        var player = CreatePlayer();
        var inventory = new PlayerInventory(content.Items);
        inventory.Hotbar.Slots[0].SetStack(new ItemStack("bow", 1));
        inventory.Hotbar.Slots[1].SetStack(new ItemStack("arrow", 3));
        var entities = new EntityManager();
        var events = new GameEventBus();
        var completedEvents = 0;
        var blockedEvents = 0;
        events.Subscribe<PlayerItemUseCompletedEvent>(_ => completedEvents++);
        events.Subscribe<PlayerItemUseBlockedEvent>(_ => blockedEvents++);
        var system = new PlayerItemUseSystem();
        var target = player.Body.Center + Vector2.UnitX * 80;

        var first = system.UseSelectedItem(world, content, player, inventory, entities, TilePos.Zero, target, 0.1f, events);
        var blocked = system.UseSelectedItem(world, content, player, inventory, entities, TilePos.Zero, target, 0.1f, events);
        var repeated = system.UseSelectedItem(world, content, player, inventory, entities, TilePos.Zero, target, 0.1f, events);

        Assert.True(first.Success);
        Assert.Equal(PlayerItemUseStatus.Succeeded, first.Status);
        Assert.Equal(0.4f, first.CooldownRemaining, precision: 3);
        Assert.Equal(0f, first.CooldownProgress, precision: 3);
        Assert.True(blocked.Blocked);
        Assert.Equal(PlayerItemUseKind.None, blocked.Kind);
        Assert.Equal(PlayerItemUseKind.Shoot, blocked.AttemptedKind);
        Assert.Equal(GameplayActionFailureReason.Cooldown, blocked.FailureReason);
        Assert.Equal(0.4f, blocked.CooldownRemaining, precision: 3);
        Assert.Equal(blocked, repeated);
        Assert.Equal(1, completedEvents);
        Assert.Equal(1, blockedEvents);
    }

    [Fact]
    public void UseSelectedItem_RangedAndMagicFailuresAreExplicitAndDoNotSpendResources()
    {
        var content = CreateContent();
        var world = CreateWorld();
        var player = CreatePlayer(currentMana: 1);
        var inventory = new PlayerInventory(content.Items);
        inventory.Hotbar.Slots[0].SetStack(new ItemStack("bow", 1));
        inventory.Hotbar.Slots[1].SetStack(new ItemStack("wand", 1));
        var entities = new EntityManager();
        var target = player.Body.Center + Vector2.UnitX * 80;

        var noAmmo = new PlayerItemUseSystem().UseSelectedItem(
            world,
            content,
            player,
            inventory,
            entities,
            TilePos.Zero,
            target,
            0.1f);

        inventory.SelectHotbarSlot(1);
        var noMana = new PlayerItemUseSystem().UseSelectedItem(
            world,
            content,
            player,
            inventory,
            entities,
            TilePos.Zero,
            target,
            0.1f);

        Assert.Equal(GameplayActionFailureReason.InsufficientAmmo, noAmmo.FailureReason);
        Assert.Equal(PlayerItemUseKind.Shoot, noAmmo.AttemptedKind);
        Assert.Equal(GameplayActionFailureReason.InsufficientMana, noMana.FailureReason);
        Assert.Equal(PlayerItemUseKind.Cast, noMana.AttemptedKind);
        Assert.Equal(1, player.Mana);
        Assert.Empty(entities.Entities);
    }

    [Fact]
    public void UseSelectedItem_InvalidProjectileTargetDoesNotConsumeAmmo()
    {
        var content = CreateContent();
        var world = CreateWorld();
        var player = CreatePlayer();
        var inventory = new PlayerInventory(content.Items);
        inventory.Hotbar.Slots[0].SetStack(new ItemStack("bow", 1));
        inventory.Hotbar.Slots[1].SetStack(new ItemStack("arrow", 1));

        var result = new PlayerItemUseSystem().UseSelectedItem(
            world,
            content,
            player,
            inventory,
            new EntityManager(),
            TilePos.Zero,
            player.Body.Center,
            0.1f);

        Assert.Equal(GameplayActionFailureReason.InvalidTarget, result.FailureReason);
        Assert.Equal(1, inventory.CountItem("arrow"));
    }

    [Fact]
    public void UseSelectedItem_ConsumableAppliesOnlyRealBenefitsAndPublishesTypedFeedback()
    {
        var content = CreateContent();
        var world = CreateWorld();
        var player = CreatePlayer(currentHealth: 70, currentMana: 10);
        var inventory = new PlayerInventory(content.Items);
        inventory.Hotbar.Slots[0].SetStack(new ItemStack("restoration_tonic", 2));
        var events = new GameEventBus();
        ResourceRestoredEvent? restoredEvent = null;
        StatusEffectAppliedEvent? effectEvent = null;
        PlayerItemUseCompletedEvent? completedEvent = null;
        events.Subscribe<ResourceRestoredEvent>(gameEvent => restoredEvent = gameEvent);
        events.Subscribe<StatusEffectAppliedEvent>(gameEvent => effectEvent = gameEvent);
        events.Subscribe<PlayerItemUseCompletedEvent>(gameEvent => completedEvent = gameEvent);
        var system = new PlayerItemUseSystem(statusEffects: new StatusEffectApplier(new Random(1)));

        var used = system.UseSelectedItem(
            world,
            content,
            player,
            inventory,
            new EntityManager(),
            TilePos.Zero,
            player.Body.Center + Vector2.UnitX,
            0.1f,
            events);

        Assert.True(used.Success);
        Assert.Equal(GameplayActionSuccessReason.ConsumableApplied, used.SuccessReason);
        Assert.True(used.ConsumedItem);
        Assert.Equal(25, used.HealthRestored);
        Assert.Equal(10, used.ManaRestored);
        Assert.Equal(1, used.StatusEffectsApplied);
        Assert.Equal(95, player.Health);
        Assert.Equal(20, player.Mana);
        Assert.True(player.StatusEffects.HasEffect("fortified"));
        Assert.Equal(1, inventory.CountItem("restoration_tonic"));
        Assert.NotNull(restoredEvent);
        Assert.NotNull(effectEvent);
        Assert.NotNull(completedEvent);
        Assert.Equal(StatusEffectSourceKind.Item, effectEvent.SourceKind);

        player.HealthComponent.RestoreFull();
        player.ManaComponent.RestoreFull();
        system.Update(1f);
        var noBenefit = system.UseSelectedItem(
            world,
            content,
            player,
            inventory,
            new EntityManager(),
            TilePos.Zero,
            player.Body.Center + Vector2.UnitX,
            0.1f,
            events);

        Assert.True(noBenefit.Blocked);
        Assert.Equal(GameplayActionFailureReason.NoBenefit, noBenefit.FailureReason);
        Assert.Equal(1, inventory.CountItem("restoration_tonic"));
    }

    [Fact]
    public void ItemDefinitionJsonLoader_LoadsConsumableRestoresAndStatusEffectApplications()
    {
        var item = new ItemDefinitionJsonLoader().LoadDefinitionFromJson("""
        {
          "id": "restoration_tonic",
          "displayName": "Restoration Tonic",
          "type": "Consumable",
          "texturePath": "items/restoration_tonic",
          "maxStack": 10,
          "healthRestore": 30,
          "manaRestore": 12,
          "statusEffectApplications": [
            { "effectId": "fortified", "chance": 0.75, "durationSeconds": 8 }
          ]
        }
        """);

        Assert.Equal(30, item.HealthRestore);
        Assert.Equal(12, item.ManaRestore);
        var effect = Assert.Single(item.StatusEffectApplications);
        Assert.Equal("fortified", effect.EffectId);
        Assert.Equal(0.75f, effect.Chance);
        Assert.Equal(8f, effect.DurationSeconds);
    }

    private static World CreateWorld()
    {
        return new World(8, 8, WorldMetadata.CreateDefault(seed: 1));
    }

    private static PlayerEntity CreatePlayer(int? currentHealth = null, int? currentMana = null)
    {
        return new PlayerEntity(
            Vector2.Zero,
            new TileCollisionResolver(),
            maxHealth: 100,
            currentHealth: currentHealth,
            maxMana: 20,
            currentMana: currentMana);
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
                    Hardness = 2,
                    DropItemId = "dirt_block"
                }
            }),
            ItemRegistry.Create(new[]
            {
                new ItemDefinition
                {
                    Id = "pickaxe",
                    DisplayName = "Pickaxe",
                    Type = ItemType.ToolPickaxe,
                    TexturePath = "items/pickaxe",
                    ToolPower = 10
                },
                new ItemDefinition
                {
                    Id = "bow",
                    DisplayName = "Bow",
                    Type = ItemType.WeaponRanged,
                    TexturePath = "items/bow",
                    UseTime = 0.4f,
                    Damage = 2,
                    Actions = new[]
                    {
                        new ItemActionDefinition
                        {
                            Kind = ItemActionKind.Shoot,
                            ProjectileId = "arrow",
                            AmmoItemId = "arrow"
                        }
                    }
                },
                new ItemDefinition
                {
                    Id = "arrow",
                    DisplayName = "Arrow",
                    Type = ItemType.Ammo,
                    TexturePath = "items/arrow",
                    MaxStack = 99
                },
                new ItemDefinition
                {
                    Id = "wand",
                    DisplayName = "Wand",
                    Type = ItemType.WeaponMagic,
                    TexturePath = "items/wand",
                    ManaCost = 5,
                    Actions = new[]
                    {
                        new ItemActionDefinition
                        {
                            Kind = ItemActionKind.Cast,
                            ProjectileId = "spark"
                        }
                    }
                },
                new ItemDefinition
                {
                    Id = "restoration_tonic",
                    DisplayName = "Restoration Tonic",
                    Type = ItemType.Consumable,
                    TexturePath = "items/restoration_tonic",
                    MaxStack = 10,
                    UseTime = 0.2f,
                    HealthRestore = 25,
                    ManaRestore = 15,
                    StatusEffectApplications = new[]
                    {
                        new StatusEffectApplication { EffectId = "fortified" }
                    }
                }
            }),
            RecipeRegistry.Create(Array.Empty<RecipeDefinition>()),
            LootTableRegistry.Create(Array.Empty<LootTableDefinition>()),
            BiomeRegistry.Create(Array.Empty<BiomeDefinition>()),
            ProjectileRegistry.Create(new[]
            {
                new ProjectileDefinition
                {
                    Id = "arrow",
                    TexturePath = "projectiles/arrow",
                    Speed = 200,
                    Damage = 3,
                    Lifetime = 2
                },
                new ProjectileDefinition
                {
                    Id = "spark",
                    TexturePath = "projectiles/spark",
                    Speed = 180,
                    Damage = 4,
                    Lifetime = 2
                }
            }),
            EntityDefinitionRegistry.Create(Array.Empty<EntityDefinition>()),
            SpawnRuleRegistry.Create(Array.Empty<SpawnRuleDefinition>()))
        {
            StatusEffects = StatusEffectRegistry.Create(new[]
            {
                new StatusEffectDefinition
                {
                    Id = "fortified",
                    DisplayName = "Fortified",
                    Kind = StatusEffectKind.Buff,
                    DurationSeconds = 5,
                    DefenseDelta = 2
                }
            })
        };
    }
}
