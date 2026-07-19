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
        if (dx == 0)
        {
            return true;
        }

        // A single root spur plants the one-cell centerline without widening the
        // whole trunk into a pillar.
        if (height >= 9 && dyFromTop == height - 1 && dx == -primaryDirection)
        {
            return true;
        }

        // Two long, vertically staggered stair branches form the readable crown
        // skeleton. Each rise has an orthogonal elbow, so the apparent diagonal is
        // still one connected autotile path and never closes into a U-shaped loop.
        var upperStart = Math.Min(height - 2, 3 + ((variation >> 1) & 1));
        if (IsBranch(
                dx,
                dyFromTop,
                upperStart,
                primaryDirection,
                length: 4))
        {
            return true;
        }

        var middleLength = SecondaryBranchLength(variation);
        var middleStart = Math.Min(height - 2, 5 + ((variation >> 2) & 1));
        if (IsBranch(
                dx,
                dyFromTop,
                middleStart,
                -primaryDirection,
                middleLength))
        {
            return true;
        }

        // Some silhouettes carry one low accent crown. Its short branch remains
        // well below the main pads, preserving a band of visible sky between them.
        return HasAccentCrown(variation) &&
            height >= 9 &&
            IsBranch(
                dx,
                dyFromTop,
                height - 1,
                primaryDirection,
                length: 3);
    }

    private static bool IsLeaf(int dx, int dyFromTop, int height, int variation)
    {
        if (dyFromTop < -TopPadding || dyFromTop > height - 2)
        {
            return false;
        }

        var primaryDirection = (variation & 1) == 0 ? -1 : 1;
        var upperStart = Math.Min(height - 2, 3 + ((variation >> 1) & 1));
        const int primaryLength = 4;
        var primaryTipY = BranchY(upperStart, primaryLength);
        var secondaryLength = SecondaryBranchLength(variation);
        var middleStart = Math.Min(height - 2, 5 + ((variation >> 2) & 1));
        var secondaryTipY = BranchY(middleStart, secondaryLength);

        // V4 uses three clearly separated, asymmetric crown pads. Each pad is four
        // tiles wide and three high at its silhouette bounds, with deterministic
        // bites around the perimeter instead of square or diamond leaf blobs.
        // The bottom row sits directly above its branch tip so every pad is attached.
        var topCenterX = -primaryDirection;
        if (InLooseCrownPad(
                dx - topCenterX,
                dyFromTop + 1,
                variation: variation,
                salt: 1))
        {
            return true;
        }

        var primaryCenterX = primaryDirection * primaryLength;
        if (InLooseCrownPad(
                dx - primaryCenterX,
                dyFromTop - (primaryTipY - 1),
                variation: variation,
                salt: 4,
                clippedMiddleSide: -primaryDirection))
        {
            return true;
        }

        var secondaryCenterX = -primaryDirection * secondaryLength;
        if (InLooseCrownPad(
                dx - secondaryCenterX,
                dyFromTop - (secondaryTipY - 1),
                variation: variation,
                salt: 8))
        {
            return true;
        }

        if (!HasAccentCrown(variation) || height < 9)
        {
            return false;
        }

        var accentTipY = BranchY(height - 1, step: 3);
        return InAccentCrownPad(
            dx - (primaryDirection * 3),
            dyFromTop - (accentTipY - 1),
            variation);
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

    private static bool InLooseCrownPad(
        int dx,
        int dy,
        int variation,
        int salt,
        int clippedMiddleSide = 0)
    {
        if (dy is < -2 or > 0 || Math.Abs(dx) > 2)
        {
            return false;
        }

        if (dy == 0)
        {
            return Math.Abs(dx) <= 1;
        }

        if (dy == -1)
        {
            var clippedSide = clippedMiddleSide != 0
                ? clippedMiddleSide
                : (((variation + salt) & 1) == 0 ? -1 : 1);
            return dx != clippedSide * 2;
        }

        // The two-cell top cap shifts between variations, keeping a broad crown
        // reading while avoiding a repeated three-by-five stamp.
        var clippedTopSide = ((variation + salt * 3) & 1) == 0 ? -1 : 1;
        return Math.Abs(dx) <= 1 && dx != clippedTopSide;
    }

    private static bool InAccentCrownPad(int dx, int dy, int variation)
    {
        if (dy is < -1 or > 0 || Math.Abs(dx) > 1)
        {
            return false;
        }

        return dy == -1 || dx != (((variation >> 1) & 1) == 0 ? -1 : 1);
    }

    private static int SecondaryBranchLength(int variation)
    {
        return 3 + ((variation >> 3) & 1);
    }

    private static bool HasAccentCrown(int variation)
    {
        return variation % 3 != 1;
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
