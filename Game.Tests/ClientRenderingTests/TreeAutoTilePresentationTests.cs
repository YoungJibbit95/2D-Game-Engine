using Game.Client.Rendering;
using Game.Core.Tiles;
using Game.Core.World;
using Xunit;

namespace Game.Tests.ClientRenderingTests;

public sealed class TreeAutoTilePresentationTests
{
    [Fact]
    public void CompatibleRegionalMaterials_ReceiveReciprocalPresentationSockets()
    {
        var world = CreateWorld();
        world.SetTile(20, 12, KnownTileIds.OakTrunk);
        world.SetTile(21, 12, KnownTileIds.OakLeaves);

        var trunkMask = TreeAutoTilePresentation.AddCompatibleMaterialConnections(
            world,
            20,
            12,
            KnownTileIds.OakTrunk,
            AutoTileMask.Top);
        var leavesMask = TreeAutoTilePresentation.AddCompatibleMaterialConnections(
            world,
            21,
            12,
            KnownTileIds.OakLeaves,
            AutoTileMask.Bottom);

        Assert.Equal(AutoTileMask.Top | AutoTileMask.Right, trunkMask);
        Assert.Equal(AutoTileMask.Bottom | AutoTileMask.Left, leavesMask);
    }

    [Theory]
    [InlineData(KnownTileIds.Wood, KnownTileIds.Leaves)]
    [InlineData(KnownTileIds.LivingWood, KnownTileIds.AutumnLeaves)]
    [InlineData(KnownTileIds.MangroveRoot, KnownTileIds.MarshLeaves)]
    public void EveryTreeMaterialPair_ConnectsAcrossItsSharedEdge(ushort trunkTileId, ushort leavesTileId)
    {
        var world = CreateWorld();
        world.SetTile(20, 12, trunkTileId);
        world.SetTile(20, 11, leavesTileId);

        Assert.Equal(
            AutoTileMask.Top,
            TreeAutoTilePresentation.AddCompatibleMaterialConnections(
                world,
                20,
                12,
                trunkTileId,
                AutoTileMask.None));
        Assert.Equal(
            AutoTileMask.Bottom,
            TreeAutoTilePresentation.AddCompatibleMaterialConnections(
                world,
                20,
                11,
                leavesTileId,
                AutoTileMask.None));
    }

    [Fact]
    public void MismatchedSpeciesAndNonTreeTiles_DoNotGainConnections()
    {
        var world = CreateWorld();
        world.SetTile(20, 12, KnownTileIds.OakTrunk);
        world.SetTile(21, 12, KnownTileIds.MarshLeaves);
        world.SetTile(19, 12, KnownTileIds.Stone);

        Assert.Equal(
            AutoTileMask.Top,
            TreeAutoTilePresentation.AddCompatibleMaterialConnections(
                world,
                20,
                12,
                KnownTileIds.OakTrunk,
                AutoTileMask.Top));
        Assert.Equal(
            AutoTileMask.Left,
            TreeAutoTilePresentation.AddCompatibleMaterialConnections(
                world,
                19,
                12,
                KnownTileIds.Stone,
                AutoTileMask.Left));
    }

    [Fact]
    public void ConnectionPlanning_RemainsAllocationFreeAfterWarmup()
    {
        var world = CreateWorld();
        world.SetTile(20, 12, KnownTileIds.OakTrunk);
        world.SetTile(21, 12, KnownTileIds.OakLeaves);
        for (var warmup = 0; warmup < 512; warmup++)
        {
            _ = TreeAutoTilePresentation.AddCompatibleMaterialConnections(
                world,
                20,
                12,
                KnownTileIds.OakTrunk,
                AutoTileMask.None);
        }

        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var checksum = 0;
        for (var iteration = 0; iteration < 10_000; iteration++)
        {
            checksum += (int)TreeAutoTilePresentation.AddCompatibleMaterialConnections(
                world,
                20,
                12,
                KnownTileIds.OakTrunk,
                AutoTileMask.None);
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        Assert.NotEqual(0, checksum);
        Assert.Equal(0, allocated);
    }

    private static World CreateWorld()
    {
        return new World(64, 32, WorldMetadata.CreateDefault(seed: 4_217));
    }
}
