using Game.Core.Biomes;
using Game.Core.Crafting;
using Game.Core.Entities;
using Game.Core.Items;
using Game.Core.Loot;
using Game.Core.Projectiles;
using Game.Core.Spawning;
using Game.Core.Tiles;

namespace Game.Core.Data;

public sealed record GameContentDatabase(
    TileRegistry Tiles,
    ItemRegistry Items,
    RecipeRegistry Recipes,
    LootTableRegistry LootTables,
    BiomeRegistry Biomes,
    ProjectileRegistry Projectiles,
    EntityDefinitionRegistry Entities,
    SpawnRuleRegistry SpawnRules);
