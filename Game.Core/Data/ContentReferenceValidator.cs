using Game.Core.Mods;
using Game.Core.Items;
using Game.Core.Effects;
using Game.Core.Maps;
using Game.Core.Startup;
using Game.Core.Combat;

namespace Game.Core.Data;

public sealed class ContentReferenceValidator
{
    public void Validate(GameContentDatabase database, ContentLoadReport report)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(report);

        ValidateTiles(database, report);
        ValidateItems(database, report);
        ValidateRecipes(database, report);
        ValidateLootTables(database, report);
        ValidateBiomes(database, report);
        ValidateProjectiles(database, report);
        ValidateEntities(database, report);
        ValidateSpawnRules(database, report);
        ValidateEncounters(database, report);
        ValidateCrops(database, report);
        ValidateMaps(database, report);
        ValidateShops(database, report);
        ValidateGameStartups(database, report);
        ValidateAnimations(database, report);
        ValidateCharacters(database, report);
        ValidateWorldGenerationProfiles(database, report);
        ValidateWorldEvents(database, report);
        ValidateAttackSequences(database, report);
        ValidateSpriteReferences(database, report);
    }

    private static void ValidateTiles(GameContentDatabase database, ContentLoadReport report)
    {
        foreach (var tile in database.Tiles.Definitions)
        {
            if (string.IsNullOrWhiteSpace(tile.DropItemId))
            {
                continue;
            }

            if (!database.Items.TryGetById(tile.DropItemId, out _))
            {
                AddMissingReference(report, "tile", tile.Id, "drop item", tile.DropItemId);
            }
        }
    }

    private static void ValidateItems(GameContentDatabase database, ContentLoadReport report)
    {
        foreach (var item in database.Items.Definitions)
        {
            if (!string.IsNullOrWhiteSpace(item.PlacesTileId) && !database.Tiles.TryGetById(item.PlacesTileId, out _))
            {
                AddMissingReference(report, "item", item.Id, "placed tile", item.PlacesTileId);
            }

            foreach (var action in item.Actions)
            {
                ValidateItemAction(database, report, item.Id, action);
            }

            ValidateStatusEffects(database, report, "item", item.Id, item.OnHitEffects);
        }
    }

    private static void ValidateItemAction(
        GameContentDatabase database,
        ContentLoadReport report,
        string itemId,
        ItemActionDefinition action)
    {
        if (!string.IsNullOrWhiteSpace(action.ProjectileId) && !database.Projectiles.TryGetById(action.ProjectileId, out _))
        {
            AddMissingReference(report, "item", itemId, "projectile", action.ProjectileId);
        }

        if (!string.IsNullOrWhiteSpace(action.AmmoItemId) && !database.Items.TryGetById(action.AmmoItemId, out _))
        {
            AddMissingReference(report, "item", itemId, "ammo item", action.AmmoItemId);
        }

        if (!string.IsNullOrWhiteSpace(action.AttackSequenceId) &&
            !database.AttackSequences.TryGetById(action.AttackSequenceId, out _))
        {
            AddMissingReference(report, "item", itemId, "attack sequence", action.AttackSequenceId);
        }
    }

    private static void ValidateAttackSequences(GameContentDatabase database, ContentLoadReport report)
    {
        foreach (var sequence in database.AttackSequences.Definitions)
        {
            foreach (var step in sequence.Steps)
            {
                var ammoItemId = step.Cost.AmmoItemId;
                if (!string.IsNullOrWhiteSpace(ammoItemId) && !database.Items.TryGetById(ammoItemId, out _))
                {
                    AddMissingReference(
                        report,
                        "attack-sequence",
                        sequence.Id,
                        $"ammo item for step '{step.Id}'",
                        ammoItemId);
                }
            }
        }
    }

    private static void ValidateRecipes(GameContentDatabase database, ContentLoadReport report)
    {
        foreach (var recipe in database.Recipes.Definitions)
        {
            if (!database.Items.TryGetById(recipe.Result.ItemId, out _))
            {
                AddMissingReference(report, "recipe", recipe.Id, "result item", recipe.Result.ItemId);
            }

            foreach (var ingredient in recipe.Ingredients)
            {
                if (!database.Items.TryGetById(ingredient.ItemId, out _))
                {
                    AddMissingReference(report, "recipe", recipe.Id, "ingredient item", ingredient.ItemId);
                }
            }

            if (!string.IsNullOrWhiteSpace(recipe.Station) &&
                !database.Tiles.Definitions.Any(tile => string.Equals(tile.CraftingStationId, recipe.Station, StringComparison.OrdinalIgnoreCase)))
            {
                report.AddIssue(
                    ContentIssueSeverity.Warning,
                    "validation",
                    "recipe",
                    recipe.Id,
                    $"Recipe requires station '{recipe.Station}', but no tile currently provides that station.");
            }
        }
    }

    private static void ValidateLootTables(GameContentDatabase database, ContentLoadReport report)
    {
        foreach (var table in database.LootTables.Definitions)
        {
            foreach (var entry in table.Entries)
            {
                if (!database.Items.TryGetById(entry.ItemId, out _))
                {
                    AddMissingReference(report, "loot", table.Id, "entry item", entry.ItemId);
                }
            }
        }
    }

    private static void ValidateBiomes(GameContentDatabase database, ContentLoadReport report)
    {
        foreach (var biome in database.Biomes.Definitions)
        {
            if (!database.Tiles.TryGetById(biome.SurfaceTile, out _))
            {
                AddMissingReference(report, "biome", biome.Id, "surface tile", biome.SurfaceTile);
            }

            if (!database.Tiles.TryGetById(biome.UndergroundTile, out _))
            {
                AddMissingReference(report, "biome", biome.Id, "underground tile", biome.UndergroundTile);
            }

            if (!string.IsNullOrWhiteSpace(biome.TreeType))
            {
                if (!database.Tiles.TryGetById(biome.TreeMaterial.TrunkTile, out _))
                {
                    AddMissingReference(
                        report,
                        "biome",
                        biome.Id,
                        $"tree type '{biome.TreeType}' trunk tile",
                        biome.TreeMaterial.TrunkTile);
                }

                if (!database.Tiles.TryGetById(biome.TreeMaterial.CanopyTile, out _))
                {
                    AddMissingReference(
                        report,
                        "biome",
                        biome.Id,
                        $"tree type '{biome.TreeType}' canopy tile",
                        biome.TreeMaterial.CanopyTile);
                }
            }

            ValidateOptionalSpawnRule(database, report, biome.Id, "enemy spawn table", biome.EnemySpawnTable);
            ValidateOptionalSpawnRule(database, report, biome.Id, "surface day spawn table", biome.Spawning.SurfaceDayTableId);
            ValidateOptionalSpawnRule(database, report, biome.Id, "surface night spawn table", biome.Spawning.SurfaceNightTableId);
            ValidateOptionalSpawnRule(database, report, biome.Id, "cave spawn table", biome.Spawning.CaveTableId);

            if (database.SpriteAssets.HasExplicitAssets)
            {
                RequireOptionalSprite(database, report, "biome", biome.Id, biome.Presentation.BackgroundSpriteId);
                RequireOptionalSprite(database, report, "biome", biome.Id, biome.Presentation.AmbientParticleSpriteId);
                RequireOptionalSprite(database, report, "biome", biome.Id, biome.Presentation.AmbientCritterSpriteId);
                RequireOptionalSprite(database, report, "biome", biome.Id, biome.Presentation.BiomeIconSpriteId);
                RequireOptionalSprite(database, report, "biome", biome.Id, biome.Presentation.EliteSpriteId);
            }
        }
    }

    private static void ValidateEntities(GameContentDatabase database, ContentLoadReport report)
    {
        foreach (var entity in database.Entities.Definitions)
        {
            if (!string.IsNullOrWhiteSpace(entity.LootTableId) && !database.LootTables.TryGetById(entity.LootTableId, out _))
            {
                AddMissingReference(report, "entity", entity.Id, "loot table", entity.LootTableId);
            }

            ValidateStatusEffects(database, report, "entity", entity.Id, entity.OnContactEffects);
        }
    }

    private static void ValidateProjectiles(GameContentDatabase database, ContentLoadReport report)
    {
        foreach (var projectile in database.Projectiles.Definitions)
        {
            ValidateStatusEffects(database, report, "projectile", projectile.Id, projectile.OnHitEffects);
        }
    }

    private static void ValidateSpawnRules(GameContentDatabase database, ContentLoadReport report)
    {
        var knownWorldEvents = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var definition in database.WorldEvents.Definitions)
        {
            knownWorldEvents.Add(definition.Id);
        }

        foreach (var profile in database.RegionalGenerationProfiles.Profiles)
        {
            foreach (var definition in profile.WorldEvents)
            {
                knownWorldEvents.Add(definition.Id);
            }
        }

        foreach (var rule in database.SpawnRules.Definitions)
        {
            if (!database.Entities.TryGetById(rule.EntityId, out _))
            {
                AddMissingReference(report, "spawn", rule.Id, "entity", rule.EntityId);
            }

            if (!string.IsNullOrWhiteSpace(rule.BiomeId) && !database.Biomes.TryGetById(rule.BiomeId, out _))
            {
                AddMissingReference(report, "spawn", rule.Id, "biome", rule.BiomeId);
            }

            foreach (var eventId in rule.WorldEventWeights.Keys)
            {
                if (!string.Equals(eventId, "none", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(eventId, "*", StringComparison.OrdinalIgnoreCase) &&
                    !knownWorldEvents.Contains(eventId))
                {
                    AddMissingReference(report, "spawn", rule.Id, "world event", eventId);
                }
            }
        }
    }

    private static void ValidateEncounters(GameContentDatabase database, ContentLoadReport report)
    {
        foreach (var encounter in database.Encounters.Definitions)
        {
            foreach (var biomeId in encounter.BiomeIds)
            {
                if (!database.Biomes.TryGetById(biomeId, out _))
                {
                    AddMissingReference(report, "encounter", encounter.Id, "biome", biomeId);
                }
            }

            foreach (var role in encounter.Roles)
            {
                if (!database.SpawnRules.TryGetById(role.SpawnRuleId, out var spawnRule))
                {
                    AddMissingReference(report, "encounter", encounter.Id, $"spawn rule for role '{role.Id}'", role.SpawnRuleId);
                    continue;
                }

                if (!database.Entities.TryGetById(spawnRule.EntityId, out var entity))
                {
                    continue;
                }

                if (!HasActiveAiRule(entity))
                {
                    report.AddIssue(
                        ContentIssueSeverity.Error,
                        "validation",
                        "encounter",
                        encounter.Id,
                        $"Role '{role.Id}' spawn rule '{spawnRule.Id}' references entity '{entity.Id}' without an active AI rule.");
                }
            }
        }
    }

    private static bool HasActiveAiRule(Game.Core.Entities.EntityDefinition entity)
    {
        if (entity.Ai is { Kind: not Game.Core.Entities.AI.AiBehaviorKind.None })
        {
            return true;
        }

        return entity.AiBehavior?.ToLowerInvariant() is
            "slime" or "critter" or "wander" or "flee" or "hostile" or "patrol" or "chase";
    }

    private static void ValidateCrops(GameContentDatabase database, ContentLoadReport report)
    {
        foreach (var crop in database.Crops.Definitions)
        {
            if (!database.Items.TryGetById(crop.SeedItemId, out _))
            {
                AddMissingReference(report, "crop", crop.Id, "seed item", crop.SeedItemId);
            }

            if (!database.Items.TryGetById(crop.HarvestItemId, out _))
            {
                AddMissingReference(report, "crop", crop.Id, "harvest item", crop.HarvestItemId);
            }
        }
    }

    private static void ValidateMaps(GameContentDatabase database, ContentLoadReport report)
    {
        foreach (var map in database.Maps.Definitions)
        {
            foreach (var mapObject in map.Objects)
            {
                ValidateMapObjectActionReferences(database, report, map.Id, mapObject);

                if (string.IsNullOrWhiteSpace(mapObject.TargetMapId))
                {
                    continue;
                }

                if (!database.Maps.TryGetById(mapObject.TargetMapId, out var targetMap))
                {
                    AddMissingReference(report, "map", map.Id, "target map", mapObject.TargetMapId);
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(mapObject.TargetSpawnId) &&
                    !targetMap.TryGetSpawn(mapObject.TargetSpawnId, out _))
                {
                    AddMissingReference(report, "map", map.Id, "target spawn", $"{mapObject.TargetMapId}:{mapObject.TargetSpawnId}");
                }
            }
        }
    }

    private static void ValidateMapObjectActionReferences(
        GameContentDatabase database,
        ContentLoadReport report,
        string mapId,
        MapObjectDefinition mapObject)
    {
        if (TryResolveProperty(mapObject, out var shopId, "shopId", "storeId") &&
            !database.Shops.TryGetById(shopId, out _))
        {
            AddMissingReference(report, "map", mapId, "shop", shopId);
        }

        if (TryResolveProperty(mapObject, out var dialogueId, "dialogueId") &&
            !database.Dialogues.TryGetById(dialogueId, out _))
        {
            AddMissingReference(report, "map", mapId, "dialogue", dialogueId);
        }
    }

    private static void ValidateShops(GameContentDatabase database, ContentLoadReport report)
    {
        foreach (var shop in database.Shops.Definitions)
        {
            if (!database.Items.TryGetById(shop.CurrencyItemId, out _))
            {
                AddMissingReference(report, "shop", shop.Id, "currency item", shop.CurrencyItemId);
            }

            foreach (var stock in shop.Stock)
            {
                if (!database.Items.TryGetById(stock.ItemId, out _))
                {
                    AddMissingReference(report, "shop", shop.Id, "stock item", stock.ItemId);
                }

                if (!string.IsNullOrWhiteSpace(stock.CurrencyItemId) &&
                    !database.Items.TryGetById(stock.CurrencyItemId, out _))
                {
                    AddMissingReference(report, "shop", shop.Id, "stock currency item", stock.CurrencyItemId);
                }
            }

            foreach (var sellPrice in shop.SellPrices)
            {
                if (!database.Items.TryGetById(sellPrice.ItemId, out _))
                {
                    AddMissingReference(report, "shop", shop.Id, "sell item", sellPrice.ItemId);
                }

                if (!string.IsNullOrWhiteSpace(sellPrice.CurrencyItemId) &&
                    !database.Items.TryGetById(sellPrice.CurrencyItemId, out _))
                {
                    AddMissingReference(report, "shop", shop.Id, "sell currency item", sellPrice.CurrencyItemId);
                }
            }
        }
    }

    private static void ValidateGameStartups(GameContentDatabase database, ContentLoadReport report)
    {
        foreach (var startup in database.GameStartups.Definitions)
        {
            if (!string.IsNullOrWhiteSpace(startup.WorldProfileId) &&
                !database.WorldGenerationProfiles.TryGetById(startup.WorldProfileId, out _))
            {
                AddMissingReference(report, "startup", startup.Id, "world profile", startup.WorldProfileId);
            }

            if (!string.IsNullOrWhiteSpace(startup.StartupMapId) &&
                !database.Maps.TryGetById(startup.StartupMapId, out _))
            {
                AddMissingReference(report, "startup", startup.Id, "startup map", startup.StartupMapId);
            }

            foreach (var item in startup.StarterItems)
            {
                if (!database.Items.TryGetById(item.ItemId, out var itemDefinition))
                {
                    AddMissingReference(report, "startup", startup.Id, "starter item", item.ItemId);
                    continue;
                }

                if (item.Target != StarterInventoryTarget.Auto &&
                    item.Slot.HasValue &&
                    item.Count > itemDefinition.MaxStack)
                {
                    report.AddIssue(
                        ContentIssueSeverity.Error,
                        "validation",
                        "startup",
                        startup.Id,
                        $"Starter item '{item.ItemId}' targets one slot with count {item.Count}, but max stack is {itemDefinition.MaxStack}.");
                }
            }
        }
    }


    private static void ValidateAnimations(GameContentDatabase database, ContentLoadReport report)
    {
        if (!database.SpriteAssets.HasExplicitAssets)
        {
            return;
        }

        foreach (var animation in database.Animations.Clips)
        {
            foreach (var frame in animation.Frames)
            {
                if (!database.SpriteAssets.TryGetById(frame.SpriteId, out _))
                {
                    AddMissingReference(report, "animation", animation.Id, "sprite asset", frame.SpriteId);
                }
            }
        }
    }

    private static void ValidateCharacters(GameContentDatabase database, ContentLoadReport report)
    {
        foreach (var character in database.Characters.Definitions)
        {
            if (database.SpriteAssets.HasExplicitAssets &&
                !database.SpriteAssets.TryGetById(character.DefaultAppearance.BodySpriteId, out _))
            {
                AddMissingReference(report, "character", character.Id, "body sprite asset", character.DefaultAppearance.BodySpriteId);
            }

            foreach (var clipId in character.AnimationSet.StateClips.Values)
            {
                if (!database.Animations.TryGetById(clipId, out _))
                {
                    AddMissingReference(report, "character", character.Id, "animation clip", clipId);
                }
            }

            if (!string.IsNullOrWhiteSpace(character.AnimationSet.DefaultClipId) &&
                !database.Animations.TryGetById(character.AnimationSet.DefaultClipId, out _))
            {
                AddMissingReference(report, "character", character.Id, "default animation clip", character.AnimationSet.DefaultClipId);
            }
        }
    }

    private static void ValidateWorldGenerationProfiles(GameContentDatabase database, ContentLoadReport report)
    {
        foreach (var profile in database.WorldGenerationProfiles.Profiles)
        {
            foreach (var dimension in profile.Dimensions)
            {
                ValidateDimensionTile(database, report, profile.Id, "surface tile numeric id", dimension.SurfaceTileId);
                ValidateDimensionTile(database, report, profile.Id, "subsurface tile numeric id", dimension.SubsurfaceTileId);
                ValidateDimensionTile(database, report, profile.Id, "fill tile numeric id", dimension.FillTileId);
            }

            foreach (var ore in profile.Ores)
            {
                if (ore.TileId == 0)
                {
                    continue;
                }

                if (!database.Tiles.TryGetByNumericId(ore.TileId, out _))
                {
                    AddMissingReference(report, "worldgen", profile.Id, "ore tile numeric id", ore.TileId.ToString());
                }

                if (!database.Tiles.TryGetByNumericId(ore.ReplaceTileId, out _))
                {
                    AddMissingReference(report, "worldgen", profile.Id, "ore replacement tile numeric id", ore.ReplaceTileId.ToString());
                }
            }
        }


        foreach (var structure in database.StructurePlans.Definitions)
        {
            foreach (var tileId in structure.Legend.Values.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!string.Equals(tileId, "air", StringComparison.OrdinalIgnoreCase) &&
                    !database.Tiles.TryGetById(tileId, out _))
                {
                    AddMissingReference(report, "structure-plan", structure.Id, "template tile", tileId);
                }
            }

            foreach (var biomeId in structure.AllowedBiomeIds)
            {
                if (!database.Biomes.TryGetById(biomeId, out _))
                {
                    AddMissingReference(report, "structure-plan", structure.Id, "biome", biomeId);
                }
            }
        }

        foreach (var profile in database.RegionalGenerationProfiles.Profiles)
        {
            foreach (var layer in profile.BiomeLayers)
            {
                foreach (var biomeId in layer.BiomeIds)
                {
                    if (!database.Biomes.TryGetById(biomeId, out _))
                    {
                        AddMissingReference(report, "regional-worldgen", profile.Id, $"layer '{layer.Id}' biome", biomeId);
                    }
                }
            }

            foreach (var feature in profile.Features)
            {
                foreach (var biomeId in feature.AllowedBiomeIds)
                {
                    if (!database.Biomes.TryGetById(biomeId, out _))
                    {
                        AddMissingReference(report, "regional-worldgen", profile.Id, $"feature '{feature.Id}' biome", biomeId);
                    }
                }
            }
        }
    }

    private static void ValidateWorldEvents(GameContentDatabase database, ContentLoadReport report)
    {
        foreach (var definition in database.WorldEvents.Definitions)
        {
            foreach (var biomeId in definition.AllowedBiomeIds)
            {
                if (!database.Biomes.TryGetById(biomeId, out _))
                {
                    AddMissingReference(report, "world-event", definition.Id, "biome", biomeId);
                }
            }

            if (database.SpriteAssets.HasExplicitAssets)
            {
                RequireOptionalSprite(
                    database,
                    report,
                    "world-event",
                    definition.Id,
                    definition.Modifiers.ParticleSpriteId);
                foreach (var phase in definition.Phases)
                {
                    RequireOptionalSprite(
                        database,
                        report,
                        "world-event",
                        $"{definition.Id}:{phase.Id}",
                        phase.Modifiers.ParticleSpriteId);
                }
            }
        }
    }

    private static void ValidateDimensionTile(
        GameContentDatabase database,
        ContentLoadReport report,
        string profileId,
        string referenceKind,
        ushort tileId)
    {
        if (!database.Tiles.TryGetByNumericId(tileId, out _))
        {
            AddMissingReference(report, "worldgen", profileId, referenceKind, tileId.ToString());
        }
    }

    private static void ValidateSpriteReferences(GameContentDatabase database, ContentLoadReport report)
    {
        if (!database.SpriteAssets.HasExplicitAssets)
        {
            return;
        }

        foreach (var tile in database.Tiles.Definitions.Where(tile => !string.Equals(tile.Id, database.Tiles.Fallback.Id, StringComparison.OrdinalIgnoreCase)))
        {
            RequireSprite(database, report, "tile", tile.Id, tile.TexturePath);
        }

        foreach (var item in database.Items.Definitions.Where(item => !string.Equals(item.Id, database.Items.Fallback.Id, StringComparison.OrdinalIgnoreCase)))
        {
            RequireSprite(database, report, "item", item.Id, item.TexturePath);
        }

        foreach (var entity in database.Entities.Definitions)
        {
            RequireSprite(database, report, "entity", entity.Id, entity.TexturePath);
        }

        foreach (var projectile in database.Projectiles.Definitions)
        {
            RequireSprite(database, report, "projectile", projectile.Id, projectile.TexturePath);
        }

        foreach (var crop in database.Crops.Definitions)
        {
            RequireSprite(database, report, "crop", crop.Id, crop.TexturePath);
        }
    }

    private static void RequireSprite(
        GameContentDatabase database,
        ContentLoadReport report,
        string contentKind,
        string contentId,
        string spriteId)
    {
        if (database.SpriteAssets.TryGetById(spriteId, out _))
        {
            return;
        }

        AddMissingReference(report, contentKind, contentId, "sprite asset", spriteId);
    }

    private static void RequireOptionalSprite(
        GameContentDatabase database,
        ContentLoadReport report,
        string contentKind,
        string contentId,
        string? spriteId)
    {
        if (!string.IsNullOrWhiteSpace(spriteId))
        {
            RequireSprite(database, report, contentKind, contentId, spriteId);
        }
    }

    private static void ValidateOptionalSpawnRule(
        GameContentDatabase database,
        ContentLoadReport report,
        string biomeId,
        string referenceKind,
        string? ruleId)
    {
        if (!string.IsNullOrWhiteSpace(ruleId) && !database.SpawnRules.TryGetById(ruleId, out _))
        {
            AddMissingReference(report, "biome", biomeId, referenceKind, ruleId);
        }
    }

    private static void ValidateStatusEffects(
        GameContentDatabase database,
        ContentLoadReport report,
        string contentKind,
        string contentId,
        IEnumerable<StatusEffectApplication> effects)
    {
        foreach (var effect in effects)
        {
            if (!database.StatusEffects.TryGetById(effect.EffectId, out _))
            {
                AddMissingReference(report, contentKind, contentId, "status effect", effect.EffectId);
            }
        }
    }

    private static void AddMissingReference(
        ContentLoadReport report,
        string contentKind,
        string contentId,
        string referenceKind,
        string referenceId)
    {
        report.AddIssue(
            ContentIssueSeverity.Error,
            "validation",
            contentKind,
            contentId,
            $"Missing {referenceKind} reference '{referenceId}'.");
    }

    private static bool TryResolveProperty(MapObjectDefinition mapObject, out string value, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (mapObject.Properties.TryGetValue(key, out value!) && !string.IsNullOrWhiteSpace(value))
            {
                return true;
            }
        }

        value = string.Empty;
        return false;
    }
}
