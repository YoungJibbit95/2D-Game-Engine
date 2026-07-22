using Game.Core.Tiles;
using Game.Core.World;

namespace Game.Client.Rendering;

/// <summary>
/// Adds presentation-only sockets between the wood and foliage materials of one
/// tree species. Simulation tile identity and global merge groups remain unchanged.
/// </summary>
internal static class TreeAutoTilePresentation
{
    public static AutoTileMask AddCompatibleMaterialConnections(
        World world,
        int tileX,
        int tileY,
        ushort tileId,
        AutoTileMask baseMask)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (!TryGetCompanion(tileId, out var companionTileId))
        {
            return baseMask;
        }

        var mask = baseMask;
        if (IsCompanion(world, tileX, tileY - 1, companionTileId))
        {
            mask |= AutoTileMask.Top;
        }

        if (IsCompanion(world, tileX + 1, tileY, companionTileId))
        {
            mask |= AutoTileMask.Right;
        }

        if (IsCompanion(world, tileX, tileY + 1, companionTileId))
        {
            mask |= AutoTileMask.Bottom;
        }

        if (IsCompanion(world, tileX - 1, tileY, companionTileId))
        {
            mask |= AutoTileMask.Left;
        }

        return mask;
    }

    private static bool IsCompanion(World world, int tileX, int tileY, ushort companionTileId)
    {
        return world.IsInBounds(tileX, tileY) && world.GetTile(tileX, tileY).TileId == companionTileId;
    }

    private static bool TryGetCompanion(ushort tileId, out ushort companionTileId)
    {
        switch (tileId)
        {
            case KnownTileIds.Wood:
                companionTileId = KnownTileIds.Leaves;
                return true;
            case KnownTileIds.Leaves:
                companionTileId = KnownTileIds.Wood;
                return true;
            case KnownTileIds.OakTrunk:
                companionTileId = KnownTileIds.OakLeaves;
                return true;
            case KnownTileIds.OakLeaves:
                companionTileId = KnownTileIds.OakTrunk;
                return true;
            case KnownTileIds.LivingWood:
                companionTileId = KnownTileIds.AutumnLeaves;
                return true;
            case KnownTileIds.AutumnLeaves:
                companionTileId = KnownTileIds.LivingWood;
                return true;
            case KnownTileIds.MangroveRoot:
                companionTileId = KnownTileIds.MarshLeaves;
                return true;
            case KnownTileIds.MarshLeaves:
                companionTileId = KnownTileIds.MangroveRoot;
                return true;
            default:
                companionTileId = KnownTileIds.Air;
                return false;
        }
    }
}
