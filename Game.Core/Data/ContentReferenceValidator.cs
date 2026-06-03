using Game.Core.Mods;
using Game.Core.Items;
using Game.Core.Effects;
using Game.Core.Maps;

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
        ValidateCrops(database, report);
        ValidateMaps(database, report);
        ValidateShops(database, report);
        ValidateWorldGenerationProfiles(database, report);
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
        }
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
