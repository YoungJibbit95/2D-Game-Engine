namespace Game.Core.World.Liquids;

public readonly record struct LiquidSimulationResult(
    int ChangedTiles,
    int MovedLiquid,
    IReadOnlyList<RectI> ChangedRegions)
{
    public static LiquidSimulationResult None { get; } = new(0, 0, Array.Empty<RectI>());

    public LiquidSimulationResult Add(LiquidSimulationResult other)
    {
        if (ChangedTiles == 0 && MovedLiquid == 0)
        {
            return other;
        }

        if (other.ChangedTiles == 0 && other.MovedLiquid == 0)
        {
            return this;
        }

        var tracker = new DirtyRegionTracker();
        tracker.AddRange(ChangedRegions);
        tracker.AddRange(other.ChangedRegions);
        return new LiquidSimulationResult(
            ChangedTiles + other.ChangedTiles,
            MovedLiquid + other.MovedLiquid,
            tracker.DrainMerged());
    }
}
