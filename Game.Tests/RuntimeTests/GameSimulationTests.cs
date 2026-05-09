using Game.Core.Biomes;
using Game.Core.Combat;
using Game.Core.Crafting;
using Game.Core.Data;
using Game.Core.Entities;
using Game.Core.Events;
using Game.Core.Inventory;
using Game.Core.Items;
using Game.Core.Loot;
using Game.Core.Physics;
using Game.Core.Projectiles;
using Game.Core.Runtime;
using Game.Core.Spawning;
using Game.Core.Tiles;
using Game.Core.World;
using Game.Core.World.Simulation;
using System.Numerics;
using Xunit;

namespace Game.Tests.RuntimeTests;

public sealed class GameSimulationTests
{
    [Fact]
    public void Tick_AdvancesCoreSystemsTogether()
    {
        var content = CreateContent();
        var world = new World(32, 16, WorldMetadata.CreateDefault(seed: 1));
        for (var x = 0; x < world.WidthTiles; x++)
        {
            world.SetTile(x, 8, KnownTileIds.Dirt);
        }

        var player = new PlayerEntity(new Vector2(64, 96), new TileCollisionResolver());
        var inventory = new Inventory(4, content.Items);
        var entities = new EntityManager(spatialCellSize: 16);
        entities.Add(new DroppedItemEntity(new ItemStack("gel", 2), player.Body.Position, new TileCollisionResolver()));
        entities.Add(new EntityFactory(new TileCollisionResolver()).CreateEnemy(CreateSlimeDefinition(), player.Body.Position));

        var simulation = new GameSimulation(
            content,
            world,
            new BiomeMap("forest"),
            player,
            inventory,
            entities,
            combat: new CombatSystem(new LootRoller(new Random(1)), new TileCollisionResolver()),
            spawnOptions: new SpawnSchedulerOptions { MaxTotalActiveEnemies = 0 });

        var result = simulation.Tick(PlayerCommand.None, 0.016f);

        Assert.Equal(1, result.ContactDamage.ContactHits);
        Assert.Equal(90, player.Health);
        Assert.Equal(1, result.PickedUpItems);
        Assert.Equal(2, inventory.CountItem("gel"));
    }

    [Fact]
    public void Tick_AdvancesScheduledWorldSimulation()
    {
        var content = CreateContent();
        var world = new World(16, 16, WorldMetadata.CreateDefault(seed: 1));
        world.SetTile(5, 5, TileInstance.Liquid(255));
        var player = new PlayerEntity(new Vector2(32, 32), new TileCollisionResolver());
        var inventory = new Inventory(4, content.Items);
        var simulation = new GameSimulation(
            content,
            world,
            new BiomeMap("forest"),
            player,
            inventory,
            spawnOptions: new SpawnSchedulerOptions { MaxTotalActiveEnemies = 0 },
            worldSimulationOptions: new WorldSimulationOptions(LiquidStepIntervalSeconds: 0));

        var result = simulation.Tick(PlayerCommand.None, 0.016f);

        Assert.True(result.WorldSimulation.Liquids.MovedLiquid > 0);
        Assert.False(world.GetTile(5, 5).HasLiquid);
        Assert.True(world.GetTile(5, 6).HasLiquid);
        Assert.True(world.GetOrCreateChunk(new ChunkPos(0, 0)).Metadata.ActiveLiquidTiles > 0);
    }

    [Fact]
    public void Tick_ProcessesWorldSimulationRegionsMarkedByEvents()
    {
        var content = CreateContent();
        var world = new World(16, 16, WorldMetadata.CreateDefault(seed: 1));
        world.SetTile(5, 5, TileInstance.Liquid(255));
        var player = new PlayerEntity(new Vector2(32, 32), new TileCollisionResolver());
        var inventory = new Inventory(4, content.Items);
        var events = new GameEventBus();
        var simulation = new GameSimulation(
            content,
            world,
            new BiomeMap("forest"),
            player,
            inventory,
            events: events,
            spawnOptions: new SpawnSchedulerOptions { MaxTotalActiveEnemies = 0 },
            worldSimulationOptions: new WorldSimulationOptions(LiquidStepIntervalSeconds: 0, SeedExistingLiquids: false));

        events.Publish(new TileMinedEvent(new TilePos(5, 5), KnownTileIds.Dirt, ItemStack.Empty));
        var result = simulation.Tick(PlayerCommand.None, 0.016f);

        Assert.True(result.WorldSimulation.Liquids.MovedLiquid > 0);
        Assert.True(result.WorldSimulation.LiquidRegionsProcessed > 0);
    }

    private static GameContentDatabase CreateContent()
    {
        return new GameContentDatabase(
            TileRegistry.Create(new[]
            {
                new TileDefinition
                {
                    NumericId = KnownTileIds.Dirt,
                    Id = "dirt",
                    DisplayName = "Dirt",
                    TexturePath = "tiles/dirt",
                    Solid = true,
                    BlocksLight = true,
                    Hardness = 1
                }
            }),
            ItemRegistry.Create(new[]
            {
                new ItemDefinition
                {
                    Id = "gel",
                    DisplayName = "Gel",
                    Type = ItemType.Material,
                    TexturePath = "items/gel",
                    MaxStack = 999
                }
            }),
            RecipeRegistry.Create(Array.Empty<RecipeDefinition>()),
            LootTableRegistry.Create(Array.Empty<LootTableDefinition>()),
            BiomeRegistry.Create(new[]
            {
                new BiomeDefinition
                {
                    Id = "forest",
                    DisplayName = "Forest",
                    SurfaceTile = "dirt",
                    UndergroundTile = "dirt"
                }
            }),
            ProjectileRegistry.Create(Array.Empty<ProjectileDefinition>()),
            EntityDefinitionRegistry.Create(new[] { CreateSlimeDefinition() }),
            SpawnRuleRegistry.Create(Array.Empty<SpawnRuleDefinition>()));
    }

    private static EntityDefinition CreateSlimeDefinition()
    {
        return new EntityDefinition
        {
            Id = "slime",
            DisplayName = "Slime",
            TexturePath = "entities/slime",
            MaxHealth = 20
        };
    }
}
