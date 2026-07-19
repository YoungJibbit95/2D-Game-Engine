using Game.Client.Rendering;
using Game.Core.Tiles;
using Game.Core.World;
using Xunit;

namespace Game.Tests.ClientRenderingTests;

public sealed class TreeTileVisualSelectorTests
{
    [Fact]
    public void Resolve_UsesOneStableVariantAcrossTrunkBranchesAndCanopy()
    {
        var world = CreateWorld(seed: 17_311, width: 96);
        PlaceTree(world, anchorX: 40);
        var expected = TreeTileVisualSelector.Resolve(world, 40, 18, KnownTileIds.Wood);

        Assert.InRange(expected, (byte)0, (byte)(TreeTileVisualSelector.VariantCount - 1));
        Assert.Equal(expected, TreeTileVisualSelector.Resolve(world, 43, 12, KnownTileIds.Leaves));
        Assert.Equal(expected, TreeTileVisualSelector.Resolve(world, 37, 11, KnownTileIds.Leaves));
        Assert.Equal(expected, TreeTileVisualSelector.Resolve(world, 42, 14, KnownTileIds.Wood));
    }

    [Fact]
    public void Resolve_DistributesProjectOwnedPalettesAcrossAnchors()
    {
        var world = CreateWorld(seed: 8_181, width: 640);
        var variants = new HashSet<byte>();
        for (var index = 0; index < 16; index++)
        {
            var anchorX = 20 + index * 32;
            PlaceTree(world, anchorX);
            variants.Add(TreeTileVisualSelector.Resolve(world, anchorX, 18, KnownTileIds.Wood));
        }

        Assert.Equal(TreeTileVisualSelector.VariantCount, variants.Count);
    }

    [Fact]
    public void Resolve_KeepsPlainWoodStructuresOnConfiguredDefaultTexture()
    {
        var world = CreateWorld(seed: 91, width: 96);
        for (var y = 12; y <= 18; y++)
        {
            world.SetTile(40, y, TileInstance.FromTileId(KnownTileIds.Wood));
        }

        Assert.Equal(0, TreeTileVisualSelector.Resolve(world, 40, 16, KnownTileIds.Wood));
        Assert.Equal(0, TreeTileVisualSelector.Resolve(world, 40, 16, KnownTileIds.Stone));
    }

    [Fact]
    public void Resolve_UsesOneStableVariantAcrossRegionalOakTree()
    {
        var world = CreateWorld(seed: 41_901, width: 96);
        for (var y = 10; y <= 19; y++)
        {
            world.SetTile(40, y, TileInstance.FromTileId(KnownTileIds.OakTrunk));
        }

        world.SetTile(37, 11, TileInstance.FromTileId(KnownTileIds.OakLeaves));
        world.SetTile(43, 12, TileInstance.FromTileId(KnownTileIds.OakLeaves));

        var expected = TreeTileVisualSelector.Resolve(world, 40, 18, KnownTileIds.OakTrunk);
        Assert.InRange(expected, (byte)0, (byte)(TreeTileVisualSelector.VariantCount - 1));
        Assert.Equal(expected, TreeTileVisualSelector.Resolve(world, 43, 12, KnownTileIds.OakLeaves));
        Assert.Equal(expected, TreeTileVisualSelector.Resolve(world, 37, 11, KnownTileIds.OakLeaves));
    }

    [Fact]
    public void Resolve_DoesNotAssociateMismatchedRegionalMaterials()
    {
        var world = CreateWorld(seed: 41_901, width: 96);
        for (var y = 10; y <= 19; y++)
        {
            world.SetTile(40, y, TileInstance.FromTileId(KnownTileIds.LivingWood));
        }

        world.SetTile(39, 11, TileInstance.FromTileId(KnownTileIds.OakLeaves));

        Assert.Equal(0, TreeTileVisualSelector.Resolve(world, 40, 18, KnownTileIds.LivingWood));
        Assert.Equal(0, TreeTileVisualSelector.Resolve(world, 39, 11, KnownTileIds.OakLeaves));
    }

    [Fact]
    public void ResolveTransform_IsStableDistributedAndFoliageOnly()
    {
        var world = CreateWorld(seed: 73_019, width: 96);
        var transforms = new HashSet<TileVisualTransform>();

        for (var x = 12; x < 44; x++)
        {
            var first = TreeTileVisualSelector.ResolveTransform(world, x, 10, KnownTileIds.OakLeaves);
            transforms.Add(first);
            Assert.Equal(first, TreeTileVisualSelector.ResolveTransform(world, x, 10, KnownTileIds.OakLeaves));
        }

        Assert.Equal(2, transforms.Count);
        Assert.Contains(TileVisualTransform.None, transforms);
        Assert.Contains(TileVisualTransform.FlipHorizontal, transforms);
        Assert.Equal(
            TileVisualTransform.None,
            TreeTileVisualSelector.ResolveTransform(world, 20, 10, KnownTileIds.OakTrunk));
    }

