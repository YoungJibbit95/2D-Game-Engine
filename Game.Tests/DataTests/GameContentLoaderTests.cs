using Game.Core.Data;
using Game.Core.Mods;
using Xunit;

namespace Game.Tests.DataTests;

public sealed class GameContentLoaderTests : IDisposable
{
    private readonly string _contentRoot = Path.Combine(Path.GetTempPath(), "yjse-content-tests", Guid.NewGuid().ToString("N"));

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
        Directory.CreateDirectory(Path.Combine(_contentRoot, "crops"));
        Directory.CreateDirectory(Path.Combine(_contentRoot, "maps"));
        Directory.CreateDirectory(Path.Combine(_contentRoot, "dialogue"));
        Directory.CreateDirectory(Path.Combine(_contentRoot, "shops"));
        Directory.CreateDirectory(Path.Combine(_contentRoot, "startup"));
        Directory.CreateDirectory(Path.Combine(_contentRoot, "animations"));
        Directory.CreateDirectory(Path.Combine(_contentRoot, "characters"));

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

        File.WriteAllText(Path.Combine(_contentRoot, "items", "parsnip_seeds.json"), """
        {
          "id": "parsnip_seeds",
          "displayName": "Parsnip Seeds",
          "type": "Seed",
          "texture": "items/parsnip_seeds",
          "maxStack": 999
        }
        """);

        File.WriteAllText(Path.Combine(_contentRoot, "items", "parsnip.json"), """
        {
          "id": "parsnip",
          "displayName": "Parsnip",
          "type": "Consumable",
          "texture": "items/parsnip",
          "maxStack": 999
        }
        """);

