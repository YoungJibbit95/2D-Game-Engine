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
        Directory.CreateDirectory(Path.Combine(_contentRoot, "tiles"));
        Directory.CreateDirectory(Path.Combine(_contentRoot, "items"));
        Directory.CreateDirectory(Path.Combine(_contentRoot, "recipes"));
        Directory.CreateDirectory(Path.Combine(_contentRoot, "loot"));
        Directory.CreateDirectory(Path.Combine(_contentRoot, "biomes"));
        Directory.CreateDirectory(Path.Combine(_contentRoot, "projectiles"));
        Directory.CreateDirectory(Path.Combine(_contentRoot, "entities"));

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

        var database = new GameContentLoader().LoadFromRoot(_contentRoot);

        Assert.Equal("dirt", database.Tiles.GetByNumericId(1).Id);
        Assert.Equal("dirt", database.Items.GetById("dirt_block").PlacesTileId);
        Assert.True(database.Recipes.TryGetById("stone_from_dirt", out _));
        Assert.True(database.LootTables.TryGetById("slime_basic", out _));
        Assert.True(database.Biomes.TryGetById("forest", out _));
        Assert.True(database.Projectiles.TryGetById("wooden_arrow", out _));
        Assert.True(database.Entities.TryGetById("slime", out _));
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
}
