using Game.Core.Biomes;
using Game.Core.Crafting;
using Game.Core.Entities;
using Game.Core.Items;
using Game.Core.Loot;
using Game.Core.Mods;
using Game.Core.Projectiles;
using Game.Core.Spawning;
using Game.Core.Tiles;
using System.Text.Json;

namespace Game.Core.Data;

public sealed class GameContentLoader
{
    private readonly TileDefinitionJsonLoader _tileLoader;
    private readonly ItemDefinitionJsonLoader _itemLoader;
    private readonly RecipeDefinitionJsonLoader _recipeLoader;
    private readonly LootTableJsonLoader _lootTableLoader;
    private readonly BiomeJsonLoader _biomeLoader;
    private readonly ProjectileDefinitionJsonLoader _projectileLoader;
    private readonly EntityDefinitionJsonLoader _entityLoader;
    private readonly SpawnRuleJsonLoader _spawnRuleLoader;

    public GameContentLoader()
        : this(
            new TileDefinitionJsonLoader(),
            new ItemDefinitionJsonLoader(),
            new RecipeDefinitionJsonLoader(),
            new LootTableJsonLoader(),
            new BiomeJsonLoader(),
            new ProjectileDefinitionJsonLoader(),
            new EntityDefinitionJsonLoader(),
            new SpawnRuleJsonLoader())
    {
    }

    public GameContentLoader(
        TileDefinitionJsonLoader tileLoader,
        ItemDefinitionJsonLoader itemLoader,
        RecipeDefinitionJsonLoader recipeLoader,
        LootTableJsonLoader lootTableLoader,
        BiomeJsonLoader biomeLoader,
        ProjectileDefinitionJsonLoader projectileLoader,
        EntityDefinitionJsonLoader entityLoader,
        SpawnRuleJsonLoader spawnRuleLoader)
    {
        _tileLoader = tileLoader;
        _itemLoader = itemLoader;
        _recipeLoader = recipeLoader;
        _lootTableLoader = lootTableLoader;
        _biomeLoader = biomeLoader;
        _projectileLoader = projectileLoader;
        _entityLoader = entityLoader;
        _spawnRuleLoader = spawnRuleLoader;
    }

    public GameContentDatabase LoadFromRoot(string contentRoot)
    {
        return LoadWithMods(contentRoot, modsRoot: null).Database;
    }

    public GameContentLoadResult LoadWithMods(string contentRoot, string? modsRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentRoot);

        if (!Directory.Exists(contentRoot))
        {
            throw new DirectoryNotFoundException($"Content root does not exist: {contentRoot}");
        }

        var report = new ContentLoadReport();
        var builder = new ContentDatabaseBuilder(report);
        builder.Merge(LoadDefinitionSet(contentRoot), "base");

        if (!string.IsNullOrWhiteSpace(modsRoot) && Directory.Exists(modsRoot))
        {
            foreach (var modDirectory in Directory.EnumerateDirectories(modsRoot).Order(StringComparer.OrdinalIgnoreCase))
            {
                var manifest = LoadManifest(modDirectory);
                builder.Merge(LoadDefinitionSet(modDirectory), manifest.Id);
            }
        }

