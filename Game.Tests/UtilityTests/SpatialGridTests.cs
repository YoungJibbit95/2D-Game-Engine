using Game.Core.Utilities;
using Game.Core.World;
using Xunit;

namespace Game.Tests.UtilityTests;

public sealed class SpatialGridTests
{
    [Fact]
    public void Query_ReturnsItemsIntersectingArea()
    {
        var grid = new SpatialGrid<string>(cellSize: 16);
        grid.Insert("player", new RectI(4, 4, 10, 10));
        grid.Insert("slime", new RectI(100, 100, 10, 10));

        var result = grid.Query(new RectI(0, 0, 32, 32));

        Assert.Equal(new[] { "player" }, result);
    }

    [Fact]
    public void Query_DoesNotReturnDuplicatesForMultiCellItems()
    {
        var grid = new SpatialGrid<string>(cellSize: 16);
        grid.Insert("large", new RectI(0, 0, 64, 64));

        var result = grid.Query(new RectI(0, 0, 64, 64));

        Assert.Single(result);
        Assert.Equal("large", result[0]);
    }

    [Fact]
    public void Insert_ReplacesExistingItemBounds()
    {
        var grid = new SpatialGrid<string>(cellSize: 16);
        grid.Insert("pickup", new RectI(0, 0, 8, 8));
        grid.Insert("pickup", new RectI(64, 64, 8, 8));

        Assert.Empty(grid.Query(new RectI(0, 0, 16, 16)));
        Assert.Equal(new[] { "pickup" }, grid.Query(new RectI(64, 64, 16, 16)));
    }

    [Fact]
    public void Query_SupportsNegativeCoordinates()
    {
        var grid = new SpatialGrid<string>(cellSize: 16);
        grid.Insert("left", new RectI(-20, -10, 8, 8));

        var result = grid.Query(new RectI(-32, -16, 16, 16));

        Assert.Equal(new[] { "left" }, result);
    }

    [Fact]
    public void Remove_RemovesItemFromAllCells()
    {
        var grid = new SpatialGrid<string>(cellSize: 16);
        grid.Insert("large", new RectI(0, 0, 64, 64));

        var removed = grid.Remove("large");

        Assert.True(removed);
        Assert.Empty(grid.Query(new RectI(0, 0, 64, 64)));
        Assert.Equal(0, grid.Count);
    }
}
