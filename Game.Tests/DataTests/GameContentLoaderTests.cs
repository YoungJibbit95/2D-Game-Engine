using Game.Core.Data;
using Game.Core.Mods;
using Xunit;

namespace Game.Tests.DataTests;

public sealed class GameContentLoaderTests : IDisposable
{
    private readonly string _contentRoot = Path.Combine(Path.GetTempPath(), "terraria-like-content-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void LoadFromRoot_LoadsTileAndItemRegistries()
    {
        Directory.CreateDirectory(Path.Combine(_contentRoot, "assets"));
        Directory.CreateDirectory(Path.Combine(_contentRoot, "tiles"));
        Directory.CreateDirectory(Path.Combine(_contentRoot, "items"));
        Directory.CreateDirectory(Path.Combine(_contentRoot, "recipes"));
        Directory.CreateDirectory(Path.Combine(_contentRoot, "loot"));
        Directory.CreateDirectory(Path.Combine(_contentRoot, "biomes"));
        Directory.CreateDirectory(Path.Combine(_contentRoot, "projectiles"));
        Directory.CreateDirectory(Path.Combine(_contentRoot, "entities"));
        Directory.CreateDirectory(Path.Combine(_contentRoot, "effects"));
        Directory.CreateDirectory(Path.Combine(_contentRoot, "worldgen"));

        File.WriteAllText(Path.Combine(_contentRoot, "tiles", "dirt.json"), """
        {
          "id": "dirt",
          "numericId": 1,
          "displayName": "Dirt Block",
          "texture": "tiles/dirt",
          "solid": true,
          "blocksLight": true,
          "hardness": 1.0,
          "miningPowerRequired": 0,
          "dropItem": "dirt_block",
          "mergeGroup": "soil"
        }
        """);

        File.WriteAllText(Path.Combine(_contentRoot, "items", "dirt_block.json"), """
        {
          "id": "dirt_block",
          "displayName": "Dirt Block",
          "type": "PlaceableTile",
          "texture": "items/dirt_block",
          "maxStack": 999,
          "placesTile": "dirt"
        }
        """);

        File.WriteAllText(Path.Combine(_contentRoot, "recipes", "stone_from_dirt.json"), """
        {
          "id": "stone_from_dirt",
          "result": { "itemId": "stone_block", "count": 1 },
          "ingredients": [
            { "itemId": "dirt_block", "count": 2 }
          ]
        }
        """);

        File.WriteAllText(Path.Combine(_contentRoot, "loot", "slime_basic.json"), """
        {
          "id": "slime_basic",
          "entries": [
            { "itemId": "gel", "min": 1, "max": 3, "chance": 1.0 }
          ]
        }
        """);

        File.WriteAllText(Path.Combine(_contentRoot, "biomes", "forest.json"), """
        {
          "id": "forest",
          "displayName": "Forest",
          "surfaceTile": "grass",
          "undergroundTile": "dirt"
        }
        """);

        File.WriteAllText(Path.Combine(_contentRoot, "projectiles", "wooden_arrow.json"), """
        {
          "id": "wooden_arrow",
          "texture": "projectiles/wooden_arrow",
          "speed": 320,
          "damage": 5,
          "lifetime": 5.0
        }
        """);

        File.WriteAllText(Path.Combine(_contentRoot, "entities", "slime.json"), """
        {
          "id": "slime",
          "displayName": "Slime",
          "texture": "entities/slime",
          "maxHealth": 20
        }
        """);

        File.WriteAllText(Path.Combine(_contentRoot, "effects", "poisoned.json"), """
        {
          "id": "poisoned",
          "displayName": "Poisoned",
          "kind": "Debuff",
          "durationSeconds": 6.0,
          "tickIntervalSeconds": 1.0,
          "damagePerTick": 2
        }
        """);

        File.WriteAllText(Path.Combine(_contentRoot, "worldgen", "tiny.json"), """
        {
          "id": "tiny",
          "widthTiles": 96,
          "heightTiles": 64,
          "surfaceBaseY": 24,
          "surfaceAmplitude": 4,
          "dirtDepthMin": 4,
          "dirtDepthMax": 8,
          "ores": [
            { "tileId": 1, "veinCount": 2, "minDepthOffset": 8, "replaceTileId": 1 }
          ]
        }
        """);

        File.WriteAllText(Path.Combine(_contentRoot, "assets", "sprites.json"), """
        {
          "sprites": [
            { "id": "tiles/dirt", "path": "sprites/world/tiles/dirt.png", "category": "Tile", "width": 16, "height": 16 },
            { "id": "items/dirt_block", "path": "sprites/items/blocks/dirt_block.png", "category": "Item", "width": 16, "height": 16 },
            { "id": "projectiles/wooden_arrow", "path": "sprites/projectiles/wooden_arrow.png", "category": "Projectile", "width": 16, "height": 16 },
            { "id": "entities/slime", "path": "sprites/entities/slime.png", "category": "Entity", "width": 16, "height": 16 }
          ]
        }
        """);

        var database = new GameContentLoader().LoadFromRoot(_contentRoot);

        Assert.Equal("dirt", database.Tiles.GetByNumericId(1).Id);
        Assert.Equal("dirt", database.Items.GetById("dirt_block").PlacesTileId);
        Assert.True(database.Recipes.TryGetById("stone_from_dirt", out _));
        Assert.True(database.LootTables.TryGetById("slime_basic", out _));
        Assert.True(database.Biomes.TryGetById("forest", out _));
        Assert.True(database.Projectiles.TryGetById("wooden_arrow", out _));
        Assert.True(database.Entities.TryGetById("slime", out _));
        Assert.True(database.StatusEffects.TryGetById("poisoned", out _));
        Assert.True(database.SpriteAssets.TryGetById("items/dirt_block", out _));
        Assert.True(database.WorldGenerationProfiles.TryGetById("tiny", out _));
    }

    [Fact]
    public void LoadWithMods_ReportsOverridesAndAppliesLatestDefinition()
    {
        WriteMinimalBaseContent(_contentRoot);
        var modsRoot = Path.Combine(_contentRoot, "Mods");
        var modDirectory = Path.Combine(modsRoot, "StackMod");
        Directory.CreateDirectory(Path.Combine(modDirectory, "items"));
        File.WriteAllText(Path.Combine(modDirectory, "mod.json"), """
        {
          "id": "stack_mod",
          "displayName": "Stack Mod",
          "version": "1.0.0"
        }
        """);
        File.WriteAllText(Path.Combine(modDirectory, "items", "dirt_block.json"), """
        {
          "id": "dirt_block",
          "displayName": "Tiny Dirt Stack",
          "type": "PlaceableTile",
          "texture": "items/dirt_block",
          "maxStack": 12,
          "placesTile": "dirt"
        }
        """);

        var result = new GameContentLoader().LoadWithMods(_contentRoot, modsRoot);

        Assert.Equal(12, result.Database.Items.GetById("dirt_block").MaxStack);
        Assert.Contains(result.Report.Overrides, item => item.ContentKind == "item" && item.ContentId == "dirt_block");
        Assert.False(result.Report.HasErrors);
    }

    [Fact]
    public void LoadWithMods_ReportsTileNumericIdConflicts()
    {
        WriteMinimalBaseContent(_contentRoot);
        var modsRoot = Path.Combine(_contentRoot, "Mods");
        var modDirectory = Path.Combine(modsRoot, "BadTileMod");
        Directory.CreateDirectory(Path.Combine(modDirectory, "tiles"));
        File.WriteAllText(Path.Combine(modDirectory, "tiles", "mud.json"), """
        {
          "id": "mud",
          "numericId": 1,
          "displayName": "Mud Block",
          "texture": "tiles/mud",
          "solid": true,
          "blocksLight": true,
          "hardness": 1.0,
          "miningPowerRequired": 0
        }
        """);

        var result = new GameContentLoader().LoadWithMods(_contentRoot, modsRoot);

        Assert.True(result.Report.HasErrors);
        Assert.Contains(result.Report.Issues, issue => issue.ContentKind == "tile" && issue.ContentId == "mud");
        Assert.False(result.Database.Tiles.TryGetById("mud", out _));
    }

    [Fact]
    public void LoadWithMods_ReportsCrossRegistryReferenceErrors()
    {
        Directory.CreateDirectory(Path.Combine(_contentRoot, "tiles"));
        Directory.CreateDirectory(Path.Combine(_contentRoot, "items"));
        File.WriteAllText(Path.Combine(_contentRoot, "tiles", "dirt.json"), """
        {
          "id": "dirt",
          "numericId": 1,
          "displayName": "Dirt Block",
          "texture": "tiles/dirt",
          "solid": true,
          "blocksLight": true,
          "hardness": 1.0,
          "miningPowerRequired": 0
        }
        """);
        File.WriteAllText(Path.Combine(_contentRoot, "items", "bad_block.json"), """
        {
          "id": "bad_block",
          "displayName": "Bad Block",
          "type": "PlaceableTile",
          "texture": "items/bad_block",
          "maxStack": 999,
          "placesTile": "ghost_tile"
        }
        """);

        var result = new GameContentLoader().LoadWithMods(_contentRoot, modsRoot: null);

        Assert.True(result.Report.HasErrors);
        Assert.Contains(result.Report.Issues, issue =>
            issue.Severity == ContentIssueSeverity.Error &&
            issue.ContentKind == "item" &&
            issue.ContentId == "bad_block");
    }

    [Fact]
    public void LoadWithMods_ReportsMissingSpriteReferencesWhenAssetManifestExists()
    {
        WriteMinimalBaseContent(_contentRoot);
        Directory.CreateDirectory(Path.Combine(_contentRoot, "assets"));
        File.WriteAllText(Path.Combine(_contentRoot, "assets", "sprites.json"), """
        {
          "sprites": [
            { "id": "tiles/dirt", "path": "sprites/world/tiles/dirt.png", "category": "Tile", "width": 16, "height": 16 }
          ]
        }
        """);

        var result = new GameContentLoader().LoadWithMods(_contentRoot, modsRoot: null);

        Assert.True(result.Report.HasErrors);
        Assert.Contains(result.Report.Issues, issue =>
            issue.ContentKind == "item" &&
            issue.ContentId == "dirt_block" &&
            issue.Message.Contains("sprite asset", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void LoadWithMods_ReportsMissingItemStatusEffectReferences()
    {
        Directory.CreateDirectory(Path.Combine(_contentRoot, "tiles"));
        Directory.CreateDirectory(Path.Combine(_contentRoot, "items"));
        File.WriteAllText(Path.Combine(_contentRoot, "tiles", "dirt.json"), """
        {
          "id": "dirt",
          "numericId": 1,
          "displayName": "Dirt Block",
          "texture": "tiles/dirt",
          "solid": true,
          "blocksLight": true,
          "hardness": 1.0,
          "miningPowerRequired": 0
        }
        """);
        File.WriteAllText(Path.Combine(_contentRoot, "items", "bad_sword.json"), """
        {
          "id": "bad_sword",
          "displayName": "Bad Sword",
          "type": "WeaponMelee",
          "texture": "items/bad_sword",
          "maxStack": 1,
          "onHitEffects": [
            { "effect": "missing_poison" }
          ]
        }
        """);

        var result = new GameContentLoader().LoadWithMods(_contentRoot, modsRoot: null);

        Assert.True(result.Report.HasErrors);
        Assert.Contains(result.Report.Issues, issue =>
            issue.ContentKind == "item" &&
            issue.ContentId == "bad_sword" &&
            issue.Message.Contains("status effect", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void LoadWithMods_ReportsMissingWorldgenOreTileReferences()
    {
        WriteMinimalBaseContent(_contentRoot);
        Directory.CreateDirectory(Path.Combine(_contentRoot, "worldgen"));
        File.WriteAllText(Path.Combine(_contentRoot, "worldgen", "bad_ores.json"), """
        {
          "id": "bad_ores",
          "widthTiles": 64,
          "heightTiles": 64,
          "surfaceBaseY": 24,
          "surfaceAmplitude": 3,
          "dirtDepthMin": 4,
          "dirtDepthMax": 8,
          "ores": [
            { "tileId": 99, "veinCount": 2, "replaceTileId": 1 }
          ]
        }
        """);

        var result = new GameContentLoader().LoadWithMods(_contentRoot, modsRoot: null);

        Assert.True(result.Report.HasErrors);
        Assert.Contains(result.Report.Issues, issue =>
            issue.ContentKind == "worldgen" &&
            issue.ContentId == "bad_ores" &&
            issue.Message.Contains("ore tile", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void LoadWithMods_RepositoryGameDataHasNoContentErrors()
    {
        var dataRoot = FindRepositoryGameData();

        var result = new GameContentLoader().LoadWithMods(dataRoot, modsRoot: null);

        Assert.False(result.Report.HasErrors, string.Join(Environment.NewLine, result.Report.Issues.Select(issue => issue.Message)));
        Assert.True(result.Database.SpriteAssets.HasExplicitAssets);
        Assert.True(result.Database.Tiles.TryGetById("workbench", out var workbench));
        Assert.Equal("workbench", workbench.CraftingStationId);
        Assert.True(result.Database.WorldGenerationProfiles.TryGetById("small", out var smallProfile));
        Assert.True(result.Database.WorldGenerationProfiles.TryGetById("medium", out _));
        Assert.True(result.Database.WorldGenerationProfiles.TryGetById("large", out _));
        Assert.NotEmpty(smallProfile.Ores);
        Assert.True(result.Database.Items.TryGetById("copper_chestplate", out var copperChestplate));
        Assert.Equal(Game.Core.Equipment.EquipmentSlotType.Body, copperChestplate.EquipmentSlot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_contentRoot))
        {
            Directory.Delete(_contentRoot, recursive: true);
        }
    }

    private static void WriteMinimalBaseContent(string contentRoot)
    {
        Directory.CreateDirectory(Path.Combine(contentRoot, "tiles"));
        Directory.CreateDirectory(Path.Combine(contentRoot, "items"));

        File.WriteAllText(Path.Combine(contentRoot, "tiles", "dirt.json"), """
        {
          "id": "dirt",
          "numericId": 1,
          "displayName": "Dirt Block",
          "texture": "tiles/dirt",
          "solid": true,
          "blocksLight": true,
          "hardness": 1.0,
          "miningPowerRequired": 0
        }
        """);

        File.WriteAllText(Path.Combine(contentRoot, "items", "dirt_block.json"), """
        {
          "id": "dirt_block",
          "displayName": "Dirt Block",
          "type": "PlaceableTile",
          "texture": "items/dirt_block",
          "maxStack": 999,
          "placesTile": "dirt"
        }
        """);
    }

    private static string FindRepositoryGameData()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "Game.Data");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository Game.Data directory.");
    }
}
