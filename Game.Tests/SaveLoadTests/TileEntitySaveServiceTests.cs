using Game.Core.Inventory;
using Game.Core.Items;
using Game.Core.Saving;
using Game.Core.World;
using Game.Core.World.TileEntities;
using Xunit;

namespace Game.Tests.SaveLoadTests;

public sealed class TileEntitySaveServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "terraria-like-tile-entity-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void SaveLoad_RoundTripsChestInventory()
    {
        var items = CreateItems();
        var manager = new TileEntityManager();
        var chest = new ChestTileEntity(new TilePos(4, 5), items, slotCount: 4);
        chest.Inventory.AddItem(new ItemStack("gel", 12));
        manager.Add(chest);
        var path = Path.Combine(_root, "tile_entities.json");

        var service = new TileEntitySaveService();
        service.Save(manager, path);
        var loaded = service.Load(path, items);

        var loadedChest = Assert.IsType<ChestTileEntity>(Assert.Single(loaded.Entities));
        Assert.Equal(new TilePos(4, 5), loadedChest.Position);
        Assert.Equal(12, loadedChest.Inventory.CountItem("gel"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
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
