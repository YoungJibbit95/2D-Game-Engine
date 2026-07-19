using Game.Client.Rendering;
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
    public void Resolve_LeavesRegionalOakMaterialPairOnConfiguredTextures()
    {
        var world = CreateWorld(seed: 41_901, width: 96);
        for (var y = 10; y <= 19; y++)
        {
            world.SetTile(40, y, TileInstance.FromTileId(KnownTileIds.OakTrunk));
        }

        world.SetTile(37, 11, TileInstance.FromTileId(KnownTileIds.OakLeaves));
        world.SetTile(43, 12, TileInstance.FromTileId(KnownTileIds.OakLeaves));

        Assert.Equal(0, TreeTileVisualSelector.Resolve(world, 40, 18, KnownTileIds.OakTrunk));
        Assert.Equal(0, TreeTileVisualSelector.Resolve(world, 43, 12, KnownTileIds.OakLeaves));
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
