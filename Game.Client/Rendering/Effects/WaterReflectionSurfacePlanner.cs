using Game.Core;
using Game.Core.World;
using Microsoft.Xna.Framework;

namespace Game.Client.Rendering.Effects;

public enum ReflectionSurfaceKind
{
    Water,
    WetSolid
}

public readonly record struct WaterReflectionSurface(
    Rectangle ScreenBounds,
    ReflectionSurfaceKind Kind,
    Color Tint,
    float Reflectivity,
    uint Phase);

public readonly record struct WaterReflectionPlanTelemetry(
    int SurfaceCount,
    int TilesSampled,
    bool WasBudgetClamped);

public static class WaterReflectionSurfacePlanner
{
    public static WaterReflectionPlanTelemetry Build(
        World world,
        Camera2D camera,
        Rectangle viewport,
        in PresentationQualityProfile profile,
        Span<WaterReflectionSurface> destination)
    {
        var palette = WaterPresentationPaletteCatalog.ClearWater;
        return Build(world, camera, viewport, profile, palette, destination);
    }

    public static WaterReflectionPlanTelemetry Build(
        World world,
        Camera2D camera,
        Rectangle viewport,
        in PresentationQualityProfile profile,
        in WaterPresentationPalette palette,
        Span<WaterReflectionSurface> destination)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(camera);
        var maximum = Math.Min(destination.Length, profile.Budget.MaxReflectionSurfaces);
        if (maximum == 0 || camera.VisibleWorldRect.IsEmpty || viewport.IsEmpty)
        {
            return default;
        }

        var visible = camera.VisibleWorldRect;
        var minTileX = WorldPixelToTile(visible.X);
        var maxTileX = WorldPixelToTile((long)visible.X + visible.Width - 1L);
        var minTileY = Math.Max(0, WorldPixelToTile(visible.Y));
        var maxTileY = Math.Min(
            world.HeightTiles - 1,
            WorldPixelToTile((long)visible.Y + visible.Height - 1L));
        if (!world.IsHorizontallyInfinite)
        {
            minTileX = Math.Max(0, minTileX);
            maxTileX = Math.Min(world.WidthTiles - 1, maxTileX);
        }

        if (minTileX > maxTileX || minTileY > maxTileY)
        {
            return default;
        }

        var tileWidth = (long)maxTileX - minTileX + 1L;
        var tileHeight = (long)maxTileY - minTileY + 1L;
        var maxColumns = Math.Max(1, profile.MaskSize.X * 2);
        var maxRows = Math.Max(1, profile.MaskSize.Y * 2);
        var stepX = Math.Max(1L, DivideRoundUp(tileWidth, maxColumns));
        var stepY = Math.Max(1L, DivideRoundUp(tileHeight, maxRows));
        var count = 0;
        var sampled = 0;
        var clamped = stepX > 1 || stepY > 1;

        for (long tileY = minTileY; tileY <= maxTileY; tileY += stepY)
        {
            long runStart = 0;
            var runActive = false;
            var runKind = ReflectionSurfaceKind.Water;
            for (long tileX = minTileX; tileX <= (long)maxTileX + stepX; tileX += stepX)
            {
                var sampleX = tileX > maxTileX ? maxTileX : (int)tileX;
                var isSentinel = tileX > maxTileX;
                var kind = ReflectionSurfaceKind.Water;
                var reflective = !isSentinel && IsReflectiveSurface(world, sampleX, (int)tileY, out kind);
                sampled++;

                if (reflective && (!runActive || kind == runKind))
                {
                    if (!runActive)
                    {
                        runActive = true;
                        runStart = tileX;
                        runKind = kind;
                    }

                    continue;
                }

                if (runActive)
                {
                    var runEndExclusive = Math.Min((long)maxTileX + 1L, tileX);
                    if (TryCreateSurface(
                            camera,
                            viewport,
                            runStart,
                            tileY,
                            runEndExclusive,
                            Math.Min((long)world.HeightTiles, tileY + stepY),
                            runKind,
                            palette,
                            out var surface))
                    {
                        destination[count++] = surface;
                        if (count == maximum)
                        {
                            return new WaterReflectionPlanTelemetry(count, sampled, true);
                        }
                    }

                    runActive = false;
                }

                if (reflective)
                {
                    runActive = true;
                    runStart = tileX;
                    runKind = kind;
                }
            }
        }

