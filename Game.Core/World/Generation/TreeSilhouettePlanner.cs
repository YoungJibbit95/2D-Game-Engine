namespace Game.Core.World.Generation;

internal enum TreeSilhouetteCell
{
    Empty,
    Trunk,
    Leaves
}

internal static class TreeSilhouettePlanner
{
    public const int MaximumHalfWidth = 7;
    public const int TopPadding = 4;
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

        return IsLeaf(dx, dyFromTop, normalizedVariation)
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
        if (dx == 0 && dyFromTop >= 0 && dyFromTop < height)
        {
            return true;
        }

        if (height >= 5 && dyFromTop == height - 1 && Math.Abs(dx) == 1)
        {
            return true;
        }

        var primaryDirection = (variation & 1) == 0 ? -1 : 1;
        var upperStart = Math.Min(height - 2, 2 + ((variation >> 1) & 1));
        if (height >= 5 && IsBranch(dx, dyFromTop, upperStart, primaryDirection, 3 + (variation % 3 == 0 ? 1 : 0)))
        {
            return true;
        }

        var middleStart = Math.Min(height - 2, 4 + ((variation >> 2) & 1));
        if (height >= 7 && IsBranch(dx, dyFromTop, middleStart, -primaryDirection, 2 + ((variation + 1) % 3)))
        {
            return true;
        }

        var lowerStart = Math.Min(height - 2, 6 + (variation & 1));
        return height >= 9 &&
               IsBranch(dx, dyFromTop, lowerStart, variation % 4 < 2 ? -1 : 1, 2);
    }

    private static bool IsLeaf(int dx, int dyFromTop, int variation)
    {
        if (dyFromTop is < -4 or > 6)
        {
            return false;
        }

        var primaryDirection = (variation & 1) == 0 ? -1 : 1;
        var crownShift = (variation % 3) - 1;
        var crownTop = -2 - ((variation / 3) & 1);
        var primaryX = primaryDirection * (3 + ((variation >> 2) & 1));
        var secondaryX = -primaryDirection * (3 + ((variation >> 1) & 1));
        var inCrown = InRoundedCluster(dx - crownShift, dyFromTop - crownTop, 3, 2);
        var inCore = InRoundedCluster(dx, dyFromTop - 1, 3 + (variation % 4 == 0 ? 1 : 0), 2);
        var inPrimary = InRoundedCluster(dx - primaryX, dyFromTop - (variation % 2), 3, 2);
        var inSecondary = InRoundedCluster(dx - secondaryX, dyFromTop - (2 + ((variation >> 2) & 1)), 3, 2);
        var hasLowCrown = variation % 3 != 1;
        var lowerX = primaryDirection * (1 + ((variation >> 1) & 1));
        var inLower = hasLowCrown && InRoundedCluster(dx - lowerX, dyFromTop - 4, 2, 2);
        if (!(inCrown || inCore || inPrimary || inSecondary || inLower))
        {
            return false;
        }

        // Deterministic openings preserve readable negative space between the overlapping crowns.
        // The crown core remains filled so branches never look detached from the trunk.
        if (Math.Abs(dx) > 1 && dyFromTop >= -1)
        {
            var opening = PositiveMod((dx * 17) + (dyFromTop * 31) + (variation * 13), 9);
            if (opening == 0)
            {
                return false;
            }
        }

        return true;
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

        var branchY = startY - (step / 2);
        return dyFromTop == branchY || (step > 1 && (step & 1) == 0 && dyFromTop == branchY + 1);
    }

    private static bool InRoundedCluster(int dx, int dy, int radiusX, int radiusY)
    {
        if (Math.Abs(dx) > radiusX || Math.Abs(dy) > radiusY)
        {
            return false;
        }

        var normalizedX = dx * dx * radiusY * radiusY;
        var normalizedY = dy * dy * radiusX * radiusX;
        return normalizedX + normalizedY <= radiusX * radiusX * radiusY * radiusY;
    }

    private static int PositiveMod(int value, int divisor)
    {
        var remainder = value % divisor;
        return remainder < 0 ? remainder + divisor : remainder;
    }
}