        var database = builder.Build();
        new ContentReferenceValidator().Validate(database, report);
        return new GameContentLoadResult(database, report);
    }

    private ContentDefinitionSet LoadDefinitionSet(string root)
    {
        return new ContentDefinitionSet(
            _tileLoader.LoadDefinitionsFromDirectory(Path.Combine(root, "tiles")),
            _itemLoader.LoadDefinitionsFromDirectory(Path.Combine(root, "items")),
            _recipeLoader.LoadDefinitionsFromDirectory(Path.Combine(root, "recipes")),
            _lootTableLoader.LoadDefinitionsFromDirectory(Path.Combine(root, "loot")),
            _biomeLoader.LoadDefinitionsFromDirectory(Path.Combine(root, "biomes")),
            _projectileLoader.LoadDefinitionsFromDirectory(Path.Combine(root, "projectiles")),
            _entityLoader.LoadDefinitionsFromDirectory(Path.Combine(root, "entities")),
            _spawnRuleLoader.LoadDefinitionsFromDirectory(Path.Combine(root, "spawns")));
    }

    private static ContentPackManifest LoadManifest(string modDirectory)
    {
        var manifestPath = Path.Combine(modDirectory, "mod.json");
        if (!File.Exists(manifestPath))
        {
            return new ContentPackManifest
            {
                Id = Path.GetFileName(modDirectory),
                DisplayName = Path.GetFileName(modDirectory)
            };
        }

        return JsonSerializer.Deserialize<ContentPackManifest>(
            File.ReadAllText(manifestPath),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException($"Mod manifest was empty: {manifestPath}");
    }

    private sealed record ContentDefinitionSet(
        IReadOnlyList<TileDefinition> Tiles,
        IReadOnlyList<ItemDefinition> Items,
        IReadOnlyList<RecipeDefinition> Recipes,
        IReadOnlyList<LootTableDefinition> LootTables,
        IReadOnlyList<BiomeDefinition> Biomes,
        IReadOnlyList<ProjectileDefinition> Projectiles,
        IReadOnlyList<EntityDefinition> Entities,
        IReadOnlyList<SpawnRuleDefinition> SpawnRules);

    private sealed class ContentDatabaseBuilder
    {
        private readonly ContentLoadReport _report;
        private readonly Dictionary<string, Packed<TileDefinition>> _tiles = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<ushort, Packed<TileDefinition>> _tilesByNumericId = new();
        private readonly Dictionary<string, Packed<ItemDefinition>> _items = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Packed<RecipeDefinition>> _recipes = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Packed<LootTableDefinition>> _lootTables = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Packed<BiomeDefinition>> _biomes = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Packed<ProjectileDefinition>> _projectiles = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Packed<EntityDefinition>> _entities = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Packed<SpawnRuleDefinition>> _spawnRules = new(StringComparer.OrdinalIgnoreCase);

        public ContentDatabaseBuilder(ContentLoadReport report)
        {
            _report = report;
        }

        public void Merge(ContentDefinitionSet definitions, string packId)
        {
            foreach (var tile in definitions.Tiles)
            {
                MergeTile(tile, packId);
            }

            MergeById(_items, definitions.Items, item => item.Id, "item", packId);
            MergeById(_recipes, definitions.Recipes, recipe => recipe.Id, "recipe", packId);
            MergeById(_lootTables, definitions.LootTables, table => table.Id, "loot", packId);
            MergeById(_biomes, definitions.Biomes, biome => biome.Id, "biome", packId);
            MergeById(_projectiles, definitions.Projectiles, projectile => projectile.Id, "projectile", packId);
            MergeById(_entities, definitions.Entities, entity => entity.Id, "entity", packId);
            MergeById(_spawnRules, definitions.SpawnRules, rule => rule.Id, "spawn", packId);
        }

        public GameContentDatabase Build()
        {
            return new GameContentDatabase(
                TileRegistry.Create(_tiles.Values.Select(value => value.Definition)),
                ItemRegistry.Create(_items.Values.Select(value => value.Definition)),
                RecipeRegistry.Create(_recipes.Values.Select(value => value.Definition)),
                LootTableRegistry.Create(_lootTables.Values.Select(value => value.Definition)),
                BiomeRegistry.Create(_biomes.Values.Select(value => value.Definition)),
                ProjectileRegistry.Create(_projectiles.Values.Select(value => value.Definition)),
                EntityDefinitionRegistry.Create(_entities.Values.Select(value => value.Definition)),
                SpawnRuleRegistry.Create(_spawnRules.Values.Select(value => value.Definition)));
        }

        private void MergeTile(TileDefinition tile, string packId)
        {
            if (_tiles.TryGetValue(tile.Id, out var previousById))
            {
                if (previousById.Definition.NumericId != tile.NumericId)
                {
                    _report.AddIssue(
                        ContentIssueSeverity.Error,
                        packId,
                        "tile",
                        tile.Id,
                        $"Tile override must keep numeric id {previousById.Definition.NumericId}, but got {tile.NumericId}.");
                    return;
                }

                _tiles[tile.Id] = new Packed<TileDefinition>(tile, packId);
                _tilesByNumericId[tile.NumericId] = new Packed<TileDefinition>(tile, packId);
                _report.AddOverride("tile", tile.Id, previousById.PackId, packId);
                return;
            }

            if (_tilesByNumericId.TryGetValue(tile.NumericId, out var previousByNumericId))
            {
                _report.AddIssue(
                    ContentIssueSeverity.Error,
                    packId,
                    "tile",
                    tile.Id,
                    $"Tile numeric id {tile.NumericId} is already used by '{previousByNumericId.Definition.Id}' from '{previousByNumericId.PackId}'.");
                return;
            }

            var packed = new Packed<TileDefinition>(tile, packId);
            _tiles.Add(tile.Id, packed);
            _tilesByNumericId.Add(tile.NumericId, packed);
        }

        private void MergeById<T>(
            Dictionary<string, Packed<T>> target,
            IEnumerable<T> definitions,
            Func<T, string> getId,
            string contentKind,
            string packId)
        {
            foreach (var definition in definitions)
            {
                var id = getId(definition);
                if (target.TryGetValue(id, out var previous))
                {
                    target[id] = new Packed<T>(definition, packId);
                    _report.AddOverride(contentKind, id, previous.PackId, packId);
                    continue;
                }

                target.Add(id, new Packed<T>(definition, packId));
            }
        }
    }

    private sealed record Packed<T>(T Definition, string PackId);
}
