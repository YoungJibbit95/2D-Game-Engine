using Game.Core.Biomes;
using Game.Core.Crafting;
using Game.Core.Data;
using Game.Core.Entities;
using Game.Core.Items;
using Game.Core.Loot;
using Game.Core.Projectiles;
using Game.Core.Spawning;
using Game.Core.Tiles;

namespace Game.Tests.CommandTests;

internal static class CommandTestContent
{
    public static GameContentDatabase Create()
    {
        return new GameContentDatabase(
            TileRegistry.Create(Array.Empty<TileDefinition>()),
            ItemRegistry.Create(new[]
            {
                new ItemDefinition
                {
                    Id = "gel",
                    DisplayName = "Gel",
                    Type = ItemType.Material,
                    TexturePath = "items/gel",
                    MaxStack = 999
                },
                new ItemDefinition
                {
                    Id = "wood",
                    DisplayName = "Wood",
                    Type = ItemType.Material,
                    TexturePath = "items/wood",
                    MaxStack = 999
                }
            }),
            RecipeRegistry.Create(Array.Empty<RecipeDefinition>()),
            LootTableRegistry.Create(Array.Empty<LootTableDefinition>()),
            BiomeRegistry.Create(Array.Empty<BiomeDefinition>()),
            ProjectileRegistry.Create(Array.Empty<ProjectileDefinition>()),
            EntityDefinitionRegistry.Create(new[]
            {
                new EntityDefinition
                {
                    Id = "slime",
                    DisplayName = "Slime",
                    TexturePath = "entities/slime",
                    MaxHealth = 5,
                    Width = 16,
                    Height = 16,
                    AiBehavior = "slime"
                }
            }),
            SpawnRuleRegistry.Create(Array.Empty<SpawnRuleDefinition>()));
    }
}