        return new WaterReflectionPlanTelemetry(count, sampled, clamped);
    }

    private static bool IsReflectiveSurface(
        World world,
        int tileX,
        int tileY,
        out ReflectionSurfaceKind kind)
    {
        var tile = world.GetTile(tileX, tileY);
        if (HasLiquid(tile) && (tileY == 0 || !HasLiquid(world.GetTile(tileX, tileY - 1))))
        {
            kind = ReflectionSurfaceKind.Water;
            return true;
        }

        if ((tile.Flags & TileFlags.Solid) != 0 &&
            tileY > 0 &&
            HasLiquid(world.GetTile(tileX, tileY - 1)))
        {
            kind = ReflectionSurfaceKind.WetSolid;
            return true;
        }

        kind = default;
        return false;
    }

    private static bool HasLiquid(in TileInstance tile)
    {
        return (tile.Flags & TileFlags.HasLiquid) != 0 && tile.LiquidAmount > 0;
    }

    private static bool TryCreateSurface(
        Camera2D camera,
        Rectangle viewport,
        long leftTile,
        long topTile,
        long rightTileExclusive,
        long bottomTileExclusive,
        ReflectionSurfaceKind kind,
        in WaterPresentationPalette palette,
        out WaterReflectionSurface surface)
    {
        var left = WorldToScreenX(leftTile * GameConstants.TileSize, camera, viewport);
        var right = WorldToScreenX(rightTileExclusive * GameConstants.TileSize, camera, viewport);
        var top = WorldToScreenY(topTile * GameConstants.TileSize, camera, viewport);
        var bottom = WorldToScreenY(bottomTileExclusive * GameConstants.TileSize, camera, viewport);
        var viewportRight = (long)viewport.X + viewport.Width;
        var viewportBottom = (long)viewport.Y + viewport.Height;
        var clippedLeft = Math.Max((long)Math.Min(left, right), viewport.X);
        var clippedTop = Math.Max((long)Math.Min(top, bottom), viewport.Y);
        var clippedRight = Math.Min((long)Math.Max(left, right), viewportRight);
        var clippedBottom = Math.Min((long)Math.Max(top, bottom), viewportBottom);
        if (clippedRight <= clippedLeft || clippedBottom <= clippedTop)
        {
            surface = default;
            return false;
        }

        var bounds = new Rectangle(
            (int)clippedLeft,
            (int)clippedTop,
            (int)(clippedRight - clippedLeft),
            (int)(clippedBottom - clippedTop));

        var phase = unchecked((uint)leftTile * 0x9E3779B9u ^ (uint)topTile * 0x85EBCA6Bu);
        surface = kind == ReflectionSurfaceKind.Water
            ? new WaterReflectionSurface(
                bounds,
                kind,
                palette.ReflectionTint,
                palette.WaterReflectivity,
                phase)
            : new WaterReflectionSurface(
                bounds,
                kind,
                palette.WetSurfaceTint,
                palette.WetSurfaceReflectivity,
                phase);
        return true;
    }

    private static int WorldToScreenX(long worldX, Camera2D camera, Rectangle viewport)
    {
        var value = (worldX - (double)camera.Position.X) * camera.Zoom + viewport.Width * 0.5d + viewport.X;
        return SaturatingRound(value);
    }

    private static int WorldToScreenY(long worldY, Camera2D camera, Rectangle viewport)
    {
        var value = (worldY - (double)camera.Position.Y) * camera.Zoom + viewport.Height * 0.5d + viewport.Y;
        return SaturatingRound(value);
    }

    private static int WorldPixelToTile(long worldPixel)
    {
        var tile = Math.Floor(worldPixel / (double)GameConstants.TileSize);
        return tile <= int.MinValue
            ? int.MinValue
            : tile >= int.MaxValue
                ? int.MaxValue
                : (int)tile;
    }

    private static int SaturatingRound(double value)
    {
        if (double.IsNaN(value))
        {
            return 0;
        }

        return value <= int.MinValue
            ? int.MinValue
            : value >= int.MaxValue
                ? int.MaxValue
                : (int)Math.Round(value, MidpointRounding.AwayFromZero);
    }

    private static long DivideRoundUp(long value, long divisor)
    {
        return value / divisor + (value % divisor == 0 ? 0 : 1);
    }
}
