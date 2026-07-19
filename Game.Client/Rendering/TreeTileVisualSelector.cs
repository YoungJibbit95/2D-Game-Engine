using Game.Core.World;

namespace Game.Client.Rendering;

internal static class TreeTileVisualSelector
{
    public const byte VariantCount = 3;
    private const int HorizontalSearchRadius = 7;
    private const int VerticalSearchRadius = 12;

    public static byte Resolve(World world, int tileX, int tileY, ushort tileId)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (!IsTemperateTreeTile(tileId))
        {
            return 0;
        }

        var bestAnchorX = tileX;
        var bestWoodCount = 0;
        var bestDistance = int.MaxValue;
        var foundLeaves = IsTemperateCanopy(tileId);
        var minimumY = Math.Max(0, tileY - VerticalSearchRadius);
        var maximumY = Math.Min(world.HeightTiles - 1, tileY + VerticalSearchRadius);
        for (var offsetX = -HorizontalSearchRadius; offsetX <= HorizontalSearchRadius; offsetX++)
        {
            var candidateX = SaturateToInt((long)tileX + offsetX);
            var woodCount = 0;
            for (var scanY = minimumY; scanY <= maximumY; scanY++)
            {
                var candidate = world.GetTile(candidateX, scanY).TileId;
                woodCount += IsTemperateTrunk(candidate) ? 1 : 0;
                foundLeaves |= IsTemperateCanopy(candidate);
            }

            var distance = Math.Abs(offsetX);
            if (woodCount > bestWoodCount ||
                (woodCount == bestWoodCount && distance < bestDistance) ||
                (woodCount == bestWoodCount && distance == bestDistance && candidateX < bestAnchorX))
            {
                bestAnchorX = candidateX;
                bestWoodCount = woodCount;
                bestDistance = distance;
            }
        }

        // Plain wooden structures retain their configured wood texture. Only connected
        // trunk/canopy formations opt into the three project-owned tree palettes.
        if (bestWoodCount == 0 || !foundLeaves)
        {
            return 0;
        }

        return (byte)(StableHash(world.Metadata.Seed, bestAnchorX) % VariantCount);
    }

    private static uint StableHash(int seed, int anchorX)
    {
        unchecked
        {
            var hash = (uint)seed ^ 0x9E3779B9u;
            hash ^= (uint)anchorX + 0x85EBCA6Bu + (hash << 6) + (hash >> 2);
            hash ^= hash >> 16;
            hash *= 0x7FEB352Du;
            hash ^= hash >> 15;
            hash *= 0x846CA68Bu;
            return hash ^ (hash >> 16);
        }
    }

    private static bool IsTemperateTreeTile(ushort tileId)
    {
        return IsTemperateTrunk(tileId) || IsTemperateCanopy(tileId);
    }

    private static bool IsTemperateTrunk(ushort tileId)
    {
        return tileId == KnownTileIds.Wood;
    }

    private static bool IsTemperateCanopy(ushort tileId)
    {
        return tileId == KnownTileIds.Leaves;
    }

    private static int SaturateToInt(long value)
    {
        return (int)Math.Clamp(value, int.MinValue, int.MaxValue);
    }
}
