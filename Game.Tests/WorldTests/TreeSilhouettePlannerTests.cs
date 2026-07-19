using Game.Core.World.Generation;
using Xunit;

namespace Game.Tests.WorldTests;

public sealed class TreeSilhouettePlannerTests
{
    [Fact]
    public void LegacyVersion_PreservesStraightTrunkAndDiamondCanopy()
    {
        Assert.Equal(
            TreeSilhouetteCell.Trunk,
            TreeSilhouettePlanner.Classify(0, 0, 7, variation: 99, WorldGenerationVersions.Legacy));
        Assert.Equal(
            TreeSilhouetteCell.Leaves,
            TreeSilhouettePlanner.Classify(3, 0, 7, variation: 99, WorldGenerationVersions.Legacy));
        Assert.Equal(
            TreeSilhouetteCell.Leaves,
            TreeSilhouettePlanner.Classify(-1, -2, 7, variation: 99, WorldGenerationVersions.Legacy));
        Assert.Equal(
            TreeSilhouetteCell.Empty,
            TreeSilhouettePlanner.Classify(3, 1, 7, variation: 99, WorldGenerationVersions.Legacy));
    }

    [Fact]
    public void CurrentVersion_ProvidesTwelveDeterministicSilhouettes()
    {
        var silhouettes = Enumerable.Range(0, TreeSilhouettePlanner.VariationCount)
            .Select(variation => Capture(height: 9, variation))
            .ToArray();

        Assert.Equal(TreeSilhouettePlanner.VariationCount, silhouettes.Distinct().Count());
        Assert.Equal(silhouettes, Enumerable.Range(0, TreeSilhouettePlanner.VariationCount)
            .Select(variation => Capture(height: 9, variation)));
    }

    [Fact]
    public void CurrentVersion_ContainsRootsBranchesAndOpenCrownSpaceWithinBounds()
    {
        for (var variation = 0; variation < TreeSilhouettePlanner.VariationCount; variation++)
        {
            Assert.Equal(
                TreeSilhouetteCell.Trunk,
                TreeSilhouettePlanner.Classify(-1, 8, 9, variation));
            Assert.Equal(
                TreeSilhouetteCell.Trunk,
                TreeSilhouettePlanner.Classify(1, 8, 9, variation));
            Assert.Contains(
                Enumerable.Range(-TreeSilhouettePlanner.MaximumHalfWidth, TreeSilhouettePlanner.MaximumHalfWidth * 2 + 1),
                dx => Math.Abs(dx) >= 3 && Enumerable.Range(0, 8)
                    .Any(dy => TreeSilhouettePlanner.Classify(dx, dy, 9, variation) == TreeSilhouetteCell.Trunk));
            Assert.Contains(
                Enumerable.Range(-TreeSilhouettePlanner.MaximumHalfWidth, TreeSilhouettePlanner.MaximumHalfWidth * 2 + 1),
                dx => dx < 0 && Enumerable.Range(-TreeSilhouettePlanner.TopPadding, 10)
                    .Any(dy => TreeSilhouettePlanner.Classify(dx, dy, 9, variation) == TreeSilhouetteCell.Leaves));
            Assert.Contains(
                Enumerable.Range(-TreeSilhouettePlanner.MaximumHalfWidth, TreeSilhouettePlanner.MaximumHalfWidth * 2 + 1),
                dx => dx > 0 && Enumerable.Range(-TreeSilhouettePlanner.TopPadding, 10)
                    .Any(dy => TreeSilhouettePlanner.Classify(dx, dy, 9, variation) == TreeSilhouetteCell.Leaves));
        }

        Assert.Equal(
            TreeSilhouetteCell.Empty,
            TreeSilhouettePlanner.Classify(TreeSilhouettePlanner.MaximumHalfWidth + 1, 0, 9, 0));
        Assert.Equal(
            TreeSilhouetteCell.Empty,
            TreeSilhouettePlanner.Classify(0, -TreeSilhouettePlanner.TopPadding - 1, 9, 0));
    }

    private static string Capture(int height, int variation)
    {
        var cells = new char[(TreeSilhouettePlanner.MaximumHalfWidth * 2 + 1) *
                             (height + TreeSilhouettePlanner.TopPadding)];
        var index = 0;
        for (var dy = -TreeSilhouettePlanner.TopPadding; dy < height; dy++)
        {
            for (var dx = -TreeSilhouettePlanner.MaximumHalfWidth;
                 dx <= TreeSilhouettePlanner.MaximumHalfWidth;
                 dx++)
            {
                cells[index++] = TreeSilhouettePlanner.Classify(dx, dy, height, variation) switch
                {
                    TreeSilhouetteCell.Trunk => 'T',
                    TreeSilhouetteCell.Leaves => 'L',
                    _ => '.'
                };
            }
        }

        return new string(cells);
    }
}
