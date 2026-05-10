using Game.Core;
using Game.Core.Biomes;
using Game.Core.Crafting;
using Game.Core.Data;
using Game.Core.Entities;
using Game.Core.Events;
using Game.Core.Inventory;
using Game.Core.Items;
using Game.Core.Loot;
using Game.Core.Physics;
using Game.Core.Projectiles;
using Game.Core.Saving;
using Game.Core.Spawning;
using Game.Core.Tiles;
using Game.Core.World;
using Game.Core.World.TileEntities;
using System.Numerics;
using Xunit;

namespace Game.Tests.SaveLoadTests;

public sealed class GameLoadCoordinatorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "terraria-like-game-load-tests", Guid.NewGuid().ToString("N"));
    private readonly GameContentDatabase _content = CreateContent();

    [Fact]
    public void SaveThenLoad_RestoresWholeRuntimeSession()
    {
        var world = new World(64, 64, WorldMetadata.CreateDefault(seed: 99));
        world.SetTile(2, 3, KnownTileIds.Dirt);
        world.SetTile(3, 3, KnownTileIds.Stone);

        var player = new PlayerEntity(new Vector2(88, 144), new TileCollisionResolver(), maxHealth: 140, currentHealth: 73);
        var inventory = new PlayerInventory(_content.Items);
        inventory.SelectHotbarSlot(3);
        inventory.AddItem(new ItemStack("gel", 5));

        var entities = new EntityManager();
        var drop = new DroppedItemEntity(new ItemStack("gel", 2), new Vector2(48, 72), new TileCollisionResolver());
        drop.Body.Velocity = new Vector2(5, -2);
        entities.Add(drop);

        var tileEntities = new TileEntityManager();
        var chest = new ChestTileEntity(new TilePos(6, 7), _content.Items, slotCount: 6);
        chest.AddItem(new ItemStack("gel", 9));
        tileEntities.Add(chest);

        new GameSaveCoordinator().Save(
            new GameSaveRequest(world, player, inventory, entities)
            {
                TileEntities = tileEntities
            },
            _root,
            new GameSaveCoordinatorOptions
            {
                PlayerId = "player_load",
                PlayerDisplayName = "Loader",
                ChunkStorageMode = WorldChunkStorageMode.RegionFiles,
                WorldSaveMode = WorldSaveMode.AllChunks
            });

        var events = new GameEventBus();
        GameLoadedEvent? loadedEvent = null;
        events.Subscribe<GameLoadedEvent>(gameEvent => loadedEvent = gameEvent);

        var result = new GameLoadCoordinator(
                new WorldSaveService(WorldChunkStorageMode.RegionFiles),
                new PlayerSaveService(),
                new EntitySaveService(),
                new TileEntitySaveService(),
                new TileCollisionResolver(),
                clock: () => new DateTimeOffset(2026, 5, 10, 18, 30, 0, TimeSpan.Zero))
            .Load(_root, _content, events: events);

        Assert.Equal(new DateTimeOffset(2026, 5, 10, 18, 30, 0, TimeSpan.Zero), result.LoadedAtUtc);
        Assert.True(result.PlayerLoaded);
        Assert.True(result.RuntimeEntitiesLoaded);
        Assert.True(result.TileEntitiesLoaded);
        Assert.Equal(KnownTileIds.Dirt, result.World.GetTile(2, 3).TileId);
        Assert.Equal(KnownTileIds.Stone, result.World.GetTile(3, 3).TileId);
        Assert.Equal(new Vector2(88, 144), result.Player.Body.Position);
        Assert.Equal(73, result.Player.Health);
        Assert.Equal(140, result.Player.MaxHealth);
        Assert.Equal(3, result.Inventory.SelectedHotbarSlot);
        Assert.Equal(5, result.Inventory.CountItem("gel"));

        var loadedDrop = Assert.IsType<DroppedItemEntity>(Assert.Single(result.Entities.Entities));
        Assert.Equal(new ItemStack("gel", 2), loadedDrop.Stack);
        Assert.Equal(new Vector2(5, -2), loadedDrop.Body.Velocity);

        var loadedChest = Assert.IsType<ChestTileEntity>(Assert.Single(result.TileEntities.Entities));
        Assert.Equal(new TilePos(6, 7), loadedChest.Position);
        Assert.Equal(9, loadedChest.Inventory.CountItem("gel"));
        Assert.NotNull(loadedEvent);
        Assert.Equal(result, loadedEvent.Result);
    }

    [Fact]
    public void CanLoad_RequiresMetadataAndPlayerFiles()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "metadata.json"), "{}");
        var coordinator = new GameLoadCoordinator();

        Assert.False(coordinator.CanLoad(_root));

        File.WriteAllText(Path.Combine(_root, "player.json"), "{}");

        Assert.True(coordinator.CanLoad(_root));
    }

    [Fact]
    public void Load_CanSkipOptionalRuntimeAndTileEntities()
    {
        var request = CreateSaveRequest();
        request.Entities.Add(new DroppedItemEntity(new ItemStack("gel", 1), new Vector2(10, 20), new TileCollisionResolver()));
        var tileEntities = new TileEntityManager();
        tileEntities.Add(new ChestTileEntity(new TilePos(1, 2), _content.Items, slotCount: 2));

        new GameSaveCoordinator().Save(
            request with { TileEntities = tileEntities },
            _root,
            new GameSaveCoordinatorOptions { WorldSaveMode = WorldSaveMode.AllChunks });

        var loaded = new GameLoadCoordinator().Load(
            _root,
            _content,
            new GameLoadCoordinatorOptions
            {
                LoadRuntimeEntities = false,
                LoadTileEntities = false
            });

        Assert.False(loaded.RuntimeEntitiesLoaded);
        Assert.False(loaded.TileEntitiesLoaded);
        Assert.Empty(loaded.Entities.Entities);
        Assert.Empty(loaded.TileEntities.Entities);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private GameSaveRequest CreateSaveRequest()
    {
        var world = new World(32, 32, WorldMetadata.CreateDefault(seed: 12));
        world.SetTile(0, 0, KnownTileIds.Dirt);
        return new GameSaveRequest(
            world,
            new PlayerEntity(new Vector2(16, 32), new TileCollisionResolver()),
            new PlayerInventory(_content.Items),
            new EntityManager());
    }

    private static GameContentDatabase CreateContent()
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
                }
            }),
            RecipeRegistry.Create(Array.Empty<RecipeDefinition>()),
            LootTableRegistry.Create(Array.Empty<LootTableDefinition>()),
            BiomeRegistry.Create(Array.Empty<BiomeDefinition>()),
            ProjectileRegistry.Create(Array.Empty<ProjectileDefinition>()),
            EntityDefinitionRegistry.Create(Array.Empty<EntityDefinition>()),
            SpawnRuleRegistry.Create(Array.Empty<SpawnRuleDefinition>()));
    }
}
