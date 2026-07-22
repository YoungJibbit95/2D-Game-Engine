using Game.Core.Tiles;
using Game.Core.World;
using Microsoft.Xna.Framework;

namespace Game.Client.Rendering;

[Flags]
public enum TerrainSurfaceDetailFlags : byte
{
    None = 0,
    TopFringe = 1 << 0,
    SideRoot = 1 << 1,
    AccentCluster = 1 << 2,
    HangingFringe = 1 << 3,
    InteriorFacet = 1 << 4
}

public readonly record struct TerrainSurfaceDetailPlan(
    TerrainSurfaceDetailFlags Flags,
    byte Variant)
{
    public bool Has(TerrainSurfaceDetailFlags flag) => (Flags & flag) != 0;
}

/// <summary>
/// Resolves and renders sparse, deterministic surface clusters for terrain tiles.
/// The planner is allocation-free and the renderer emits only bounded pixel primitives.
/// </summary>
public static class TerrainSurfaceDetailRenderer
{
    public static TerrainSurfaceDetailPlan Plan(
        ushort tileId,
        AutoTileMask mask,
        int tileX,
        int tileY)
    {
        if (!IsSupported(tileId))
        {
            return default;
        }

        var hash = StableHash(tileX, tileY, tileId);
        var flags = TerrainSurfaceDetailFlags.None;
        var topExposed = (mask & AutoTileMask.Top) == 0;
        var leftExposed = (mask & AutoTileMask.Left) == 0;
        var rightExposed = (mask & AutoTileMask.Right) == 0;
        var bottomExposed = (mask & AutoTileMask.Bottom) == 0;

        if (topExposed && tileId is KnownTileIds.Grass or KnownTileIds.MarshMoss or KnownTileIds.Snow or KnownTileIds.Stone or KnownTileIds.Amberstone)
        {
            flags |= TerrainSurfaceDetailFlags.TopFringe;
        }

        if ((leftExposed || rightExposed) && tileId is KnownTileIds.Dirt or KnownTileIds.Grass or KnownTileIds.MarshMoss)
        {
            flags |= TerrainSurfaceDetailFlags.SideRoot;
        }

        if (bottomExposed && tileId == KnownTileIds.MarshMoss)
        {
            flags |= TerrainSurfaceDetailFlags.HangingFringe;
        }

        if ((hash & 7u) <= 1u && tileId is KnownTileIds.Grass or KnownTileIds.Stone or KnownTileIds.Amberstone or KnownTileIds.Snow or KnownTileIds.Ice)
        {
            flags |= TerrainSurfaceDetailFlags.AccentCluster;
        }

        if ((hash & 3u) == 0u && tileId is KnownTileIds.Dirt or KnownTileIds.Stone or KnownTileIds.Amberstone or KnownTileIds.Ice)
        {
            flags |= TerrainSurfaceDetailFlags.InteriorFacet;
        }

        return new TerrainSurfaceDetailPlan(flags, (byte)((hash >> 8) & 3u));
    }

    public static int Draw(
        RenderContext context,
        Rectangle destination,
        ushort tileId,
        AutoTileMask mask,
        int tileX,
        int tileY)
    {
        if (destination.Width < 8 || destination.Height < 8)
        {
            return 0;
        }

        var plan = Plan(tileId, mask, tileX, tileY);
        if (plan.Flags == TerrainSurfaceDetailFlags.None)
        {
            return 0;
        }

        var commands = 0;
        if (plan.Has(TerrainSurfaceDetailFlags.TopFringe))
        {
            commands += DrawTopFringe(context, destination, tileId, plan.Variant);
        }

        if (plan.Has(TerrainSurfaceDetailFlags.SideRoot))
        {
            commands += DrawSideRoot(context, destination, mask, tileId, plan.Variant);
        }

        if (plan.Has(TerrainSurfaceDetailFlags.HangingFringe))
        {
            commands += DrawHangingFringe(context, destination, plan.Variant);
        }

        if (plan.Has(TerrainSurfaceDetailFlags.AccentCluster))
        {
            commands += DrawAccent(context, destination, tileId, plan.Variant);
        }

        if (plan.Has(TerrainSurfaceDetailFlags.InteriorFacet))
        {
            commands += DrawInteriorFacet(context, destination, tileId, plan.Variant);
        }

        return commands;
    }

    private static int DrawTopFringe(RenderContext context, Rectangle tile, ushort tileId, byte variant)
    {
        if (tileId == KnownTileIds.Grass)
        {
            var dark = new Color(39, 91, 50, 220);
            var light = new Color(151, 201, 89, 238);
            var x = variant switch { 0 => 2, 1 => 6, 2 => 10, _ => 13 };
            DrawLogical(context, tile, x, -3 - (variant & 1), 1, 5 + (variant & 1), dark);
            DrawLogical(context, tile, Math.Max(0, x - 1), -2, 3, 1, light);
            DrawLogical(context, tile, (x + 6) & 15, -2, 1, 3, light);
            return 3;
        }

        if (tileId == KnownTileIds.MarshMoss)
        {
            var shadow = new Color(29, 77, 57, 224);
            var light = new Color(104, 169, 91, 236);
            DrawLogical(context, tile, 1 + variant * 3, -2, 5, 3, shadow);
            DrawLogical(context, tile, 2 + variant * 3, -2, 3, 1, light);
            return 2;
        }

        if (tileId == KnownTileIds.Snow)
        {
            DrawLogical(context, tile, 1 + variant, -1, 7, 2, new Color(242, 248, 250, 246));
            DrawLogical(context, tile, 9 + (variant & 1), 0, 5, 1, new Color(180, 211, 225, 232));
            return 2;
        }

        var edge = tileId == KnownTileIds.Amberstone
            ? new Color(226, 166, 63, 220)
            : new Color(151, 147, 158, 202);
        DrawLogical(context, tile, 2 + variant * 3, 0, 5, 1, edge);
        DrawLogical(context, tile, 4 + variant * 2, -1, 2, 1, edge);
        return 2;
    }

