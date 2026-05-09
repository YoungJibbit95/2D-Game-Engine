using Game.Core.Assets;
using Game.Core.Biomes;
using Game.Core.Crafting;
using Game.Core.Effects;
using Game.Core.Entities;
using Game.Core.Items;
using Game.Core.Loot;
using Game.Core.Projectiles;
using Game.Core.Spawning;
using Game.Core.Tiles;
using Game.Core.World.Generation;

namespace Game.Core.Data;

public sealed record GameContentDatabase(
    TileRegistry Tiles,
    ItemRegistry Items,
    RecipeRegistry Recipes,
    LootTableRegistry LootTables,
    BiomeRegistry Biomes,
    ProjectileRegistry Projectiles,
    EntityDefinitionRegistry Entities,
    SpawnRuleRegistry SpawnRules)
{
    public SpriteAssetRegistry SpriteAssets { get; init; } = SpriteAssetRegistry.Create(Array.Empty<SpriteAssetDefinition>());

    public StatusEffectRegistry StatusEffects { get; init; } = StatusEffectRegistry.Create(Array.Empty<StatusEffectDefinition>());

    public WorldGenerationProfileRegistry WorldGenerationProfiles { get; init; } =
        WorldGenerationProfileRegistry.Create(Array.Empty<WorldGenerationProfile>());
}
