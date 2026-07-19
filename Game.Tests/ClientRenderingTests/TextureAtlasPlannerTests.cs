using Game.Client.Rendering.Atlas;
using Xunit;

namespace Game.Tests.ClientRenderingTests;

public sealed class TextureAtlasPlannerTests
{
    [Fact]
    public void Build_PacksDeterministicallyAcrossRowsAndPages()
    {
        TextureAtlasSourceSize[] sources =
        [
            new(14, 14),
            new(14, 14),
            new(14, 14),
            new(14, 14),
            new(14, 14)
        ];
        var placements = new TextureAtlasPlacement[sources.Length];

        var plan = TextureAtlasPlanner.Build(sources, 32, 32, 1, placements);

        Assert.Equal(2, plan.PageCount);
        Assert.Equal(5, plan.PlacedSourceCount);
        Assert.Equal(0, plan.UnplacedSourceCount);
        Assert.Equal(0, placements[0].PageIndex);
        Assert.Equal(0, placements[3].PageIndex);
        Assert.Equal(1, placements[4].PageIndex);
        Assert.Equal(1, placements[0].ContentBounds.X);
        Assert.Equal(1, placements[0].ContentBounds.Y);
    }

    [Fact]
    public void Build_RejectsOversizedSourceWithoutPerturbingLaterPlacements()
    {
        TextureAtlasSourceSize[] sources = [new(65, 8), new(8, 8)];
        var placements = new TextureAtlasPlacement[sources.Length];

        var plan = TextureAtlasPlanner.Build(sources, 64, 64, 1, placements);

        Assert.False(placements[0].IsPlaced);
        Assert.True(placements[1].IsPlaced);
        Assert.Equal(1, plan.PlacedSourceCount);
        Assert.Equal(1, plan.UnplacedSourceCount);
        Assert.Equal(1, plan.PageCount);
    }

    [Fact]
    public void Build_HasZeroSteadyStateAllocationWithCallerStorage()
    {
        var sources = new TextureAtlasSourceSize[256];
        var placements = new TextureAtlasPlacement[sources.Length];
        Array.Fill(sources, new TextureAtlasSourceSize(16, 16));
        _ = TextureAtlasPlanner.Build(sources, 512, 512, 1, placements);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var iteration = 0; iteration < 1_000; iteration++)
        {
            _ = TextureAtlasPlanner.Build(sources, 512, 512, 1, placements);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
}
