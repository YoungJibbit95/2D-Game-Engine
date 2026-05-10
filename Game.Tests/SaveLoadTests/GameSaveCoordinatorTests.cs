using Game.Core;
using Game.Core.Entities;
using Game.Core.Events;
using Game.Core.Inventory;
using Game.Core.Items;
using Game.Core.Physics;
using Game.Core.Saving;
using Game.Core.World;
using Game.Core.World.TileEntities;
using System.Numerics;
using Xunit;

namespace Game.Tests.SaveLoadTests;

public sealed class GameSaveCoordinatorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "terraria-like-game-save-tests", Guid.NewGuid().ToString("N"));
    private readonly ItemRegistry _items = CreateItems();

    [Fact]
    public void Save_WritesWorldPlayerEntitiesAndTileEntities()
    {
        var request = CreateRequest();
        request.Inventory.SelectHotbarSlot(2);
        request.Inventory.AddItem(new ItemStack("gel", 3));
        request.Entities.Add(new DroppedItemEntity(new ItemStack("gel", 2), new Vector2(32, 32), new TileCollisionResolver()));
        var tileEntities = new TileEntityManager();
        var chest = new ChestTileEntity(new TilePos(4, 5), _items, slotCount: 4);
        chest.AddItem(new ItemStack("gel", 7));
        tileEntities.Add(chest);
        request = request with { TileEntities = tileEntities };
        request.World.SetTile(3, 3, KnownTileIds.Dirt);
        var events = new GameEventBus();
        GameSavedEvent? savedEvent = null;
        events.Subscribe<GameSavedEvent>(gameEvent => savedEvent = gameEvent);

        var result = new GameSaveCoordinator(
                new PlayerSaveService(),
                new EntitySaveService(),
                new TileEntitySaveService(),
                clock: () => new DateTimeOffset(2026, 5, 10, 12, 0, 0, TimeSpan.Zero))
            .Save(
                request,
                _root,
                new GameSaveCoordinatorOptions
                {
                    PlayerId = "player_test",
                    PlayerDisplayName = "Builder",
                    ChunkStorageMode = WorldChunkStorageMode.RegionFiles,
                    WorldSaveMode = WorldSaveMode.DirtyChunksOnly
                },
                events: events);

        Assert.True(result.WorldSaved);
        Assert.True(result.PlayerSaved);
        Assert.True(result.EntitiesSaved);
        Assert.True(result.TileEntitiesSaved);
        Assert.Equal(1, result.RuntimeEntitiesSaved);
        Assert.Equal(1, result.TileEntityCount);
        Assert.True(result.WorldChunksConsidered > 0);
        Assert.True(File.Exists(Path.Combine(_root, "metadata.json")));
        Assert.True(File.Exists(Path.Combine(_root, "player.json")));
        Assert.True(File.Exists(Path.Combine(_root, "entities.json")));
        Assert.True(File.Exists(Path.Combine(_root, "tile_entities.json")));
        Assert.NotEmpty(Directory.EnumerateFiles(Path.Combine(_root, "regions"), "*.region"));
        Assert.All(request.World.Chunks.Values, chunk => Assert.False(chunk.IsDirty));
        Assert.NotNull(savedEvent);
        Assert.Equal(GameSaveReason.Manual, savedEvent.Result.Reason);

        var playerData = new PlayerSaveService().Load(Path.Combine(_root, "player.json"));
        Assert.Equal("player_test", playerData.PlayerId);
        Assert.Equal("Builder", playerData.DisplayName);
        Assert.Equal(2, playerData.SelectedHotbarSlot);
        Assert.Contains(new ItemStack("gel", 3), playerData.InventorySlots);

        var loadedTileEntities = new TileEntitySaveService().Load(Path.Combine(_root, "tile_entities.json"), _items);
        var loadedChest = Assert.IsType<ChestTileEntity>(Assert.Single(loadedTileEntities.Entities));
        Assert.Equal(7, loadedChest.Inventory.CountItem("gel"));
    }

    [Fact]
    public void TickAutosave_WaitsForIntervalThenSaves()
    {
        var request = CreateRequest();
        request.World.SetTile(1, 1, KnownTileIds.Dirt);
        var coordinator = new GameSaveCoordinator();

        var early = coordinator.TickAutosave(1f, 5f, request, _root);
        var saved = coordinator.TickAutosave(4f, 5f, request, _root);

        Assert.Null(early);
        Assert.NotNull(saved);
        Assert.Equal(GameSaveReason.Autosave, saved.Reason);
        Assert.True(File.Exists(Path.Combine(_root, "player.json")));
    }

    [Fact]
    public void PlayerSaveService_CreateSaveData_UsesPlayerAndPlayerInventoryState()
    {
        var request = CreateRequest();
        request.Inventory.SelectHotbarSlot(4);
        request.Inventory.AddItem(new ItemStack("gel", 8));

        var data = new PlayerSaveService().CreateSaveData(
            request.Player,
            request.Inventory,
            "player_002",
            "Explorer",
            mana: 30);

        Assert.Equal("player_002", data.PlayerId);
        Assert.Equal("Explorer", data.DisplayName);
        Assert.Equal(request.Player.Body.Position.X, data.PositionX);
        Assert.Equal(request.Player.Health, data.Health);
        Assert.Equal(30, data.Mana);
        Assert.Equal(4, data.SelectedHotbarSlot);
        Assert.Equal(PlayerInventory.HotbarSlotCount + PlayerInventory.MainSlotCount, data.InventorySlots.Count);
        Assert.Contains(new ItemStack("gel", 8), data.InventorySlots);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private GameSaveRequest CreateRequest()
    {
        var world = new World(64, 64, WorldMetadata.CreateDefault(seed: 77));
        var player = new PlayerEntity(new Vector2(48, 64), new TileCollisionResolver());
        var inventory = new PlayerInventory(_items);
        var entities = new EntityManager();
        return new GameSaveRequest(world, player, inventory, entities);
    }

    private static ItemRegistry CreateItems()
    {
        return ItemRegistry.Create(new[]
        {
            new ItemDefinition
            {
                Id = "gel",
                DisplayName = "Gel",
                Type = ItemType.Material,
                TexturePath = "items/gel",
                MaxStack = 999
            }
        });
    }
}
