using Game.Core.World;
using Xunit;

namespace Game.Tests.WorldTests;

public sealed class DirtyRegionTrackerTests
{
    [Fact]
    public void DrainMerged_MergesTouchingRegionsAndClearsTracker()
    {
        var tracker = new DirtyRegionTracker();
        tracker.Add(new RectI(2, 2, 2, 2));
        tracker.Add(new RectI(4, 2, 2, 2));
        tracker.Add(new RectI(20, 20, 1, 1));

        var regions = tracker.DrainMerged();

        Assert.Equal(2, regions.Count);
        Assert.Contains(new RectI(2, 2, 4, 2), regions);
        Assert.Contains(new RectI(20, 20, 1, 1), regions);
        Assert.Equal(0, tracker.Count);
    }

    [Fact]
    public void AddTile_AddsPaddedTileRegion()
    {
        var tracker = new DirtyRegionTracker();

        tracker.AddTile(new TilePos(5, 6), padding: 2);

        Assert.Equal(new RectI(3, 4, 5, 5), Assert.Single(tracker.PeekMerged()));
    }
}
