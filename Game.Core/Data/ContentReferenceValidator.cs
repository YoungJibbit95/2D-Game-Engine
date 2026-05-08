using Game.Core.Mods;

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
        ValidateEntities(database, report);
        ValidateSpawnRules(database, report);
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
            if (string.IsNullOrWhiteSpace(item.PlacesTileId))
            {
                continue;
            }

            if (!database.Tiles.TryGetById(item.PlacesTileId, out _))
            {
                AddMissingReference(report, "item", item.Id, "placed tile", item.PlacesTileId);
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
            if (string.IsNullOrWhiteSpace(entity.LootTableId))
            {
                continue;
            }

            if (!database.LootTables.TryGetById(entity.LootTableId, out _))
            {
                AddMissingReference(report, "entity", entity.Id, "loot table", entity.LootTableId);
            }
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
}
