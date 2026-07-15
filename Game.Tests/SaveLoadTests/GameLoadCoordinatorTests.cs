using Game.Core;
using Game.Core.Biomes;
using Game.Core.Characters;
using Game.Core.Crafting;
using Game.Core.Data;
using Game.Core.Effects;
using Game.Core.Entities;
using Game.Core.Equipment;
using Game.Core.Events;
using Game.Core.Farming;
using Game.Core.Inventory;
using Game.Core.Items;
using Game.Core.Loot;
using Game.Core.Physics;
using Game.Core.Projectiles;
using Game.Core.Randomness;
using Game.Core.Saving;
using Game.Core.Spawning;
using Game.Core.Tiles;
using Game.Core.World;
using Game.Core.World.TileEntities;
using Game.Core.WorldEvents;
using Game.Core.Time;
using System.Numerics;
using Xunit;

namespace Game.Tests.SaveLoadTests;

public sealed class GameLoadCoordinatorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "terraria-like-game-load-tests", Guid.NewGuid().ToString("N"));
    private readonly GameContentDatabase _content = CreateContent();

    [Fact]
    public void SaveThenLoad_RestoresWholeRuntimeSession()
    {
        var world = new World(64, 64, WorldMetadata.CreateDefault(seed: 99));
        world.SetTile(2, 3, KnownTileIds.Dirt);
        world.SetTile(3, 3, KnownTileIds.Stone);

        var player = new PlayerEntity(new Vector2(88, 144), new TileCollisionResolver(), maxHealth: 140, currentHealth: 73);
        var inventory = new PlayerInventory(_content.Items);
        inventory.SelectHotbarSlot(3);
        inventory.AddItem(new ItemStack("gel", 5));

        var entities = new EntityManager();
        var drop = new DroppedItemEntity(new ItemStack("gel", 2), new Vector2(48, 72), new TileCollisionResolver());
        drop.Body.Velocity = new Vector2(5, -2);
        entities.Add(drop);

        var tileEntities = new TileEntityManager();
        var chest = new ChestTileEntity(new TilePos(6, 7), _content.Items, slotCount: 6);
        chest.AddItem(new ItemStack("gel", 9));
        tileEntities.Add(chest);
        var farmPlots = new FarmPlotManager();
        var farmPlot = farmPlots.GetOrCreatePlot(new TilePos(9, 10));
        farmPlot.IsTilled = true;
        farmPlot.IsWatered = true;
        farmPlot.Crop = new CropInstance("parsnip", plantedDay: 2, daysUntilHarvest: 1);
        var worldTime = new WorldTime(dayLengthSeconds: 600);
        worldTime.Update(1_275);

        new GameSaveCoordinator().Save(
            new GameSaveRequest(world, player, inventory, entities)
            {
                TileEntities = tileEntities,
                FarmPlots = farmPlots,
                WorldTime = worldTime
            },
            _root,
            new GameSaveCoordinatorOptions
            {
                PlayerId = "player_load",
                PlayerDisplayName = "Loader",
                ChunkStorageMode = WorldChunkStorageMode.RegionFiles,
                WorldSaveMode = WorldSaveMode.AllChunks
            });

        var events = new GameEventBus();
        GameLoadedEvent? loadedEvent = null;
        events.Subscribe<GameLoadedEvent>(gameEvent => loadedEvent = gameEvent);

        var result = new GameLoadCoordinator(
                new WorldSaveService(WorldChunkStorageMode.RegionFiles),
                new PlayerSaveService(),
                new EntitySaveService(),
                new TileEntitySaveService(),
                new TileCollisionResolver(),
                clock: () => new DateTimeOffset(2026, 5, 10, 18, 30, 0, TimeSpan.Zero))
            .Load(_root, _content, events: events);

        Assert.Equal(new DateTimeOffset(2026, 5, 10, 18, 30, 0, TimeSpan.Zero), result.LoadedAtUtc);
        Assert.True(result.PlayerLoaded);
        Assert.True(result.RuntimeEntitiesLoaded);
        Assert.True(result.TileEntitiesLoaded);
        Assert.True(result.FarmPlotsLoaded);
        Assert.True(result.SimulationStateLoaded);
        Assert.Equal(KnownTileIds.Dirt, result.World.GetTile(2, 3).TileId);
        Assert.Equal(KnownTileIds.Stone, result.World.GetTile(3, 3).TileId);
        Assert.Equal(new Vector2(88, 144), result.Player.Body.Position);
        Assert.Equal(73, result.Player.Health);
        Assert.Equal(140, result.Player.MaxHealth);
        Assert.Equal(3, result.Inventory.SelectedHotbarSlot);
        Assert.Equal(5, result.Inventory.CountItem("gel"));

        var loadedDrop = Assert.IsType<DroppedItemEntity>(Assert.Single(result.Entities.Entities));
        Assert.Equal(new ItemStack("gel", 2), loadedDrop.Stack);
        Assert.Equal(new Vector2(5, -2), loadedDrop.Body.Velocity);

        var loadedChest = Assert.IsType<ChestTileEntity>(Assert.Single(result.TileEntities.Entities));
        Assert.Equal(new TilePos(6, 7), loadedChest.Position);
        Assert.Equal(9, loadedChest.Inventory.CountItem("gel"));
        var loadedFarmPlot = Assert.Single(result.FarmPlots.Plots);
        Assert.Equal(new TilePos(9, 10), loadedFarmPlot.Position);
        Assert.True(loadedFarmPlot.IsTilled);
        Assert.True(loadedFarmPlot.IsWatered);
        Assert.NotNull(loadedFarmPlot.Crop);
        Assert.Equal("parsnip", loadedFarmPlot.Crop!.CropId);
        Assert.Equal(1, result.FarmPlotCount);
        Assert.Equal(3, result.WorldTime.Day);
        Assert.Equal(75, result.WorldTime.TimeOfDaySeconds);
        Assert.Equal(600, result.WorldTime.DayLengthSeconds);
        Assert.NotNull(loadedEvent);
        Assert.Equal(result, loadedEvent.Result);
    }

    [Fact]
    public void CanLoad_RequiresMetadataAndPlayerFiles()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "metadata.json"), "{}");
        var coordinator = new GameLoadCoordinator();

        Assert.False(coordinator.CanLoad(_root));

        File.WriteAllText(Path.Combine(_root, "player.json"), "{}");

        Assert.True(coordinator.CanLoad(_root));
    }

    [Fact]
    public void Load_CanSkipOptionalRuntimeAndTileEntities()
    {
        var request = CreateSaveRequest();
        request.Entities.Add(new DroppedItemEntity(new ItemStack("gel", 1), new Vector2(10, 20), new TileCollisionResolver()));
        var tileEntities = new TileEntityManager();
        tileEntities.Add(new ChestTileEntity(new TilePos(1, 2), _content.Items, slotCount: 2));

        new GameSaveCoordinator().Save(
            request with { TileEntities = tileEntities },
            _root,
            new GameSaveCoordinatorOptions { WorldSaveMode = WorldSaveMode.AllChunks });

        var loaded = new GameLoadCoordinator().Load(
            _root,
            _content,
            new GameLoadCoordinatorOptions
            {
                LoadRuntimeEntities = false,
                LoadTileEntities = false
            });

        Assert.False(loaded.RuntimeEntitiesLoaded);
        Assert.False(loaded.TileEntitiesLoaded);
        Assert.False(loaded.FarmPlotsLoaded);
        Assert.Empty(loaded.Entities.Entities);
        Assert.Empty(loaded.TileEntities.Entities);
        Assert.Empty(loaded.FarmPlots.Plots);
    }

    [Fact]
    public void SaveThenLoad_RestoresEquipmentEffectsAndCharacterAppearance()
    {
        var request = CreateSaveRequest();
        var equipment = new EquipmentLoadout();
        Assert.True(equipment.TryEquip(
            new ItemStack("copper_helmet", 1),
            _content.Items,
            EquipmentSlotType.Head).Success);
        Assert.True(equipment.TryEquip(
            new ItemStack("swift_charm", 1),
            _content.Items,
            EquipmentSlotType.Accessory3).Success);
        var appearance = new CharacterAppearance
        {
            BodySpriteId = "entities/player/ranger",
            SkinTone = "#9d6c4b",
            HairStyleId = "ponytail",
            ClothesStyleId = "forest_coat",
            AccessoryId = "leaf_pin",
            HairColor = "#3a2418",
            ShirtColor = "#3d7653",
            PantsColor = "#29384b",
            EyeColor = "#6ec0a5"
        };
        request.Player.StatusEffects.Apply(_content.StatusEffects.GetById("well_fed"), 13.625f);
        request.Player.StatusEffects.Update(2.125f);

        new GameSaveCoordinator().Save(
            request with
            {
                EquipmentLoadout = equipment,
                CharacterAppearance = appearance
            },
            _root,
            new GameSaveCoordinatorOptions { WorldSaveMode = WorldSaveMode.AllChunks });

        var result = new GameLoadCoordinator().Load(_root, _content);

        Assert.Equal(new ItemStack("copper_helmet", 1), result.EquipmentLoadout.GetStack(EquipmentSlotType.Head));
        Assert.Equal(new ItemStack("swift_charm", 1), result.EquipmentLoadout.GetStack(EquipmentSlotType.Accessory3));
        Assert.Equal(appearance, result.CharacterAppearance);
        var restoredEffect = Assert.Single(result.Player.StatusEffects.ActiveEffects);
        Assert.Equal("well_fed", restoredEffect.Definition.Id);
        Assert.Equal(11.5f, restoredEffect.RemainingSeconds);
        Assert.Empty(result.PlayerWarnings);
    }

    [Fact]
    public void Load_LegacySaveWithoutSimulationStateUsesDefaultWorldTime()
    {
        new GameSaveCoordinator().Save(
            CreateSaveRequest(),
            _root,
            new GameSaveCoordinatorOptions { WorldSaveMode = WorldSaveMode.AllChunks });

        var result = new GameLoadCoordinator().Load(_root, _content);

        Assert.False(result.SimulationStateLoaded);
        Assert.Equal(1, result.WorldTime.Day);
        Assert.Equal(0, result.WorldTime.TimeOfDaySeconds);
        Assert.Equal(24 * 60, result.WorldTime.DayLengthSeconds);
    }

    [Fact]
    public void Load_SkipsUnknownOrInvalidPlayerIdentityEntriesAndReturnsWarnings()
    {
        var request = CreateSaveRequest();
        new GameSaveCoordinator().Save(
            request,
            _root,
            new GameSaveCoordinatorOptions { WorldSaveMode = WorldSaveMode.AllChunks });
        var playerPath = Path.Combine(_root, "player.json");
        var playerService = new PlayerSaveService();
        var data = playerService.Load(playerPath) with
        {
            EquipmentLoadout = new EquipmentLoadoutSaveData
            {
                Slots = new[]
                {
                    new EquipmentSlotSaveData { SlotId = "Head", ItemId = "copper_helmet" },
                    new EquipmentSlotSaveData { SlotId = "Accessory1", ItemId = "removed_charm" },
                    new EquipmentSlotSaveData { SlotId = "Body", ItemId = "gel" },
                    new EquipmentSlotSaveData { SlotId = "Cape", ItemId = "copper_helmet" }
                }
            },
            ActiveStatusEffects = new[]
            {
                new ActiveStatusEffectSaveData { EffectId = "well_fed", RemainingDurationSeconds = 7.25f },
                new ActiveStatusEffectSaveData { EffectId = "removed_effect", RemainingDurationSeconds = 4f },
                new ActiveStatusEffectSaveData { EffectId = "poisoned", RemainingDurationSeconds = -1f }
            }
        };
        playerService.Save(data, playerPath);

        var result = new GameLoadCoordinator().Load(_root, _content);

        Assert.Equal(new ItemStack("copper_helmet", 1), result.EquipmentLoadout.GetStack(EquipmentSlotType.Head));
        Assert.True(result.EquipmentLoadout.GetStack(EquipmentSlotType.Accessory1).IsEmpty);
        var effect = Assert.Single(result.Player.StatusEffects.ActiveEffects);
        Assert.Equal("well_fed", effect.Definition.Id);
        Assert.Equal(7.25f, effect.RemainingSeconds);
        Assert.Contains(result.PlayerWarnings, warning => warning.Kind == PlayerLoadWarningKind.UnknownEquipmentItem);
        Assert.Contains(result.PlayerWarnings, warning => warning.Kind == PlayerLoadWarningKind.IncompatibleEquipmentItem);
        Assert.Contains(result.PlayerWarnings, warning => warning.Kind == PlayerLoadWarningKind.InvalidEquipmentSlot);
        Assert.Contains(result.PlayerWarnings, warning => warning.Kind == PlayerLoadWarningKind.UnknownStatusEffect);
        Assert.Contains(result.PlayerWarnings, warning => warning.Kind == PlayerLoadWarningKind.InvalidStatusEffectDuration);
    }

    [Fact]
    public void Load_VersionOnePlayerWithoutIdentityFields_RestoresDefaults()
    {
        new GameSaveCoordinator().Save(
            CreateSaveRequest(),
            _root,
            new GameSaveCoordinatorOptions { WorldSaveMode = WorldSaveMode.AllChunks });
        File.WriteAllText(
            Path.Combine(_root, "player.json"),
            """
            {
              "FormatVersion": 1,
              "PlayerId": "legacy_player",
              "DisplayName": "Legacy",
              "PositionX": 24,
              "PositionY": 48,
              "Health": 90,
              "MaxHealth": 100,
              "Mana": 12,
              "SelectedHotbarSlot": 0,
              "InventorySlots": []
            }
            """);

        var result = new GameLoadCoordinator().Load(_root, _content);

        Assert.Equal(new Vector2(24, 48), result.Player.Body.Position);
        Assert.All(result.EquipmentLoadout.Slots.Values, stack => Assert.True(stack.IsEmpty));
        Assert.Empty(result.Player.StatusEffects.ActiveEffects);
        Assert.Equal(new CharacterAppearance(), result.CharacterAppearance);
        Assert.Empty(result.PlayerWarnings);
    }

    [Fact]
    public void SaveThenLoad_RandomStreamsResumeExactlyAtMidTraceCheckpoint()
    {
        var randoms = new SessionRandomRegistry(12);
        var stream = randoms.GetStream("combat.loot");
        for (var draw = 0; draw < 137; draw++)
        {
            stream.NextUInt64();
        }

        var request = CreateSaveRequest() with { RandomStreams = randoms };
        new GameSaveCoordinator().Save(
            request,
            _root,
            new GameSaveCoordinatorOptions { WorldSaveMode = WorldSaveMode.AllChunks });
        var expectedContinuation = Enumerable.Range(0, 96).Select(_ => stream.NextUInt64()).ToArray();

        var loaded = new GameLoadCoordinator().Load(_root, _content);
        var actualContinuation = Enumerable.Range(0, 96)
            .Select(_ => loaded.RandomStreams.GetStream("combat.loot").NextUInt64())
            .ToArray();

        Assert.True(loaded.RandomStateLoaded);
        Assert.Equal(RandomStateLoadSource.Primary, loaded.RandomStateSource);
        Assert.Null(loaded.RandomStateWarning);
        Assert.Equal(expectedContinuation, actualContinuation);
    }

    [Fact]
    public void Load_LegacySaveWithoutRandomSidecarCreatesSeededRegistry()
    {
        new GameSaveCoordinator().Save(
            CreateSaveRequest(),
            _root,
            new GameSaveCoordinatorOptions { WorldSaveMode = WorldSaveMode.AllChunks });

        var loaded = new GameLoadCoordinator().Load(_root, _content);
        var expected = new SessionRandomRegistry(12).GetStream("spawning.rules").NextUInt64();

        Assert.False(loaded.RandomStateLoaded);
        Assert.Equal(RandomStateLoadSource.LegacyFallback, loaded.RandomStateSource);
        Assert.Equal(expected, loaded.RandomStreams.GetStream("spawning.rules").NextUInt64());
    }

    [Fact]
    public void SaveThenLoad_WorldEventStatePreservesActiveEventCooldownsAndJournal()
    {
        var worldEventState = CreateWorldEventState();
        new GameSaveCoordinator().Save(
            CreateSaveRequest() with { WorldEventState = worldEventState },
            _root,
            new GameSaveCoordinatorOptions { WorldSaveMode = WorldSaveMode.AllChunks });

        var loaded = new GameLoadCoordinator().Load(_root, _content);

        Assert.True(loaded.WorldEventStateLoaded);
        Assert.Equal(WorldEventStateLoadSource.Primary, loaded.WorldEventStateSource);
        Assert.Null(loaded.WorldEventStateWarning);
        Assert.NotNull(loaded.WorldEventState);
        Assert.Equal(worldEventState.Runtime.LastAdvancedTick, loaded.WorldEventState!.Runtime.LastAdvancedTick);
        Assert.Equal(worldEventState.Runtime.ActiveEventId, loaded.WorldEventState.Runtime.ActiveEventId);
        Assert.Equal(
            worldEventState.Runtime.Cooldowns.ToArray(),
            loaded.WorldEventState.Runtime.Cooldowns.ToArray());
        Assert.Equal(
            worldEventState.Journal.Entries.ToArray(),
            loaded.WorldEventState.Journal.Entries.ToArray());
        Assert.Equal(
            worldEventState.LastProcessedPlayerActionSequence,
            loaded.WorldEventState.LastProcessedPlayerActionSequence);
        Assert.True(File.Exists(Path.Combine(_root, WorldEventStateSaveService.DefaultFileName)));
    }

    [Fact]
    public void Load_LegacySaveWithoutWorldEventSidecarUsesEmptyDefault()
    {
        new GameSaveCoordinator().Save(
            CreateSaveRequest(),
            _root,
            new GameSaveCoordinatorOptions { WorldSaveMode = WorldSaveMode.AllChunks });

        var loaded = new GameLoadCoordinator().Load(_root, _content);

        Assert.False(loaded.WorldEventStateLoaded);
        Assert.Equal(WorldEventStateLoadSource.LegacyFallback, loaded.WorldEventStateSource);
        Assert.Null(loaded.WorldEventState);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private GameSaveRequest CreateSaveRequest()
    {
        var world = new World(32, 32, WorldMetadata.CreateDefault(seed: 12));
        world.SetTile(0, 0, KnownTileIds.Dirt);
        return new GameSaveRequest(
            world,
            new PlayerEntity(new Vector2(16, 32), new TileCollisionResolver()),
            new PlayerInventory(_content.Items),
            new EntityManager());
    }

    private static WorldEventRuntimeStateSnapshot CreateWorldEventState()
    {
        return new WorldEventRuntimeStateSnapshot
        {
            Runtime = new WorldEventRuntimeSnapshot
            {
                LastAdvancedTick = 600,
                RegionIndex = 2,
                BiomeId = "forest",
                Status = WorldEventRuntimeStatus.Active,
                ActiveEventId = "firefly_bloom",
                LastEventId = "firefly_bloom",
                StartTick = 540,
                EndTickExclusive = 1_200,
                PhaseId = "bloom",
                PhaseIndex = 1,
                Progress = 0.1f,
                PhaseProgress = 0.2f,
                Intensity = 0.75f,
                EffectiveModifiers = WorldEventModifierSet.Identity with
                {
                    RareLootChanceMultiplier = 1.25f
                },
                Cooldowns = [new WorldEventCooldownState("wildlife_migration", 2_000)]
            },
            Journal = new WorldEventJournalSnapshot(
                WorldEventJournalSnapshot.CurrentFormatVersion,
                1,
                [new WorldEventDomainEvent(
                    0,
                    600,
                    2,
                    "firefly_bloom",
                    WorldEventDomainEventKind.Progressed,
                    "bloom",
                    0.1f,
                    0)]),
            LastProcessedPlayerActionSequence = 7
        };
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
            }),
            RecipeRegistry.Create(Array.Empty<RecipeDefinition>()),
            LootTableRegistry.Create(Array.Empty<LootTableDefinition>()),
            BiomeRegistry.Create(Array.Empty<BiomeDefinition>()),
            ProjectileRegistry.Create(Array.Empty<ProjectileDefinition>()),
            EntityDefinitionRegistry.Create(Array.Empty<EntityDefinition>()),
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
                    GrowthStageDays = new[] { 1, 1, 1 }
                }
            }),
            StatusEffects = StatusEffectRegistry.Create(new[]
            {
                new StatusEffectDefinition
                {
                    Id = "well_fed",
                    DisplayName = "Well Fed",
                    DurationSeconds = 30f,
                    MovementSpeedBonus = 0.1f
                },
                new StatusEffectDefinition
                {
                    Id = "poisoned",
                    DisplayName = "Poisoned",
                    DurationSeconds = 10f,
                    DamagePerTick = 1
                }
            })
        };
    }
}
