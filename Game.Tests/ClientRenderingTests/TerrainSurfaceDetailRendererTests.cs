using Game.Client.Rendering;
using Game.Core.Tiles;
using Game.Core.World;
using Xunit;

namespace Game.Tests.ClientRenderingTests;

public sealed class TerrainSurfaceDetailRendererTests
{
    [Fact]
    public void Plan_AddsFringeOnlyToExposedSupportedSurfaces()
    {
        var exposed = TerrainSurfaceDetailRenderer.Plan(KnownTileIds.Grass, AutoTileMask.Bottom, 12, 8);
        var enclosed = TerrainSurfaceDetailRenderer.Plan(KnownTileIds.Grass, (AutoTileMask)15, 12, 8);
        var unsupported = TerrainSurfaceDetailRenderer.Plan(KnownTileIds.Workbench, AutoTileMask.None, 12, 8);

        Assert.True(exposed.Has(TerrainSurfaceDetailFlags.TopFringe));
        Assert.False(enclosed.Has(TerrainSurfaceDetailFlags.TopFringe));
        Assert.Equal(TerrainSurfaceDetailFlags.None, unsupported.Flags);
    }

    [Fact]
    public void Plan_UsesMaterialSpecificHangingMoss()
    {
        var moss = TerrainSurfaceDetailRenderer.Plan(KnownTileIds.MarshMoss, AutoTileMask.Top, 4, 19);
        var grass = TerrainSurfaceDetailRenderer.Plan(KnownTileIds.Grass, AutoTileMask.Top, 4, 19);

        Assert.True(moss.Has(TerrainSurfaceDetailFlags.HangingFringe));
        Assert.False(grass.Has(TerrainSurfaceDetailFlags.HangingFringe));
    }

    [Fact]
    public void Plan_IsDeterministicAcrossNegativeAndPositiveWorldCoordinates()
    {
        for (var y = -32; y <= 32; y++)
        {
            for (var x = -32; x <= 32; x++)
            {
                var first = TerrainSurfaceDetailRenderer.Plan(KnownTileIds.Stone, AutoTileMask.Bottom, x, y);
                var second = TerrainSurfaceDetailRenderer.Plan(KnownTileIds.Stone, AutoTileMask.Bottom, x, y);
                Assert.Equal(first, second);
            }
        }
    }

    [Fact]
    public void Plan_AllocatesZeroBytesAfterWarmup()
    {
        _ = TerrainSurfaceDetailRenderer.Plan(KnownTileIds.Grass, AutoTileMask.Bottom, 0, 0);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var checksum = 0;

        for (var index = 0; index < 100_000; index++)
        {
            var plan = TerrainSurfaceDetailRenderer.Plan(
                (index & 1) == 0 ? KnownTileIds.Grass : KnownTileIds.Stone,
                (AutoTileMask)(index & 15),
                index - 50_000,
                index / 37);
            checksum += (int)plan.Flags + plan.Variant;
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(checksum > 0);
        Assert.Equal(0, allocated);
    }
}
