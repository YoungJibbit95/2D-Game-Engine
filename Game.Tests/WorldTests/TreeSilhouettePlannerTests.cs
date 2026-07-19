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
    public void CurrentVersion_ContainsTaperedRootedTrunksAndOpposedReadableBranchesWithinBounds()
    {
        for (var variation = 0; variation < TreeSilhouettePlanner.VariationCount; variation++)
        {
            Assert.Equal(TreeSilhouetteCell.Trunk, TreeSilhouettePlanner.Classify(0, 7, 9, variation));
            Assert.Equal(1, Enumerable.Range(-1, 3)
                .Count(dx => dx != 0 &&
                    TreeSilhouettePlanner.Classify(dx, 8, 9, variation) == TreeSilhouetteCell.Trunk));
            Assert.Contains(
                Enumerable.Range(-TreeSilhouettePlanner.MaximumHalfWidth, TreeSilhouettePlanner.MaximumHalfWidth * 2 + 1),
                dx => dx < 0 && Enumerable.Range(0, 7)
                    .Any(dy => TreeSilhouettePlanner.Classify(dx, dy, 9, variation) == TreeSilhouetteCell.Trunk));
            Assert.Contains(
                Enumerable.Range(-TreeSilhouettePlanner.MaximumHalfWidth, TreeSilhouettePlanner.MaximumHalfWidth * 2 + 1),
                dx => dx > 0 && Enumerable.Range(0, 7)
                    .Any(dy => TreeSilhouettePlanner.Classify(dx, dy, 9, variation) == TreeSilhouetteCell.Trunk));
            Assert.Contains(
                Enumerable.Range(-TreeSilhouettePlanner.MaximumHalfWidth, TreeSilhouettePlanner.MaximumHalfWidth * 2 + 1),
                dx => dx < 0 && Enumerable.Range(-TreeSilhouettePlanner.TopPadding, 10)
                    .Any(dy => TreeSilhouettePlanner.Classify(dx, dy, 9, variation) == TreeSilhouetteCell.Leaves));
            Assert.Contains(
                Enumerable.Range(-TreeSilhouettePlanner.MaximumHalfWidth, TreeSilhouettePlanner.MaximumHalfWidth * 2 + 1),
                dx => dx > 0 && Enumerable.Range(-TreeSilhouettePlanner.TopPadding, 10)
                    .Any(dy => TreeSilhouettePlanner.Classify(dx, dy, 9, variation) == TreeSilhouetteCell.Leaves));
            for (var height = 5; height <= 12; height++)
            {
                AssertTreeConnected(height, variation);
                AssertTrunkIsAcyclic(height, variation);
            }
        }

        Assert.Equal(
            TreeSilhouetteCell.Empty,
            TreeSilhouettePlanner.Classify(TreeSilhouettePlanner.MaximumHalfWidth + 1, 0, 9, 0));
        Assert.Equal(
            TreeSilhouetteCell.Empty,
            TreeSilhouettePlanner.Classify(0, -TreeSilhouettePlanner.TopPadding - 1, 9, 0));
    }

    [Fact]
    public void CurrentVersion_UsesThreeOrFourBroadSeparatedCrownPads()
    {
        for (var variation = 0; variation < TreeSilhouettePlanner.VariationCount; variation++)
        {
            var components = CaptureLeafComponents(height: 9, variation);
            var leafCount = components.Sum(component => component.Count);

            Assert.InRange(components.Count, 3, 4);
            Assert.InRange(leafCount, 22, 34);
            foreach (var component in components)
            {
                var width = component.Max(cell => cell.X) - component.Min(cell => cell.X) + 1;
                var height = component.Max(cell => cell.Y) - component.Min(cell => cell.Y) + 1;
                Assert.InRange(component.Count, 5, 9);
                Assert.InRange(width, 3, 5);
                Assert.InRange(height, 2, 3);
            }

            // Even the fullest silhouette keeps most of its bounding field open.
            // This rejects a return to the old monolithic canopy wall while allowing
            // each individual crown pad to have a useful, leafy interior.
            var fieldArea = (TreeSilhouettePlanner.MaximumHalfWidth * 2 + 1) *
                (TreeSilhouettePlanner.TopPadding + 7);
            Assert.True(leafCount * 4 < fieldArea);
        }
    }

    [Fact]
    public void CurrentVersion_ClassifyRemainsAllocationFreeAfterWarmup()
    {
        var checksum = 0;
        for (var warmup = 0; warmup < 512; warmup++)
        {
            checksum += (int)TreeSilhouettePlanner.Classify(
                (warmup % 13) - TreeSilhouettePlanner.MaximumHalfWidth,
                (warmup % 12) - TreeSilhouettePlanner.TopPadding,
                height: 9,
                variation: warmup);
        }

        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        for (var iteration = 0; iteration < 100_000; iteration++)
        {
            checksum += (int)TreeSilhouettePlanner.Classify(
                (iteration % 13) - TreeSilhouettePlanner.MaximumHalfWidth,
                (iteration % 12) - TreeSilhouettePlanner.TopPadding,
                height: 9,
                variation: iteration);
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        Assert.NotEqual(0, checksum);
        Assert.Equal(0, allocated);
    }

    private static List<HashSet<(int X, int Y)>> CaptureLeafComponents(int height, int variation)
    {
        var remaining = new HashSet<(int X, int Y)>();
        for (var dy = -TreeSilhouettePlanner.TopPadding; dy < height; dy++)
        {
            for (var dx = -TreeSilhouettePlanner.MaximumHalfWidth;
                 dx <= TreeSilhouettePlanner.MaximumHalfWidth;
                 dx++)
            {
                if (TreeSilhouettePlanner.Classify(dx, dy, height, variation) == TreeSilhouetteCell.Leaves)
                {
                    remaining.Add((dx, dy));
                }
            }
        }

        var components = new List<HashSet<(int X, int Y)>>();
        while (remaining.Count > 0)
        {
            var start = remaining.First();
            remaining.Remove(start);
            var component = new HashSet<(int X, int Y)> { start };
            var open = new Queue<(int X, int Y)>();
            open.Enqueue(start);
            while (open.TryDequeue(out var current))
            {
                Visit(current.X - 1, current.Y);
                Visit(current.X + 1, current.Y);
                Visit(current.X, current.Y - 1);
                Visit(current.X, current.Y + 1);
            }

            components.Add(component);

            void Visit(int x, int y)
            {
                if (remaining.Remove((x, y)))
                {
                    component.Add((x, y));
                    open.Enqueue((x, y));
                }
            }
        }

        return components;
    }

    private static void AssertTrunkIsAcyclic(int height, int variation)
    {
        var trunk = new HashSet<(int X, int Y)>();
        for (var dy = 0; dy < height; dy++)
        {
            for (var dx = -TreeSilhouettePlanner.MaximumHalfWidth;
                 dx <= TreeSilhouettePlanner.MaximumHalfWidth;
                 dx++)
            {
                if (TreeSilhouettePlanner.Classify(dx, dy, height, variation) == TreeSilhouetteCell.Trunk)
                {
                    trunk.Add((dx, dy));
                }
            }
        }

        var edgeCount = trunk.Sum(cell =>
            (trunk.Contains((cell.X + 1, cell.Y)) ? 1 : 0) +
            (trunk.Contains((cell.X, cell.Y + 1)) ? 1 : 0));
        Assert.Equal(trunk.Count - 1, edgeCount);
    }

    private static void AssertTreeConnected(int height, int variation)
    {
        var remaining = new HashSet<(int X, int Y)>();
        for (var dy = -TreeSilhouettePlanner.TopPadding; dy < height; dy++)
        {
            for (var dx = -TreeSilhouettePlanner.MaximumHalfWidth;
                 dx <= TreeSilhouettePlanner.MaximumHalfWidth;
                 dx++)
            {
                if (TreeSilhouettePlanner.Classify(dx, dy, height, variation) != TreeSilhouetteCell.Empty)
                {
                    remaining.Add((dx, dy));
                }
            }
        }

        var root = (X: 0, Y: height - 1);
        var visited = new HashSet<(int X, int Y)> { root };
        var open = new Queue<(int X, int Y)>();
        open.Enqueue(root);
        while (open.TryDequeue(out var current))
        {
            Visit(current.X - 1, current.Y);
            Visit(current.X + 1, current.Y);
            Visit(current.X, current.Y - 1);
            Visit(current.X, current.Y + 1);
        }

        Assert.Equal(remaining.Count, visited.Count);

        void Visit(int x, int y)
        {
            if (remaining.Contains((x, y)) && visited.Add((x, y)))
            {
                open.Enqueue((x, y));
            }
        }
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