        File.WriteAllText(Path.Combine(_contentRoot, "items", "copper_coin.json"), """
        {
          "id": "copper_coin",
          "displayName": "Copper Coin",
          "type": "Material",
          "texture": "items/copper_coin",
          "maxStack": 999
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

        File.WriteAllText(Path.Combine(_contentRoot, "crops", "parsnip.json"), """
        {
          "id": "parsnip",
          "displayName": "Parsnip",
          "texture": "crops/parsnip",
          "seedItem": "parsnip_seeds",
          "harvestItem": "parsnip",
          "growthStageDays": [1, 1, 1],
          "seasons": ["Spring"]
        }
        """);

        File.WriteAllText(Path.Combine(_contentRoot, "maps", "farm.json"), """
        {
          "id": "farm",
          "displayName": "Farm",
          "widthTiles": 3,
          "heightTiles": 2,
          "layers": [
            { "id": "ground", "kind": "Ground", "width": 3, "height": 2, "tiles": [1,1,1,1,1,1] }
          ],
          "objects": [
            { "id": "door", "kind": "Warp", "tileX": 2, "tileY": 1, "targetMapId": "farm", "targetSpawnId": "home" },
            { "id": "farmer", "kind": "NpcSpawn", "tileX": 1, "tileY": 0, "isInteractable": true, "properties": { "dialogueId": "farm_intro" } },
            { "id": "seed_shop", "kind": "Shop", "tileX": 0, "tileY": 1, "isInteractable": true, "properties": { "shopId": "seed_shop" } }
          ],
          "spawnPoints": [
            { "id": "home", "tileX": 1, "tileY": 1 }
          ]
        }
        """);

        File.WriteAllText(Path.Combine(_contentRoot, "dialogue", "farm_intro.json"), """
        {
          "id": "farm_intro",
          "displayName": "Farm Intro",
          "startNodeId": "hello",
          "nodes": [
            { "id": "hello", "speakerId": "farmer", "text": "Welcome.", "endsConversation": true }
          ]
        }
        """);

        File.WriteAllText(Path.Combine(_contentRoot, "shops", "seed_shop.json"), """
        {
          "id": "seed_shop",
          "displayName": "Seed Shop",
          "currencyItemId": "copper_coin",
          "stock": [
            { "itemId": "parsnip_seeds", "count": 1, "price": 5 }
          ],
          "sellPrices": [
            { "itemId": "parsnip", "price": 8 }
          ]
        }
        """);

        File.WriteAllText(Path.Combine(_contentRoot, "animations", "player.json"), """
        {
          "animations": [
            { "id": "player.idle", "sprite": "entities/player/base_actions", "frameStart": 0, "frameCount": 1, "frameDuration": 0.2 }
          ]
        }
        """);

        File.WriteAllText(Path.Combine(_contentRoot, "characters", "player.json"), """
        {
          "id": "player",
          "displayName": "Player",
          "defaultAppearance": { "bodySpriteId": "entities/player/base_actions" },
          "animationSet": {
            "id": "player.default",
            "states": { "Idle": "player.idle" }
          }
        }
        """);

        File.WriteAllText(Path.Combine(_contentRoot, "startup", "default.json"), """
        {
          "id": "default",
          "displayName": "Default Start",
          "worldProfileId": "tiny",
          "startupMapId": "farm",
          "selectedHotbarSlot": 0,
          "starterItems": [
            { "itemId": "dirt_block", "count": 10, "target": "Hotbar", "slot": 0 },
            { "itemId": "parsnip_seeds", "count": 3, "target": "Hotbar", "slot": 1 }
          ]
        }
        """);

        File.WriteAllText(Path.Combine(_contentRoot, "assets", "sprites.json"), """
        {
          "sprites": [
            { "id": "tiles/dirt", "path": "sprites/world/tiles/dirt.png", "category": "Tile", "width": 16, "height": 16 },
            { "id": "items/dirt_block", "path": "sprites/items/blocks/dirt_block.png", "category": "Item", "width": 16, "height": 16 },
            { "id": "items/copper_coin", "path": "sprites/items/currency/copper_coin.png", "category": "Item", "width": 16, "height": 16 },
            { "id": "items/parsnip_seeds", "path": "sprites/items/seeds/parsnip_seeds.png", "category": "Item", "width": 16, "height": 16 },
            { "id": "items/parsnip", "path": "sprites/items/crops/parsnip.png", "category": "Item", "width": 16, "height": 16 },
            { "id": "crops/parsnip", "path": "sprites/world/crops/parsnip.png", "category": "Crop", "width": 48, "height": 16 },
            { "id": "projectiles/wooden_arrow", "path": "sprites/projectiles/wooden_arrow.png", "category": "Projectile", "width": 16, "height": 16 },
            { "id": "entities/slime", "path": "sprites/entities/slime.png", "category": "Entity", "width": 16, "height": 16 },
            { "id": "entities/player/base_actions", "path": "sprites/entities/player/base_actions.png", "category": "Entity", "width": 16, "height": 32 }
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
        Assert.True(database.Crops.TryGetBySeedItemId("parsnip_seeds", out var parsnip));
        Assert.Equal(3, parsnip.TotalGrowthDays);
        Assert.True(database.Maps.TryGetById("farm", out var farm));
        Assert.True(farm.TryGetSpawn("home", out _));
        Assert.True(database.Dialogues.TryGetById("farm_intro", out var intro));
        Assert.Equal("hello", intro.StartNodeId);
        Assert.True(database.Shops.TryGetById("seed_shop", out var shop));
        Assert.True(shop.TryGetStock("parsnip_seeds", out _));
        Assert.True(database.GameStartups.TryGetDefault("default", out var startup));
        Assert.Equal("tiny", startup.WorldProfileId);
        Assert.Equal(2, startup.StarterItems.Count);
        Assert.True(database.Animations.TryGetById("player.idle", out _));
        Assert.True(database.Characters.TryGetById("player", out var player));
        Assert.Equal("player.idle", player.AnimationSet.ResolveClipId(Game.Core.Characters.CharacterAnimationState.Idle));
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
    public void LoadWithMods_ReportsMissingShopDialogueAndShopItemReferences()
    {
        WriteMinimalBaseContent(_contentRoot);
        Directory.CreateDirectory(Path.Combine(_contentRoot, "maps"));
        Directory.CreateDirectory(Path.Combine(_contentRoot, "shops"));
        Directory.CreateDirectory(Path.Combine(_contentRoot, "startup"));
        Directory.CreateDirectory(Path.Combine(_contentRoot, "animations"));
        Directory.CreateDirectory(Path.Combine(_contentRoot, "characters"));

        File.WriteAllText(Path.Combine(_contentRoot, "maps", "bad_refs.json"), """
        {
          "id": "bad_refs",
          "displayName": "Bad References",
          "widthTiles": 3,
          "heightTiles": 3,
          "objects": [
            { "id": "shop", "kind": "Shop", "tileX": 1, "tileY": 1, "isInteractable": true, "properties": { "shopId": "missing_shop" } },
            { "id": "npc", "kind": "NpcSpawn", "tileX": 2, "tileY": 1, "isInteractable": true, "properties": { "dialogueId": "missing_dialogue" } }
          ],
          "spawnPoints": [
            { "id": "home", "tileX": 1, "tileY": 2 }
          ]
        }
        """);

        File.WriteAllText(Path.Combine(_contentRoot, "shops", "bad_shop.json"), """
        {
          "id": "bad_shop",
          "displayName": "Bad Shop",
          "currencyItemId": "missing_coin",
          "stock": [
            { "itemId": "missing_seed", "count": 1, "price": 5 }
          ],
          "sellPrices": [
            { "itemId": "missing_crop", "price": 8 }
          ]
        }
        """);

        File.WriteAllText(Path.Combine(_contentRoot, "startup", "bad_start.json"), """
        {
          "id": "bad_start",
          "displayName": "Bad Start",
          "worldProfileId": "missing_profile",
          "startupMapId": "missing_map",
          "starterItems": [
            { "itemId": "missing_start_item", "count": 1 }
          ]
        }
        """);

        var result = new GameContentLoader().LoadWithMods(_contentRoot, modsRoot: null);

        Assert.True(result.Report.HasErrors);
        Assert.Contains(result.Report.Issues, issue => issue.ContentKind == "map" && issue.Message.Contains("shop", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Report.Issues, issue => issue.ContentKind == "map" && issue.Message.Contains("dialogue", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Report.Issues, issue => issue.ContentKind == "shop" && issue.Message.Contains("currency item", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Report.Issues, issue => issue.ContentKind == "shop" && issue.Message.Contains("stock item", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Report.Issues, issue => issue.ContentKind == "shop" && issue.Message.Contains("sell item", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Report.Issues, issue => issue.ContentKind == "startup" && issue.Message.Contains("world profile", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Report.Issues, issue => issue.ContentKind == "startup" && issue.Message.Contains("startup map", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Report.Issues, issue => issue.ContentKind == "startup" && issue.Message.Contains("starter item", StringComparison.OrdinalIgnoreCase));
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
        Assert.False(result.Database.WorldGenerationProfiles.TryGetById("living_world", out _));
        Assert.True(result.Database.RegionalGenerationProfiles.TryGetById("living_world", out var livingWorld));
        Assert.NotEmpty(livingWorld.BiomeLayers);
        Assert.Equal(7, result.Database.StructurePlans.Definitions.Count);
        Assert.NotEmpty(smallProfile.Ores);
        Assert.True(result.Database.Items.TryGetById("copper_chestplate", out var copperChestplate));
        Assert.Equal(Game.Core.Equipment.EquipmentSlotType.Body, copperChestplate.EquipmentSlot);
        Assert.True(result.Database.Items.TryGetById("healing_potion", out var healingPotion));
        Assert.Equal(50, healingPotion.HealthRestore);
        Assert.False(string.IsNullOrWhiteSpace(healingPotion.Description));
        Assert.True(healingPotion.Value > 0);
        Assert.True(result.Database.Tiles.TryGetById("furnace", out var furnace));
        Assert.Equal("furnace", furnace.CraftingStationId);
        Assert.True(result.Database.Tiles.TryGetById("anvil", out var anvil));
        Assert.Equal("anvil", anvil.CraftingStationId);
        Assert.Equal("workbench", result.Database.Recipes.GetById("furnace").Station);
        Assert.Equal("furnace", result.Database.Recipes.GetById("anvil").Station);
        Assert.Equal(4, result.Database.Recipes.GetById("torch").Result.Count);
        Assert.True(result.Database.Crops.TryGetBySeedItemId("parsnip_seeds", out var parsnip));
        Assert.True(parsnip.CanGrowIn(Game.Core.Farming.FarmSeason.Spring));
        Assert.True(result.Database.Maps.TryGetById("farmstead", out var farmstead));
        Assert.Contains(farmstead.Objects, item => item.Kind == Game.Core.Maps.MapObjectKind.FarmArea);
        Assert.True(result.Database.Dialogues.TryGetById("farm_welcome", out _));
        Assert.True(result.Database.Shops.TryGetById("seed_shop", out var seedShop));
        Assert.Contains(seedShop.Stock, item => item.ItemId == "parsnip_seeds");
        Assert.True(result.Database.GameStartups.TryGetDefault("default", out var startup));
        Assert.Contains(startup.StarterItems, item => item.ItemId == "copper_pickaxe");
        Assert.True(result.Database.Animations.TryGetById("player.walk", out _));
        Assert.NotNull(result.Database.RuntimeAnimations);
        Assert.True(result.Database.RuntimeAnimations.TryGetCharacter("player.wave06", out var runtimePlayer));
        Assert.Equal(5, runtimePlayer.Rig.Layers.Count);
        Assert.Equal(17, result.Database.RuntimeAnimations.Entities.Count);
        Assert.Equal(
            new[] { "cave_glowbug", "forest_moth", "meadow_butterfly" },
            result.Database.RuntimeAnimations.Entities
                .Where(profile => profile.Id is "cave_glowbug" or "forest_moth" or "meadow_butterfly")
                .Select(profile => profile.Id)
                .ToArray());
        Assert.True(result.Database.Characters.TryGetById("player", out var player));
        Assert.Equal("player.idle", player.AnimationSet.ResolveClipId(Game.Core.Characters.CharacterAnimationState.Idle));
        Assert.True(result.Database.WorldEvents.TryGetById("firefly_bloom", out var fireflyBloom));
        Assert.Equal(3, fireflyBloom.Phases.Count);
        Assert.True(result.Database.WorldEvents.TryGetById("crystal_surge", out _));
        Assert.True(result.Database.Biomes.TryGetById("amber_grove", out var amberGrove));
        Assert.Equal("world/backgrounds/amber_grove_parallax_layer_v5", amberGrove.Presentation.BackgroundSpriteId);
        Assert.True(result.Database.Biomes.TryGetById("twilight_marsh", out _));
        Assert.True(result.Database.Biomes.TryGetById("frostwood", out var frostwood));
        Assert.True(frostwood.Weather.AllowsFrozenPrecipitation);
        Assert.Equal("frostwood_day_rabbit", frostwood.Spawning.SurfaceDayTableId);
        Assert.True(result.Database.Tiles.TryGetById("snow", out var snow));
        Assert.Equal((ushort)21, snow.NumericId);
        Assert.True(result.Database.Tiles.TryGetById("ice", out var ice));
        Assert.Equal((ushort)22, ice.NumericId);
        Assert.True(result.Database.SpawnRules.TryGetById("frostwood_day_rabbit", out _));
        Assert.True(result.Database.SpawnRules.TryGetById("frostwood_night_bat", out _));
        Assert.True(result.Database.SpawnRules.TryGetById("frostwood_cave_bat", out _));
        Assert.Contains(livingWorld.Features, feature => feature.Id == "frostwood_pine_groves");
        Assert.Equal("world/tiles/polish_v1/forest_grass_loam_autotile", result.Database.Tiles.GetById("grass").TexturePath);
        Assert.Equal("world/tiles/polish_v1/forest_loam_autotile", result.Database.Tiles.GetById("dirt").TexturePath);
        Assert.Equal("world/tiles/polish_v1/layered_stone_autotile", result.Database.Tiles.GetById("stone").TexturePath);
        Assert.Equal("world/tiles/polish_v1/amberstone_autotile", result.Database.Tiles.GetById("amberstone").TexturePath);
        Assert.Equal("world/tiles/polish_v1/marsh_moss_autotile", result.Database.Tiles.GetById("marsh_moss").TexturePath);
        Assert.Equal("world/tiles/polish_v1/amberwood_plank_autotile", result.Database.Tiles.GetById("amberwood_plank").TexturePath);
        Assert.Equal("world/tiles/polish_v1/amberstone_autotile", result.Database.Items.GetById("amberstone_block").TexturePath);
        Assert.Equal("world/tiles/polish_v1/marsh_moss_autotile", result.Database.Items.GetById("marsh_moss_block").TexturePath);
        Assert.Equal("world/tiles/polish_v1/amberwood_plank_autotile", result.Database.Items.GetById("amberwood_plank_block").TexturePath);
        Assert.False(result.Database.Tiles.GetById("mangrove_root").Solid);
        Assert.Equal(70, result.Database.Items.GetById("sunsteel_pickaxe").ToolPower);
        Assert.Equal("anvil", result.Database.Recipes.GetById("mirror_shield").Station);
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
            if (File.Exists(Path.Combine(directory.FullName, "YjsE.sln")) &&
                Directory.Exists(Path.Combine(candidate, "tiles")) &&
                Directory.Exists(Path.Combine(candidate, "assets")))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository Game.Data directory.");
    }
}
