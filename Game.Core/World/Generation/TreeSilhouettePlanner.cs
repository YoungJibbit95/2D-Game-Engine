namespace Game.Core.World.Generation;

internal enum TreeSilhouetteCell
{
    Empty,
    Trunk,
    Leaves
}

internal static class TreeSilhouettePlanner
{
    public const int MaximumHalfWidth = 6;
    public const int TopPadding = 3;
    public const int VariationCount = 12;

    public static TreeSilhouetteCell Classify(
        int dx,
        int dyFromTop,
        int height,
        int variation,
        int generationVersion = WorldGenerationVersions.Current)
    {
        if (height <= 0)
        {
            return TreeSilhouetteCell.Empty;
        }

        if (WorldGenerationVersions.Normalize(generationVersion) == WorldGenerationVersions.Legacy)
        {
            return ClassifyLegacy(dx, dyFromTop, height);
        }

        var normalizedVariation = PositiveMod(variation, VariationCount);
        if (IsTrunk(dx, dyFromTop, height, normalizedVariation))
        {
            return TreeSilhouetteCell.Trunk;
        }

        return IsLeaf(dx, dyFromTop, height, normalizedVariation)
            ? TreeSilhouetteCell.Leaves
            : TreeSilhouetteCell.Empty;
    }

    private static TreeSilhouetteCell ClassifyLegacy(int dx, int dyFromTop, int height)
    {
        if (dx == 0 && dyFromTop >= 0 && dyFromTop < height)
        {
            return TreeSilhouetteCell.Trunk;
        }

        return dyFromTop is >= -2 and <= 1 && Math.Abs(dx) + Math.Abs(dyFromTop) <= 3
            ? TreeSilhouetteCell.Leaves
            : TreeSilhouetteCell.Empty;
    }

    private static bool IsTrunk(int dx, int dyFromTop, int height, int variation)
    {
        if (dyFromTop < 0 || dyFromTop >= height)
        {
            return false;
        }

        var primaryDirection = (variation & 1) == 0 ? -1 : 1;
        var upperStart = Math.Min(height - 2, 3 + ((variation >> 1) & 1));
        if (dx == 0)
        {
            return true;
        }

        // A single root spur plants the one-cell centerline without widening the
        // whole trunk into a pillar. It uses the side without an adjacent low branch,
        // avoiding a two-by-two autotile loop at the base of short silhouettes.
        var rootDirection = upperStart < height - 2 ? primaryDirection : -primaryDirection;
        if (height >= 5 && dyFromTop == height - 1 && dx == rootDirection)
        {
            return true;
        }

        // Two long, vertically staggered stair branches form the readable crown
        // skeleton. Each rise has an orthogonal elbow, so the apparent diagonal is
        // still one connected autotile path and never closes into a U-shaped loop.
        if (IsBranch(
                dx,
                dyFromTop,
                upperStart,
                primaryDirection,
                length: 3))
        {
            return true;
        }

        var middleLength = SecondaryBranchLength(variation);
        var middleStart = Math.Min(height - 2, 4 + ((variation >> 2) & 1));
        if (height >= 7 && IsBranch(
                dx,
                dyFromTop,
                middleStart,
                -primaryDirection,
                middleLength))
        {
            return true;
        }

        return false;
    }

    private static bool IsLeaf(int dx, int dyFromTop, int height, int variation)
    {
        if (dyFromTop < -TopPadding || dyFromTop > height - 2)
        {
            return false;
        }

        var primaryDirection = (variation & 1) == 0 ? -1 : 1;
        var upperStart = Math.Min(height - 2, 3 + ((variation >> 1) & 1));
        const int primaryLength = 3;
        var primaryTipY = BranchY(upperStart, primaryLength);
        var secondaryLength = SecondaryBranchLength(variation);
        var middleStart = Math.Min(height - 2, 4 + ((variation >> 2) & 1));
        var secondaryTipY = BranchY(middleStart, secondaryLength);

        // V5 follows the denser side-view sandbox language: a broad upper crown
        // overlaps two branch-tip lobes instead of leaving three isolated pom-poms.
        // Large-scale holes stay out of the cell geometry; native foliage pixels
        // provide the smaller gaps without exposing sixteen-pixel windows.
        var crownLean = ((variation >> 2) & 1) == 0 ? 0 : -primaryDirection;
        if (InMainCrown(
                dx - crownLean,
                dyFromTop,
                variation))
        {
            return true;
        }

        var primaryCenterX = primaryDirection * primaryLength;
        if (InBranchCrown(
                dx - primaryCenterX,
                dyFromTop - (primaryTipY - 1),
                variation: variation,
                salt: 4))
        {
            return true;
        }

        var secondaryCenterX = -primaryDirection * secondaryLength;
        if (height >= 7 && InBranchCrown(
                dx - secondaryCenterX,
                dyFromTop - (secondaryTipY - 1),
                variation: variation,
                salt: 8))
        {
            return true;
        }

        return false;
    }

    private static bool IsBranch(int dx, int dyFromTop, int startY, int direction, int length)
    {
        if (dx == 0 || Math.Sign(dx) != direction)
        {
            return false;
        }

        var step = Math.Abs(dx);
        if (step > length)
        {
            return false;
        }

        var branchY = BranchY(startY, step);
        if (dyFromTop == branchY)
        {
            return true;
        }

        // A single vertical elbow at each rise keeps the diagonal branch connected
        // for autotiling without restoring the old two-row rectangular limbs.
        return step > 1 &&
            branchY != BranchY(startY, step - 1) &&
            dyFromTop == BranchY(startY, step - 1);
    }

    private static bool InMainCrown(
        int dx,
        int dy,
        int variation)
    {
        if (dy is < -3 or > 1 || Math.Abs(dx) > 3)
        {
            return false;
        }

        var lean = (variation & 1) == 0 ? -1 : 1;
        if (dy == -3)
        {
            // A shifted three-cell cap prevents every crown from sharing the same
            // flat top while retaining enough mass to read at camera distance.
            return dx >= lean - 1 && dx <= lean + 1;
        }

        if (dy == -2)
        {
            return Math.Abs(dx) <= 2 || dx == lean * 3;
        }

        if (dy == -1)
        {
            return dx != -lean * 3;
        }

        if (dy == 0)
        {
            var bittenSide = ((variation >> 1) & 1) == 0 ? -lean : lean;
            return dx != bittenSide * 3;
        }

        // The lower skirt wraps the upper trunk but recedes sharply at both sides.
        return Math.Abs(dx) <= 1 || dx == lean * 2;
    }

    private static bool InBranchCrown(
        int dx,
        int dy,
        int variation,
        int salt)
    {
        if (dy is < -2 or > 1 || Math.Abs(dx) > 2)
        {
            return false;
        }

        var lobeDirection = ((variation + salt) & 1) == 0 ? -1 : 1;
        if (dy == -2)
        {
            return dx >= lobeDirection - 1 && dx <= lobeDirection + 1;
        }

        if (dy == -1)
        {
            return dx != -lobeDirection * 2;
        }

        if (dy == 0)
        {
            return dx != lobeDirection * 2;
        }

        // Keep the branch attachment buried in foliage while tapering the lobe.
        return Math.Abs(dx) <= 1;
    }

    private static int SecondaryBranchLength(int variation)
    {
        return 2 + ((variation >> 3) & 1);
    }

    private static int BranchY(int startY, int step)
    {
        return startY - (step / 2);
    }

    private static int PositiveMod(int value, int divisor)
    {
        var remainder = value % divisor;
        return remainder < 0 ? remainder + divisor : remainder;
    }
}
