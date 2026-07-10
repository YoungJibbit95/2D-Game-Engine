using Game.Core.Characters;
using Game.Core.Effects;
using Game.Core.Entities;
using Game.Core.Equipment;
using Game.Core.Inventory;
using Game.Core.Items;
using Game.Core.Physics;
using Game.Core.Saving;
using System.Numerics;
using Xunit;

namespace Game.Tests.PlayerTests;

public sealed class PlayerSaveServiceTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), "terraria-like-player-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void SaveAndLoad_RoundTripsPlayerData()
    {
        var path = Path.Combine(_tempDirectory, "player_001.json");
        var data = new PlayerSaveData
        {
            PlayerId = "player_001",
            DisplayName = "Builder",
            PositionX = 100,
            PositionY = 200,
            Health = 75,
            MaxHealth = 100,
            Mana = 20,
            SelectedHotbarSlot = 3,
            InventorySlots = new[]
            {
                new ItemStack("dirt_block", 12),
                ItemStack.Empty,
                new ItemStack("copper_pickaxe", 1)
            }
        };

        var service = new PlayerSaveService();
        service.Save(data, path);

        var loaded = service.Load(path);

        Assert.Equal(data.PlayerId, loaded.PlayerId);
        Assert.Equal(data.DisplayName, loaded.DisplayName);
        Assert.Equal(data.PositionX, loaded.PositionX);
        Assert.Equal(data.InventorySlots[0], loaded.InventorySlots[0]);
        Assert.Equal(data.SelectedHotbarSlot, loaded.SelectedHotbarSlot);
    }

    [Fact]
    public void ToInventory_RecreatesSlotContents()
    {
        var data = new PlayerSaveData
        {
            PlayerId = "player_001",
            DisplayName = "Builder",
            PositionX = 0,
            PositionY = 0,
            Health = 100,
            MaxHealth = 100,
            SelectedHotbarSlot = 0,
            InventorySlots = new[]
            {
                new ItemStack("dirt_block", 12),
                ItemStack.Empty
            }
        };

        var inventory = new PlayerSaveService().ToInventory(data, CreateItems());

        Assert.Equal(new ItemStack("dirt_block", 12), inventory.Slots[0].Stack);
        Assert.True(inventory.Slots[1].IsEmpty);
    }

    [Fact]
    public void ToPlayerInventory_RehydratesHotbarAndMainInventory()
    {
        var slots = Enumerable.Repeat(ItemStack.Empty, PlayerInventory.HotbarSlotCount + PlayerInventory.MainSlotCount).ToArray();
        slots[2] = new ItemStack("dirt_block", 8);
        slots[PlayerInventory.HotbarSlotCount] = new ItemStack("stone_block", 4);
        var data = new PlayerSaveData
        {
            PlayerId = "player_001",
            DisplayName = "Builder",
            PositionX = 0,
            PositionY = 0,
            Health = 100,
            MaxHealth = 100,
            SelectedHotbarSlot = 2,
            InventorySlots = slots
        };

        var inventory = new PlayerSaveService().ToPlayerInventory(data, CreateItems());

        Assert.Equal(2, inventory.SelectedHotbarSlot);
        Assert.Equal(new ItemStack("dirt_block", 8), inventory.Hotbar.Slots[2].Stack);
        Assert.Equal(new ItemStack("stone_block", 4), inventory.Main.Slots[0].Stack);
    }

    [Fact]
    public void SaveAndLoad_RoundTripsPersistentIdentityData()
    {
        var path = Path.Combine(_tempDirectory, "persistent_player.json");
        var appearance = new CharacterAppearance
        {
            BodySpriteId = "entities/player/mage",
            SkinTone = "#8c5f42",
            HairStyleId = "long",
            ClothesStyleId = "arcane_robe",
            AccessoryId = "round_glasses",
            HairColor = "#d8c06a",
            ShirtColor = "#345f9a",
            PantsColor = "#232a45",
            EyeColor = "#67c9b2"
        };
        var data = CreateMinimalData() with
        {
            EquipmentLoadout = new EquipmentLoadoutSaveData
            {
                Slots = new[]
                {
                    new EquipmentSlotSaveData { SlotId = "Head", ItemId = "copper_helmet" },
                    new EquipmentSlotSaveData { SlotId = "Accessory2", ItemId = "swift_charm" }
                }
            },
            ActiveStatusEffects = new[]
            {
                new ActiveStatusEffectSaveData
                {
                    EffectId = "well_fed",
                    RemainingDurationSeconds = 17.375f
                }
            },
            CharacterAppearance = appearance
        };

        var service = new PlayerSaveService();
        service.Save(data, path);
        var loaded = service.Load(path);

        Assert.Equal(PlayerSaveData.CurrentFormatVersion, loaded.FormatVersion);
        Assert.Equal(appearance, loaded.CharacterAppearance);
        Assert.Equal(2, loaded.EquipmentLoadout!.Slots.Count);
        Assert.Equal("copper_helmet", loaded.EquipmentLoadout.Slots[0].ItemId);
        var effect = Assert.Single(loaded.ActiveStatusEffects);
        Assert.Equal("well_fed", effect.EffectId);
        Assert.Equal(17.375f, effect.RemainingDurationSeconds);
    }

    [Fact]
    public void Load_VersionOneJsonWithoutIdentityFields_UsesBackwardCompatibleDefaults()
    {
        var path = Path.Combine(_tempDirectory, "legacy_player.json");
        Directory.CreateDirectory(_tempDirectory);
        File.WriteAllText(
            path,
            """
            {
              "FormatVersion": 1,
              "PlayerId": "legacy_player",
              "DisplayName": "Legacy",
              "PositionX": 12,
              "PositionY": 34,
              "Health": 80,
              "MaxHealth": 100,
              "Mana": 10,
              "SelectedHotbarSlot": 0,
              "InventorySlots": []
            }
            """);

        var service = new PlayerSaveService();
        var loaded = service.Load(path);

        Assert.Equal(1, loaded.FormatVersion);
        Assert.Null(loaded.EquipmentLoadout);
        Assert.Empty(loaded.ActiveStatusEffects);
        Assert.Null(loaded.CharacterAppearance);
        Assert.DoesNotContain(
            service.ToEquipmentLoadout(loaded, CreateItems()).Slots.Values,
            stack => !stack.IsEmpty);
        Assert.Equal(new CharacterAppearance(), service.ToCharacterAppearance(loaded));
    }

    [Fact]
    public void CreateSaveData_CapturesSlotsAppearanceAndExactRemainingEffectDuration()
    {
        var items = CreateItems();
        var equipment = new EquipmentLoadout();
        Assert.True(equipment.TryEquip(new ItemStack("copper_helmet", 1), items, EquipmentSlotType.Head).Success);
        Assert.True(equipment.TryEquip(new ItemStack("swift_charm", 1), items, EquipmentSlotType.Accessory3).Success);
        var appearance = new CharacterAppearance { HairStyleId = "braids", EyeColor = "#112233" };
        var player = new PlayerEntity(Vector2.Zero, new TileCollisionResolver());
        var effectDefinition = CreateEffects().GetById("well_fed");
        player.StatusEffects.Apply(effectDefinition, 12.75f);
        player.StatusEffects.Update(2.25f);

        var service = new PlayerSaveService();
        var data = service.CreateSaveData(
            player,
            new PlayerInventory(items),
            "persistent_player",
            "Persistent",
            equipmentLoadout: equipment,
            characterAppearance: appearance);

        Assert.Equal(PlayerSaveData.CurrentFormatVersion, data.FormatVersion);
        Assert.Equal(appearance, data.CharacterAppearance);
        Assert.Equal(2, data.EquipmentLoadout!.Slots.Count);
        Assert.Contains(data.EquipmentLoadout.Slots, slot =>
            slot.SlotId == nameof(EquipmentSlotType.Accessory3) && slot.ItemId == "swift_charm");
        Assert.Equal(10.5f, Assert.Single(data.ActiveStatusEffects).RemainingDurationSeconds);

        var path = Path.Combine(_tempDirectory, "runtime_fields.json");
        service.Save(data, path);
        var json = File.ReadAllText(path);
        Assert.DoesNotContain("TickAccumulator", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Definition\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void ToEquipmentLoadout_RestoresKnownSlotsAndReportsInvalidLegacyEntries()
    {
        var data = CreateMinimalData() with
        {
            EquipmentLoadout = new EquipmentLoadoutSaveData
            {
                Slots = new[]
                {
                    new EquipmentSlotSaveData { SlotId = "Head", ItemId = "copper_helmet" },
                    new EquipmentSlotSaveData { SlotId = "Accessory2", ItemId = "swift_charm" },
                    new EquipmentSlotSaveData { SlotId = "Accessory1", ItemId = "removed_charm" },
                    new EquipmentSlotSaveData { SlotId = "Body", ItemId = "dirt_block" },
                    new EquipmentSlotSaveData { SlotId = "Cape", ItemId = "copper_helmet" },
                    new EquipmentSlotSaveData { SlotId = "Accessory3", ItemId = "" },
                    new EquipmentSlotSaveData { SlotId = "Head", ItemId = "swift_charm" }
                }
            }
        };
        var warnings = new List<PlayerLoadWarning>();

        var loadout = new PlayerSaveService().ToEquipmentLoadout(data, CreateItems(), warnings);

        Assert.Equal(new ItemStack("copper_helmet", 1), loadout.GetStack(EquipmentSlotType.Head));
        Assert.Equal(new ItemStack("swift_charm", 1), loadout.GetStack(EquipmentSlotType.Accessory2));
        Assert.True(loadout.GetStack(EquipmentSlotType.Accessory1).IsEmpty);
        Assert.Contains(warnings, warning => warning.Kind == PlayerLoadWarningKind.UnknownEquipmentItem);
        Assert.Contains(warnings, warning => warning.Kind == PlayerLoadWarningKind.IncompatibleEquipmentItem);
        Assert.Contains(warnings, warning => warning.Kind == PlayerLoadWarningKind.InvalidEquipmentSlot);
        Assert.Contains(warnings, warning => warning.Kind == PlayerLoadWarningKind.MissingEquipmentItemId);
        Assert.Contains(warnings, warning => warning.Kind == PlayerLoadWarningKind.DuplicateEquipmentSlot);
    }

    [Fact]
    public void RestoreStatusEffects_RestoresExactDurationAndSkipsInvalidLegacyEntries()
    {
        const float remaining = 3.1415927f;
        var data = CreateMinimalData() with
        {
            ActiveStatusEffects = new[]
            {
                new ActiveStatusEffectSaveData { EffectId = "well_fed", RemainingDurationSeconds = remaining },
                new ActiveStatusEffectSaveData { EffectId = "removed_effect", RemainingDurationSeconds = 5f },
                new ActiveStatusEffectSaveData { EffectId = "well_fed", RemainingDurationSeconds = 9f },
                new ActiveStatusEffectSaveData { EffectId = "poisoned", RemainingDurationSeconds = 0f },
                new ActiveStatusEffectSaveData { EffectId = "", RemainingDurationSeconds = 2f }
            }
        };
        var collection = new StatusEffectCollection();
        var warnings = new List<PlayerLoadWarning>();

        new PlayerSaveService().RestoreStatusEffects(data, collection, CreateEffects(), warnings);

        var restored = Assert.Single(collection.ActiveEffects);
        Assert.Equal("well_fed", restored.Definition.Id);
        Assert.Equal(remaining, restored.RemainingSeconds);
        Assert.Contains(warnings, warning => warning.Kind == PlayerLoadWarningKind.UnknownStatusEffect);
        Assert.Contains(warnings, warning => warning.Kind == PlayerLoadWarningKind.DuplicateStatusEffect);
        Assert.Contains(warnings, warning => warning.Kind == PlayerLoadWarningKind.InvalidStatusEffectDuration);
        Assert.Contains(warnings, warning => warning.Kind == PlayerLoadWarningKind.MissingStatusEffectId);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    private static ItemRegistry CreateItems()
    {
        return ItemRegistry.Create(new[]
        {
            new ItemDefinition
            {
                Id = "dirt_block",
                DisplayName = "Dirt Block",
                Type = ItemType.PlaceableTile,
                TexturePath = "items/dirt_block",
                MaxStack = 999
            },
            new ItemDefinition
            {
                Id = "stone_block",
                DisplayName = "Stone Block",
                Type = ItemType.PlaceableTile,
                TexturePath = "items/stone_block",
                MaxStack = 999
            },
            new ItemDefinition
            {
                Id = "copper_helmet",
                DisplayName = "Copper Helmet",
                Type = ItemType.Armor,
                TexturePath = "items/copper_helmet",
                EquipmentSlot = EquipmentSlotType.Head
            },
            new ItemDefinition
            {
                Id = "swift_charm",
                DisplayName = "Swift Charm",
                Type = ItemType.Accessory,
                TexturePath = "items/swift_charm"
            }
        });
    }

    private static StatusEffectRegistry CreateEffects()
    {
        return StatusEffectRegistry.Create(new[]
        {
            new StatusEffectDefinition
            {
                Id = "well_fed",
                DisplayName = "Well Fed",
                DurationSeconds = 30f
            },
            new StatusEffectDefinition
            {
                Id = "poisoned",
                DisplayName = "Poisoned",
                DurationSeconds = 10f,
                DamagePerTick = 1
            }
        });
    }

    private static PlayerSaveData CreateMinimalData()
    {
        return new PlayerSaveData
        {
            PlayerId = "player_001",
            DisplayName = "Builder",
            PositionX = 0,
            PositionY = 0,
            Health = 100,
            MaxHealth = 100,
            Mana = 20,
            SelectedHotbarSlot = 0,
            InventorySlots = Array.Empty<ItemStack>()
        };
    }
}
