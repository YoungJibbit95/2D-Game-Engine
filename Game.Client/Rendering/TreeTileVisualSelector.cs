using Game.Core.Tiles;
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
        if (!TryResolveMaterialPair(tileId, out var trunkTileId, out var canopyTileId))
        {
            return 0;
        }

        var bestAnchorX = tileX;
        var bestWoodCount = 0;
        var bestDistance = int.MaxValue;
        var foundLeaves = tileId == canopyTileId;
        var minimumY = Math.Max(0, tileY - VerticalSearchRadius);
        var maximumY = Math.Min(world.HeightTiles - 1, tileY + VerticalSearchRadius);
        for (var offsetX = -HorizontalSearchRadius; offsetX <= HorizontalSearchRadius; offsetX++)
        {
            var candidateX = SaturateToInt((long)tileX + offsetX);
            var woodCount = 0;
            for (var scanY = minimumY; scanY <= maximumY; scanY++)
            {
                var candidate = world.GetTile(candidateX, scanY).TileId;
                woodCount += candidate == trunkTileId ? 1 : 0;
                foundLeaves |= candidate == canopyTileId;
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

    /// <summary>
    /// Breaks up repeated foliage stamps without adding texture resources or draw calls.
    /// The transform is cached with the chunk command and remains stable for a world seed.
    /// Trunks intentionally keep their authored orientation and lighting direction.
    /// </summary>
    public static TileVisualTransform ResolveTransform(
        World world,
        int tileX,
        int tileY,
        ushort tileId)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (!KnownTileIds.IsFoliage(tileId))
        {
            return TileVisualTransform.None;
        }

        var hash = StableHash(world.Metadata.Seed, tileX);
        hash ^= StableHash(unchecked((int)hash), tileY);
        hash ^= (uint)tileId * 0x9E3779B9u;
        return (hash & 1u) == 0u
            ? TileVisualTransform.None
            : TileVisualTransform.FlipHorizontal;
    }

    public static AutoTileMask ResolveSourceMask(
        AutoTileMask destinationMask,
        TileVisualTransform transform)
    {
        if ((transform & TileVisualTransform.FlipHorizontal) == 0)
        {
            return destinationMask;
        }

        var sourceMask = destinationMask & ~(AutoTileMask.Left | AutoTileMask.Right);
        if ((destinationMask & AutoTileMask.Left) != 0)
        {
            sourceMask |= AutoTileMask.Right;
        }

        if ((destinationMask & AutoTileMask.Right) != 0)
        {
            sourceMask |= AutoTileMask.Left;
        }

        return sourceMask;
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

    private static bool TryResolveMaterialPair(
        ushort tileId,
        out ushort trunkTileId,
        out ushort canopyTileId)
    {
        switch (tileId)
        {
            case KnownTileIds.Wood:
            case KnownTileIds.Leaves:
                trunkTileId = KnownTileIds.Wood;
                canopyTileId = KnownTileIds.Leaves;
                return true;
            case KnownTileIds.OakTrunk:
            case KnownTileIds.OakLeaves:
                trunkTileId = KnownTileIds.OakTrunk;
                canopyTileId = KnownTileIds.OakLeaves;
                return true;
            case KnownTileIds.LivingWood:
            case KnownTileIds.AutumnLeaves:
                trunkTileId = KnownTileIds.LivingWood;
                canopyTileId = KnownTileIds.AutumnLeaves;
                return true;
            case KnownTileIds.MangroveRoot:
            case KnownTileIds.MarshLeaves:
                trunkTileId = KnownTileIds.MangroveRoot;
                canopyTileId = KnownTileIds.MarshLeaves;
                return true;
            default:
                trunkTileId = KnownTileIds.Air;
                canopyTileId = KnownTileIds.Air;
                return false;
        }
    }

    private static int SaturateToInt(long value)
    {
        return (int)Math.Clamp(value, int.MinValue, int.MaxValue);
    }
}
