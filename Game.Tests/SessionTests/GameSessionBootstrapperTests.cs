using Game.Core.Inventory;
using Game.Core.Characters;
using Game.Core.Equipment;
using Game.Core.Diagnostics;
using Game.Core.Saving;
using Game.Core.Sessions;
using Game.Core.Settings;
using Xunit;

namespace Game.Tests.SessionTests;

public sealed class GameSessionBootstrapperTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "yjse-session-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void LoadOrCreate_CreatesNewSessionFromGameProjectStartup()
    {
        WriteProject();

        var result = new GameSessionBootstrapper().LoadOrCreate(new GameSessionBootstrapRequest(
            _root,
            Path.Combine(_root, "Saves", "world_1"),
            Seed: 1234,
            WorldName: "Bootstrap World",
            CreateFiniteSettings()));

        Assert.False(result.LoadedExistingSave);
        Assert.Equal("tiny", result.WorldGenerationProfile.Id);
        Assert.Equal("default", result.Startup?.Id);
        Assert.Equal("Bootstrap World", result.Session.World.Metadata.Name);
        Assert.Equal(1234, result.Session.World.Metadata.Seed);
        Assert.Equal(2, result.Session.Inventory.SelectedHotbarSlot);
        Assert.Equal(new ItemStack("starter_pickaxe", 1), result.Session.Inventory.Hotbar.Slots[0].Stack);
        Assert.Equal(new ItemStack("dirt_block", 10), result.Session.Inventory.Hotbar.Slots[1].Stack);
        Assert.Same(result.Session.Inventory, result.StarterInventory?.Inventory);
        Assert.False(result.Session.LoadedFromSave);
        Assert.Equal(0.5, result.Session.WorldTime.NormalizedTimeOfDay, precision: 3);
        Assert.False(result.Session.WorldTime.IsNight);
        AssertSessionIdentity(result.Session);
        result.Session.Dispose();
    }

    [Fact]
    public void LoadOrCreate_LoadsExistingSaveBeforeApplyingStartupInventory()
    {
        WriteProject();
        var saveDirectory = Path.Combine(_root, "Saves", "world_2");
        var bootstrapper = new GameSessionBootstrapper();
        var created = bootstrapper.LoadOrCreate(new GameSessionBootstrapRequest(
            _root,
            saveDirectory,
            Seed: 444,
            WorldName: "Saved World",
            CreateFiniteSettings()));
        AssertSessionIdentity(created.Session);

        created.Session.Inventory.Hotbar.Slots[0].SetStack(new ItemStack("dirt_block", 3));
        var equipment = Assert.IsType<EquipmentLoadout>(created.Session.EquipmentLoadout);
        Assert.True(equipment.TryEquip(new ItemStack("copper_helmet", 1), created.Session.Content.Items, EquipmentSlotType.Head).Success);
        created.Session.Player.StatusEffects.Apply(created.Session.Content.StatusEffects.GetById("poisoned"), 3.5f);
        created.Session.WorldTime.Update(1_500);
        var appearance = new CharacterAppearance
        {
            HairStyleId = "ponytail",
            HairColor = "#112233",
            ShirtColor = "#445566"
        };
        var savedWorldEventState = created.Session.Simulation.LivingWorld.CaptureWorldEventState();
        new GameSaveCoordinator().Save(
            new GameSaveRequest(created.Session.World, created.Session.Player, created.Session.Inventory, created.Session.Entities)
            {
                TileEntities = created.Session.TileEntities,
                FarmPlots = created.Session.FarmPlots,
                EquipmentLoadout = equipment,
                CharacterAppearance = appearance,
                WorldTime = created.Session.WorldTime,
                RandomStreams = created.Session.RandomStreams,
                WorldEventState = savedWorldEventState
            },
            saveDirectory,
            new GameSaveCoordinatorOptions { WorldSaveMode = WorldSaveMode.AllChunks });
        var savedStateHash = SimulationStateHasher.Compute(
            created.Session.World,
            created.Session.Player,
            created.Session.Inventory,
            created.Session.Entities,
            created.Session.WorldTime,
            created.Session.FarmPlots,
            created.Session.EquipmentLoadout,
            created.Session.RandomStreams);

        var loaded = bootstrapper.LoadOrCreate(new GameSessionBootstrapRequest(
            _root,
            saveDirectory,
            Seed: 999,
            WorldName: "Should Not Replace Save",
            CreateFiniteSettings()));

        Assert.True(loaded.LoadedExistingSave);
        Assert.True(loaded.Session.LoadedFromSave);
        Assert.Null(loaded.StarterInventory);
        Assert.Equal("Saved World", loaded.Session.World.Metadata.Name);
        Assert.Equal(444, loaded.Session.World.Metadata.Seed);
        Assert.Equal(new ItemStack("dirt_block", 3), loaded.Session.Inventory.Hotbar.Slots[0].Stack);
        Assert.Equal("copper_helmet", loaded.Session.EquipmentLoadout?.GetStack(EquipmentSlotType.Head).ItemId);
        Assert.Equal("ponytail", loaded.Session.CharacterAppearance?.HairStyleId);
        Assert.Equal("#112233", loaded.Session.CharacterAppearance?.HairColor);
        var restoredEffect = Assert.Single(loaded.Session.Player.StatusEffects.ActiveEffects);
        Assert.Equal("poisoned", restoredEffect.Definition.Id);
        Assert.Equal(3.5f, restoredEffect.RemainingSeconds, precision: 3);
        Assert.True(loaded.Session.WorldTime.Day > 1);
        Assert.Equal(created.Session.WorldTime.Day, loaded.Session.WorldTime.Day);
        Assert.Equal(created.Session.WorldTime.TimeOfDaySeconds, loaded.Session.WorldTime.TimeOfDaySeconds);
        Assert.Empty(loaded.Session.PlayerLoadWarnings ?? Array.Empty<PlayerLoadWarning>());
        AssertSessionIdentity(loaded.Session);
        Assert.NotSame(created.Session.World, loaded.Session.World);
        Assert.NotSame(created.Session.Player, loaded.Session.Player);
        Assert.NotSame(created.Session.Inventory, loaded.Session.Inventory);
        Assert.NotSame(created.Session.Entities, loaded.Session.Entities);
        Assert.NotSame(created.Session.Simulation, loaded.Session.Simulation);
        Assert.Same(loaded.Session.EquipmentLoadout, loaded.Session.Simulation.EquipmentLoadout);
        var resumedWorldEventState = loaded.Session.Simulation.LivingWorld.CaptureWorldEventState();
        Assert.NotNull(savedWorldEventState);
        Assert.NotNull(resumedWorldEventState);
        Assert.True(
            resumedWorldEventState!.Runtime.LastAdvancedTick >=
            savedWorldEventState!.Runtime.LastAdvancedTick);
        Assert.Equal(
            savedWorldEventState.LastProcessedPlayerActionSequence,
            resumedWorldEventState.LastProcessedPlayerActionSequence);
        Assert.Equal(
            savedWorldEventState.Runtime.Cooldowns.ToArray(),
            resumedWorldEventState.Runtime.Cooldowns.ToArray());
        Assert.Equal(
            savedWorldEventState.Journal.Entries.ToArray(),
            resumedWorldEventState.Journal.Entries.Take(savedWorldEventState.Journal.Entries.Count).ToArray());
        var resumedStateHash = SimulationStateHasher.Compute(
            loaded.Session.World,
            loaded.Session.Player,
            loaded.Session.Inventory,
            loaded.Session.Entities,
            loaded.Session.WorldTime,
            loaded.Session.FarmPlots,
            loaded.Session.EquipmentLoadout,
            loaded.Session.RandomStreams);
        Assert.Equal(savedStateHash, resumedStateHash);

        created.Session.Dispose();
        loaded.Session.Dispose();
    }

    [Fact]
    public void LoadedGameSession_DisposeOwnsSimulationLifecycle()
    {
        WriteProject();
        var session = new GameSessionBootstrapper().LoadOrCreate(new GameSessionBootstrapRequest(
            _root,
            Path.Combine(_root, "Saves", "world_dispose"),
            Seed: 5,
            WorldName: "Dispose World",
            CreateFiniteSettings())).Session;

        session.Dispose();
        session.Dispose();

        Assert.Throws<ObjectDisposedException>(() => session.Simulation.Tick(Game.Core.Entities.PlayerCommand.None, 0.016f));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static GameSettings CreateFiniteSettings()
    {
        return GameSettings.CreateDefault() with
        {
            World = new WorldSettings
            {
                InfiniteHorizontalGeneration = false,
                WorldProfileId = "missing_settings_profile"
            }
        };
    }

    private static void AssertSessionIdentity(LoadedGameSession session)
    {
        Assert.Same(session.Content, session.Simulation.Content);
        Assert.Same(session.World, session.Simulation.World);
        Assert.Same(session.Player, session.Simulation.Player);
        Assert.Same(session.Inventory, session.Simulation.PlayerInventory);
        Assert.Same(session.Entities, session.Simulation.Entities);
        Assert.Same(session.Events, session.Simulation.Events);
        Assert.Same(session.WorldTime, session.Simulation.Time);
        Assert.Same(session.FarmPlots, session.Simulation.FarmPlots);
        Assert.Same(session.EquipmentLoadout, session.Simulation.EquipmentLoadout);
    }

    private void WriteProject()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "yjse.game.json"), """
        {
          "schemaVersion": 1,
          "id": "bootstrap_test_game",
          "displayName": "Bootstrap Test Game",
          "contentRoot": "Content",
          "modsRoot": "Mods",
          "startupDefinitionId": "default"
        }
        """);

        WriteTiles();
        WriteItems();
        WriteWorldgen();
        WriteStartup();
        WriteEffects();
        WriteCharacter();
    }

    private void WriteTiles()
    {
        var root = Path.Combine(_root, "Content", "tiles");
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "dirt.json"), """
        {
          "id": "dirt",
          "numericId": 1,
          "displayName": "Dirt",
          "texture": "tiles/dirt",
          "solid": true,
          "blocksLight": true,
          "hardness": 1.0,
          "dropItem": "dirt_block"
        }
        """);
        File.WriteAllText(Path.Combine(root, "grass.json"), """
        {
          "id": "grass",
          "numericId": 2,
          "displayName": "Grass",
          "texture": "tiles/grass",
          "solid": true,
          "blocksLight": true,
          "hardness": 1.0,
          "dropItem": "dirt_block"
        }
        """);
        File.WriteAllText(Path.Combine(root, "stone.json"), """
        {
          "id": "stone",
          "numericId": 3,
          "displayName": "Stone",
          "texture": "tiles/stone",
          "solid": true,
          "blocksLight": true,
          "hardness": 2.0
        }
        """);
    }

    private void WriteItems()
    {
        var root = Path.Combine(_root, "Content", "items");
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "starter_pickaxe.json"), """
        {
          "id": "starter_pickaxe",
          "displayName": "Starter Pickaxe",
          "type": "ToolPickaxe",
          "texture": "items/starter_pickaxe",
          "maxStack": 1,
          "toolPower": 25
        }
        """);
        File.WriteAllText(Path.Combine(root, "dirt_block.json"), """
        {
          "id": "dirt_block",
          "displayName": "Dirt Block",
          "type": "PlaceableTile",
          "texture": "items/dirt_block",
          "maxStack": 99,
          "placesTile": "dirt"
        }
        """);
        File.WriteAllText(Path.Combine(root, "copper_helmet.json"), """
        {
          "id": "copper_helmet",
          "displayName": "Copper Helmet",
          "type": "Armor",
          "texture": "items/copper_helmet",
          "maxStack": 1,
          "equipmentSlot": "Head",
          "defense": 2
        }
        """);
    }

    private void WriteWorldgen()
    {
        var root = Path.Combine(_root, "Content", "worldgen");
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "tiny.json"), """
        {
          "id": "tiny",
          "widthTiles": 64,
          "heightTiles": 64,
          "surfaceBaseY": 24,
          "surfaceAmplitude": 2,
          "dirtDepthMin": 4,
          "dirtDepthMax": 6,
          "caveWalkerCount": 0,
          "caveWalkLength": 0,
          "treeAttempts": 0,
          "waterPocketAttempts": 0
        }
        """);
    }

    private void WriteStartup()
    {
        var root = Path.Combine(_root, "Content", "startup");
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "default.json"), """
        {
          "id": "default",
          "displayName": "Default Start",
          "worldProfileId": "tiny",
          "selectedHotbarSlot": 2,
          "starterItems": [
            { "itemId": "starter_pickaxe", "count": 1, "target": "Hotbar", "slot": 0 },
            { "itemId": "dirt_block", "count": 10, "target": "Hotbar", "slot": 1 }
          ]
        }
        """);
    }

    private void WriteEffects()
    {
        var root = Path.Combine(_root, "Content", "effects");
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "poisoned.json"), """
        {
          "id": "poisoned",
          "displayName": "Poisoned",
          "kind": "Debuff",
          "durationSeconds": 6.0,
          "tickIntervalSeconds": 1.0,
          "damagePerTick": 1
        }
        """);
    }

    private void WriteCharacter()
    {
        var root = Path.Combine(_root, "Content", "characters");
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "player.json"), """
        {
          "id": "player",
          "displayName": "Player",
          "width": 12,
          "height": 28,
          "defaultAppearance": {
            "hairStyleId": "short"
          },
          "animationSet": {
            "id": "player.default",
            "displayName": "Default",
            "defaultClipId": "player.idle"
          }
        }
        """);
    }
}
