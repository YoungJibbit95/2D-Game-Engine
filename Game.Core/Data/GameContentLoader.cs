using Game.Core.Animations;
using Game.Core.Assets;
using Game.Core.Characters;
using Game.Core.Biomes;
using Game.Core.Crafting;
using Game.Core.Dialogue;
using Game.Core.Effects;
using Game.Core.Entities;
using Game.Core.Farming;
using Game.Core.Items;
using Game.Core.Loot;
using Game.Core.Maps;
using Game.Core.Mods;
using Game.Core.Projectiles;
using Game.Core.Shops;
using Game.Core.Spawning;
using Game.Core.Startup;
using Game.Core.Tiles;
using Game.Core.World.Generation;
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
    private readonly StatusEffectDefinitionJsonLoader _statusEffectLoader;
    private readonly SpriteAssetJsonLoader _spriteAssetLoader;
    private readonly WorldGenerationProfileJsonLoader _worldGenerationProfileLoader;
    private readonly CropDefinitionJsonLoader _cropLoader;
    private readonly MapDefinitionJsonLoader _mapLoader;
    private readonly DialogueDefinitionJsonLoader _dialogueLoader;
    private readonly ShopDefinitionJsonLoader _shopLoader;
    private readonly GameStartupDefinitionJsonLoader _startupLoader;
    private readonly SpriteAnimationJsonLoader _animationLoader;
    private readonly CharacterDefinitionJsonLoader _characterLoader;

    public GameContentLoader()
        : this(
            new TileDefinitionJsonLoader(),
            new ItemDefinitionJsonLoader(),
            new RecipeDefinitionJsonLoader(),
            new LootTableJsonLoader(),
            new BiomeJsonLoader(),
            new ProjectileDefinitionJsonLoader(),
            new EntityDefinitionJsonLoader(),
            new SpawnRuleJsonLoader(),
            new StatusEffectDefinitionJsonLoader(),
            new SpriteAssetJsonLoader(),
            new WorldGenerationProfileJsonLoader(),
            new CropDefinitionJsonLoader(),
            new MapDefinitionJsonLoader())
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
        SpawnRuleJsonLoader spawnRuleLoader,
        StatusEffectDefinitionJsonLoader statusEffectLoader,
        SpriteAssetJsonLoader spriteAssetLoader,
        WorldGenerationProfileJsonLoader? worldGenerationProfileLoader = null,
        CropDefinitionJsonLoader? cropLoader = null,
        MapDefinitionJsonLoader? mapLoader = null,
        DialogueDefinitionJsonLoader? dialogueLoader = null,
        ShopDefinitionJsonLoader? shopLoader = null,
        GameStartupDefinitionJsonLoader? startupLoader = null,
        SpriteAnimationJsonLoader? animationLoader = null,
        CharacterDefinitionJsonLoader? characterLoader = null)
    {
        _tileLoader = tileLoader;
        _itemLoader = itemLoader;
        _recipeLoader = recipeLoader;
        _lootTableLoader = lootTableLoader;
        _biomeLoader = biomeLoader;
        _projectileLoader = projectileLoader;
        _entityLoader = entityLoader;
        _spawnRuleLoader = spawnRuleLoader;
        _statusEffectLoader = statusEffectLoader;
        _spriteAssetLoader = spriteAssetLoader;
        _worldGenerationProfileLoader = worldGenerationProfileLoader ?? new WorldGenerationProfileJsonLoader();
        _cropLoader = cropLoader ?? new CropDefinitionJsonLoader();
        _mapLoader = mapLoader ?? new MapDefinitionJsonLoader();
        _dialogueLoader = dialogueLoader ?? new DialogueDefinitionJsonLoader();
        _shopLoader = shopLoader ?? new ShopDefinitionJsonLoader();
        _startupLoader = startupLoader ?? new GameStartupDefinitionJsonLoader();
        _animationLoader = animationLoader ?? new SpriteAnimationJsonLoader();
        _characterLoader = characterLoader ?? new CharacterDefinitionJsonLoader();
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
            _spawnRuleLoader.LoadDefinitionsFromDirectory(Path.Combine(root, "spawns")),
            _statusEffectLoader.LoadDefinitionsFromDirectory(Path.Combine(root, "effects")),
            _spriteAssetLoader.LoadDefinitionsFromDirectory(Path.Combine(root, "assets")),
            _worldGenerationProfileLoader.LoadProfilesFromDirectory(Path.Combine(root, "worldgen")),
            _cropLoader.LoadDefinitionsFromDirectory(Path.Combine(root, "crops")),
            _mapLoader.LoadDefinitionsFromDirectory(Path.Combine(root, "maps")),
            _dialogueLoader.LoadDefinitionsFromDirectory(Path.Combine(root, "dialogue")),
            _shopLoader.LoadDefinitionsFromDirectory(Path.Combine(root, "shops")),
            _startupLoader.LoadDefinitionsFromDirectory(Path.Combine(root, "startup")),
            _animationLoader.LoadClipsFromDirectory(Path.Combine(root, "animations")),
            _characterLoader.LoadDefinitionsFromDirectory(Path.Combine(root, "characters")));
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
        IReadOnlyList<SpawnRuleDefinition> SpawnRules,
        IReadOnlyList<StatusEffectDefinition> StatusEffects,
        IReadOnlyList<SpriteAssetDefinition> SpriteAssets,
        IReadOnlyList<WorldGenerationProfile> WorldGenerationProfiles,
        IReadOnlyList<CropDefinition> Crops,
        IReadOnlyList<MapDefinition> Maps,
        IReadOnlyList<DialogueDefinition> Dialogues,
        IReadOnlyList<ShopDefinition> Shops,
        IReadOnlyList<GameStartupDefinition> GameStartups,
        IReadOnlyList<SpriteAnimationClip> Animations,
        IReadOnlyList<CharacterDefinition> Characters);

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
        private readonly Dictionary<string, Packed<StatusEffectDefinition>> _statusEffects = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Packed<SpriteAssetDefinition>> _spriteAssets = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Packed<WorldGenerationProfile>> _worldGenerationProfiles = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Packed<CropDefinition>> _crops = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Packed<MapDefinition>> _maps = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Packed<DialogueDefinition>> _dialogues = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Packed<ShopDefinition>> _shops = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Packed<GameStartupDefinition>> _gameStartups = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Packed<SpriteAnimationClip>> _animations = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Packed<CharacterDefinition>> _characters = new(StringComparer.OrdinalIgnoreCase);

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
            MergeById(_statusEffects, definitions.StatusEffects, effect => effect.Id, "effect", packId);
            MergeById(_spriteAssets, definitions.SpriteAssets, sprite => sprite.Id, "sprite", packId);
            MergeById(_worldGenerationProfiles, definitions.WorldGenerationProfiles, profile => profile.Id, "worldgen", packId);
            MergeById(_crops, definitions.Crops, crop => crop.Id, "crop", packId);
            MergeById(_maps, definitions.Maps, map => map.Id, "map", packId);
            MergeById(_dialogues, definitions.Dialogues, dialogue => dialogue.Id, "dialogue", packId);
            MergeById(_shops, definitions.Shops, shop => shop.Id, "shop", packId);
            MergeById(_gameStartups, definitions.GameStartups, startup => startup.Id, "startup", packId);
            MergeById(_animations, definitions.Animations, animation => animation.Id, "animation", packId);
            MergeById(_characters, definitions.Characters, character => character.Id, "character", packId);
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
                SpawnRuleRegistry.Create(_spawnRules.Values.Select(value => value.Definition)))
            {
                StatusEffects = StatusEffectRegistry.Create(_statusEffects.Values.Select(value => value.Definition)),
                SpriteAssets = SpriteAssetRegistry.Create(_spriteAssets.Values.Select(value => value.Definition)),
                WorldGenerationProfiles = WorldGenerationProfileRegistry.Create(_worldGenerationProfiles.Values.Select(value => value.Definition)),
                Crops = CropRegistry.Create(_crops.Values.Select(value => value.Definition)),
                Maps = MapRegistry.Create(_maps.Values.Select(value => value.Definition)),
                Dialogues = DialogueRegistry.Create(_dialogues.Values.Select(value => value.Definition)),
                Shops = ShopRegistry.Create(_shops.Values.Select(value => value.Definition)),
                GameStartups = GameStartupRegistry.Create(_gameStartups.Values.Select(value => value.Definition)),
                Animations = SpriteAnimationRegistry.Create(_animations.Values.Select(value => value.Definition)),
                Characters = CharacterDefinitionRegistry.Create(_characters.Values.Select(value => value.Definition))
            };
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