    [Theory]
    [InlineData(AutoTileMask.None, AutoTileMask.None)]
    [InlineData(AutoTileMask.Left, AutoTileMask.Right)]
    [InlineData(AutoTileMask.Right | AutoTileMask.Top, AutoTileMask.Left | AutoTileMask.Top)]
    [InlineData(AutoTileMask.Left | AutoTileMask.Bottom, AutoTileMask.Right | AutoTileMask.Bottom)]
    [InlineData(AutoTileMask.Left | AutoTileMask.Right | AutoTileMask.Top | AutoTileMask.Bottom,
        AutoTileMask.Left | AutoTileMask.Right | AutoTileMask.Top | AutoTileMask.Bottom)]
    public void ResolveSourceMask_HorizontalFlipPreservesDestinationConnectivity(
        AutoTileMask destination,
        AutoTileMask expectedSource)
    {
        Assert.Equal(
            expectedSource,
            TreeTileVisualSelector.ResolveSourceMask(destination, TileVisualTransform.FlipHorizontal));
        Assert.Equal(
            destination,
            TreeTileVisualSelector.ResolveSourceMask(destination, TileVisualTransform.None));
    }

    [Fact]
    public void FoliageTransformSelection_DoesNotAllocate()
    {
        var world = CreateWorld(seed: 55_731, width: 96);
        _ = TreeTileVisualSelector.ResolveTransform(world, 20, 10, KnownTileIds.OakLeaves);
        _ = TreeTileVisualSelector.ResolveSourceMask(
            AutoTileMask.Left | AutoTileMask.Top,
            TileVisualTransform.FlipHorizontal);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var iteration = 0; iteration < 10_000; iteration++)
        {
            var transform = TreeTileVisualSelector.ResolveTransform(
                world,
                20 + (iteration & 7),
                10 + (iteration & 3),
                KnownTileIds.OakLeaves);
            _ = TreeTileVisualSelector.ResolveSourceMask(
                AutoTileMask.Left | AutoTileMask.Top,
                transform);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Theory]
    [InlineData(KnownTileIds.OakLeaves, "tiles/loose_oak_leaves_v2")]
    [InlineData(KnownTileIds.AutumnLeaves, "tiles/loose_autumn_leaves_v2")]
    [InlineData(KnownTileIds.MarshLeaves, "tiles/loose_marsh_leaves_v2")]
    public void RegionalFoliage_UsesThreeCompleteVariantSheets(ushort tileId, string spritePrefix)
    {
        var spriteIds = Assert.IsType<string[]>(TilemapRenderer.ResolveTreeVariantSpriteIds(tileId));

        Assert.Equal(3, spriteIds.Length);
        Assert.Equal($"{spritePrefix}a_autotile", spriteIds[0]);
        Assert.Equal($"{spritePrefix}b_autotile", spriteIds[1]);
        Assert.Equal($"{spritePrefix}c_autotile", spriteIds[2]);
        Assert.Equal(3, spriteIds.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void RegionalTrunks_KeepTheirConfiguredAuthoredSheet()
    {
        Assert.Null(TilemapRenderer.ResolveTreeVariantSpriteIds(KnownTileIds.OakTrunk));
        Assert.Null(TilemapRenderer.ResolveTreeVariantSpriteIds(KnownTileIds.LivingWood));
        Assert.Null(TilemapRenderer.ResolveTreeVariantSpriteIds(KnownTileIds.MangroveRoot));
    }

    private static World CreateWorld(int seed, int width)
    {
        return new World(width, 40, WorldMetadata.CreateDefault(seed));
    }

    private static void PlaceTree(World world, int anchorX)
    {
        for (var y = 10; y <= 19; y++)
        {
            world.SetTile(anchorX, y, TileInstance.FromTileId(KnownTileIds.Wood));
        }

        world.SetTile(anchorX + 1, 14, TileInstance.FromTileId(KnownTileIds.Wood));
        world.SetTile(anchorX + 2, 14, TileInstance.FromTileId(KnownTileIds.Wood));
        foreach (var (dx, dy) in new[] { (-3, 11), (-2, 10), (0, 9), (2, 10), (3, 12), (4, 13) })
        {
            world.SetTile(anchorX + dx, dy, TileInstance.FromTileId(KnownTileIds.Leaves));
        }
    }
}