    private static int DrawSideRoot(
        RenderContext context,
        Rectangle tile,
        AutoTileMask mask,
        ushort tileId,
        byte variant)
    {
        var color = tileId == KnownTileIds.MarshMoss
            ? new Color(45, 109, 73, 220)
            : new Color(101, 69, 39, 218);
        var drawLeft = (mask & AutoTileMask.Left) == 0 && ((variant & 1) == 0 || (mask & AutoTileMask.Right) != 0);
        var x = drawLeft ? -1 : 14;
        var bend = drawLeft ? 1 : -1;
        DrawLogical(context, tile, x, 5 + variant, 2, 5, color);
        DrawLogical(context, tile, x + bend, 9 + variant, 2, 4, color);
        return 2;
    }

    private static int DrawHangingFringe(RenderContext context, Rectangle tile, byte variant)
    {
        var dark = new Color(30, 77, 59, 224);
        var light = new Color(74, 143, 85, 228);
        var x = 2 + variant * 3;
        DrawLogical(context, tile, x, 14, 2, 5 + variant, dark);
        DrawLogical(context, tile, x + 1, 15, 1, 3 + variant, light);
        DrawLogical(context, tile, 12 - variant, 15, 1, 3, dark);
        return 3;
    }

    private static int DrawAccent(RenderContext context, Rectangle tile, ushort tileId, byte variant)
    {
        if (tileId == KnownTileIds.Grass)
        {
            var stemX = 3 + variant * 3;
            DrawLogical(context, tile, stemX, -5, 1, 6, new Color(75, 139, 63, 232));
            var blossom = variant % 2 == 0 ? new Color(244, 184, 67, 244) : new Color(181, 106, 202, 240);
            DrawLogical(context, tile, stemX - 1, -6, 3, 2, blossom);
            return 2;
        }

        if (tileId == KnownTileIds.Amberstone)
        {
            var x = 4 + variant * 2;
            DrawLogical(context, tile, x, 3, 2, 5, new Color(241, 180, 54, 230));
            DrawLogical(context, tile, x + 1, 2, 1, 2, new Color(255, 239, 145, 245));
            return 2;
        }

        if (tileId == KnownTileIds.Ice || tileId == KnownTileIds.Snow)
        {
            var x = 3 + variant * 3;
            DrawLogical(context, tile, x, 2, 4, 1, new Color(226, 246, 252, 224));
            DrawLogical(context, tile, x + 1, 1, 1, 3, new Color(171, 221, 239, 216));
            return 2;
        }

        DrawLogical(context, tile, 3 + variant * 3, 2, 4, 2, new Color(186, 173, 152, 170));
        return 1;
    }

    private static int DrawInteriorFacet(RenderContext context, Rectangle tile, ushort tileId, byte variant)
    {
        var color = tileId switch
        {
            KnownTileIds.Dirt => new Color(198, 137, 73, 120),
            KnownTileIds.Amberstone => new Color(247, 190, 75, 142),
            KnownTileIds.Ice => new Color(214, 242, 249, 150),
            _ => new Color(206, 198, 194, 108)
        };
        var x = 2 + variant * 3;
        var y = 6 + (variant & 1) * 3;
        DrawLogical(context, tile, x, y, 4, 1, color);
        DrawLogical(context, tile, x + 3, y + 1, 1, 3, color);
        return 2;
    }

    private static void DrawLogical(
        RenderContext context,
        Rectangle tile,
        int x,
        int y,
        int width,
        int height,
        Color color)
    {
        var left = tile.X + FloorScale(x, tile.Width);
        var top = tile.Y + FloorScale(y, tile.Height);
        var right = tile.X + CeilingScale(x + width, tile.Width);
        var bottom = tile.Y + CeilingScale(y + height, tile.Height);
        context.SpriteBatch.Draw(
            context.Pixel,
            new Rectangle(left, top, Math.Max(1, right - left), Math.Max(1, bottom - top)),
            color);
    }

    private static int FloorScale(int value, int extent) => (int)MathF.Floor(value * extent / 16f);

    private static int CeilingScale(int value, int extent) => (int)MathF.Ceiling(value * extent / 16f);

    private static bool IsSupported(ushort tileId)
    {
        return tileId is KnownTileIds.Dirt or
            KnownTileIds.Grass or
            KnownTileIds.Stone or
            KnownTileIds.Amberstone or
            KnownTileIds.MarshMoss or
            KnownTileIds.Snow or
            KnownTileIds.Ice;
    }

    private static uint StableHash(int tileX, int tileY, ushort tileId)
    {
        unchecked
        {
            var hash = (uint)tileX * 0x8DA6B343u;
            hash ^= (uint)tileY * 0xD8163841u;
            hash ^= (uint)tileId * 0xCB1AB31Fu;
            hash ^= hash >> 13;
            hash *= 0x85EBCA6Bu;
            return hash ^ (hash >> 16);
        }
    }
}
