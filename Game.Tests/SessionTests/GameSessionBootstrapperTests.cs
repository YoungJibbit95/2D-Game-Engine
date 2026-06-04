using Game.Core.Inventory;
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

        created.Session.Inventory.Hotbar.Slots[0].SetStack(new ItemStack("dirt_block", 3));
        new GameSaveCoordinator().Save(
            new GameSaveRequest(created.Session.World, created.Session.Player, created.Session.Inventory, created.Session.Entities)
            {
                TileEntities = created.Session.TileEntities,
                FarmPlots = created.Session.FarmPlots
            },
            saveDirectory,
            new GameSaveCoordinatorOptions { WorldSaveMode = WorldSaveMode.AllChunks });

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
}
