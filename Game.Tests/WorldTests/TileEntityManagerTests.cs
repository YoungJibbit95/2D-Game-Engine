using Game.Core.Inventory;
using Game.Core.Items;
using Game.Core.World;
using Game.Core.World.TileEntities;
using Xunit;

namespace Game.Tests.WorldTests;

public sealed class TileEntityManagerTests
{
    [Fact]
    public void Add_RejectsTwoTileEntitiesAtSamePosition()
    {
        var items = CreateItems();
        var manager = new TileEntityManager();
        var first = new ChestTileEntity(new TilePos(2, 3), items);
        var second = new ChestTileEntity(new TilePos(2, 3), items);

        Assert.True(manager.Add(first));
        Assert.False(manager.Add(second));
        Assert.True(manager.TryGet(new TilePos(2, 3), out var found));
        Assert.Same(first, found);
    }

    [Fact]
    public void Query_ReturnsTileEntitiesInsideTileRegion()
    {
        var items = CreateItems();
        var manager = new TileEntityManager();
        var inside = new ChestTileEntity(new TilePos(2, 3), items);
        var outside = new ChestTileEntity(new TilePos(9, 9), items);
        manager.Add(inside);
        manager.Add(outside);

        var result = manager.Query(new RectI(0, 0, 5, 5));

        Assert.Contains(inside, result);
        Assert.DoesNotContain(outside, result);
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
