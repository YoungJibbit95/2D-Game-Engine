using Game.Core;
using Game.Core.World;
using Microsoft.Xna.Framework;

namespace Game.Client.Rendering.Effects;

public readonly record struct LiquidPresentationCommand(
    Rectangle BodyBounds,
    Rectangle DepthBandBounds,
    Rectangle SurfaceHighlightBounds,
    Rectangle LeftShoreBounds,
    Rectangle RightShoreBounds,
    Color BodyColor,
    Color DepthColor,
    Color SurfaceColor,
    Color ShoreColor,
    bool IsExposedSurface);

public readonly record struct LiquidPresentationTelemetry(
    int CommandCount,
    int TilesSampled,
    int SurfaceRuns,
    int ShoreTransitions,
    bool WasBudgetClamped);

/// <summary>
/// Converts authoritative liquid tiles into bounded screen-space draw
/// commands during LateUpdate. Draw consumes only these immutable commands.
/// </summary>
public static class LiquidPresentationPlanner
{
    public const int MaximumTilesSampled = 65_536;

    public static LiquidPresentationTelemetry Build(
        World world,
        Camera2D camera,
        Rectangle viewport,
        in WaterPresentationPalette palette,
        float opacity,
        long tickNumber,
        Span<LiquidPresentationCommand> destination)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(camera);
        if (destination.IsEmpty || viewport.IsEmpty || camera.VisibleWorldRect.IsEmpty)
        {
            return default;
        }

        opacity = float.IsFinite(opacity) ? Math.Clamp(opacity, 0f, 1f) : 0f;
        if (opacity <= 0.001f)
        {
            return default;
        }

        ResolveVisibleTileBounds(world, camera, out var minTileX, out var maxTileX, out var minTileY, out var maxTileY);
        if (minTileX > maxTileX || minTileY > maxTileY)
        {
            return default;
        }

        var count = 0;
        var sampled = 0;
        var surfaces = 0;
        var shores = 0;
        for (var tileY = minTileY; tileY <= maxTileY; tileY++)
        {
            var tileX = minTileX;
            while (tileX <= maxTileX)
            {
                if (sampled == MaximumTilesSampled)
                {
                    return new LiquidPresentationTelemetry(count, sampled, surfaces, shores, true);
                }

                var tile = world.GetTile(tileX, tileY);
                sampled++;
                if (!HasLiquid(tile))
                {
                    tileX++;
                    continue;
                }

                var runStart = tileX;
                var amount = tile.LiquidAmount;
                var exposed = !HasLiquidAbove(world, tileX, tileY);
                var depthTier = ResolveDepthTier(world, tileX, tileY);
                tileX++;
                while (tileX <= maxTileX && sampled < MaximumTilesSampled)
                {
                    var next = world.GetTile(tileX, tileY);
                    sampled++;
                    if (!HasLiquid(next) ||
                        next.LiquidAmount != amount ||
                        !HasSameSurfaceAndDepth(world, tileX, tileY, exposed, depthTier))
                    {
                        break;
                    }

                    tileX++;
                }

                var runEndExclusive = tileX;
                if (!TryCreateCommand(
                        world,
                        camera,
                        viewport,
                        runStart,
                        runEndExclusive,
                        tileY,
                        amount,
                        exposed,
                        depthTier,
                        palette,
                        opacity,
                        tickNumber,
                        out var command,
                        out var shoreCount))
                {
                    continue;
                }

                destination[count++] = command;
                surfaces += exposed ? 1 : 0;
                shores += shoreCount;
                if (count == destination.Length)
                {
                    return new LiquidPresentationTelemetry(count, sampled, surfaces, shores, true);
                }
            }
        }

        return new LiquidPresentationTelemetry(
            count,
            sampled,
            surfaces,
            shores,
            sampled >= MaximumTilesSampled);
    }

    private static bool HasSameSurfaceAndDepth(
        World world,
        int tileX,
        int tileY,
        bool exposed,
        int depthTier)
    {
        return !HasLiquidAbove(world, tileX, tileY) == exposed &&
            ResolveDepthTier(world, tileX, tileY) == depthTier;
    }

    private static bool TryCreateCommand(
        World world,
        Camera2D camera,
        Rectangle viewport,
        int runStart,
        int runEndExclusive,
        int tileY,
        byte amount,
        bool exposed,
        int depthTier,
        in WaterPresentationPalette palette,
        float opacity,
        long tickNumber,
        out LiquidPresentationCommand command,
        out int shoreCount)
    {
        var left = WorldToScreenX((long)runStart * GameConstants.TileSize, camera, viewport);
        var right = WorldToScreenX((long)runEndExclusive * GameConstants.TileSize, camera, viewport);
        var top = WorldToScreenY((long)tileY * GameConstants.TileSize, camera, viewport);
        var bottom = WorldToScreenY((long)(tileY + 1) * GameConstants.TileSize, camera, viewport);
        var cellBounds = Clip(
            new Rectangle(
                Math.Min(left, right),
                Math.Min(top, bottom),
                Math.Max(1, Math.Abs(right - left)),
                Math.Max(1, Math.Abs(bottom - top))),
            viewport);
        if (cellBounds.IsEmpty)
        {
            command = default;
            shoreCount = 0;
            return false;
        }

        var fillRatio = Math.Clamp(amount / 255f, 0.08f, 1f);
        var liquidHeight = Math.Max(1, (int)MathF.Ceiling(cellBounds.Height * fillRatio));
        var body = new Rectangle(cellBounds.X, cellBounds.Bottom - liquidHeight, cellBounds.Width, liquidHeight);
        var depthProgress = depthTier / 4f;
        var bodyColor = WithOpacity(Color.Lerp(palette.ShallowColor, palette.DeepColor, depthProgress * 0.66f), opacity);
        var depthColor = WithOpacity(Color.Lerp(palette.ShallowColor, palette.DeepColor, 0.62f + depthProgress * 0.38f), opacity * 0.74f);
        var depthHeight = Math.Max(1, body.Height / 2);
        var depthBounds = new Rectangle(body.X, body.Bottom - depthHeight, body.Width, depthHeight);

        var phase = unchecked((uint)runStart * 0x9E3779B9u ^ (uint)tileY * 0x85EBCA6Bu ^ (uint)(tickNumber / 8));
        var shimmerHeight = Math.Max(1, (int)MathF.Round(camera.Zoom));
        var shimmerInset = Math.Max(1, Math.Min(body.Width / 5, GameConstants.TileSize));
        var shimmerOffset = (phase & 1u) == 0 ? 0 : shimmerInset;
        var shimmerWidth = Math.Max(1, body.Width - shimmerInset - shimmerOffset);
        var surfaceBounds = exposed
            ? Clip(
                new Rectangle(
                    body.X + shimmerOffset,
                    body.Y + (int)((phase >> 1) & 1u),
                    shimmerWidth,
                    Math.Min(shimmerHeight, body.Height)),
                viewport)
            : Rectangle.Empty;

        var leftShore = !HasLiquidAt(world, runStart - 1, tileY);
        var rightShore = !HasLiquidAt(world, runEndExclusive, tileY);
        var foamWidth = Math.Max(1, (int)MathF.Round(camera.Zoom));
        var foamHeight = Math.Min(body.Height, Math.Max(shimmerHeight + 1, body.Height / 3));
        var leftShoreBounds = leftShore
            ? Clip(new Rectangle(body.X, body.Y, Math.Min(foamWidth, body.Width), foamHeight), viewport)
            : Rectangle.Empty;
        var rightShoreBounds = rightShore
            ? Clip(
                new Rectangle(
                    Math.Max(body.X, body.Right - foamWidth),
                    body.Y,
                    Math.Min(foamWidth, body.Width),
                    foamHeight),
                viewport)
            : Rectangle.Empty;

        command = new LiquidPresentationCommand(
            body,
            depthBounds,
            surfaceBounds,
            leftShoreBounds,
            rightShoreBounds,
            bodyColor,
            depthColor,
            WithOpacity(palette.SurfaceHighlightColor, opacity * 0.68f),
            WithOpacity(palette.ShoreFoamColor, opacity * 0.58f),
            exposed);
        shoreCount = (leftShore ? 1 : 0) + (rightShore ? 1 : 0);
        return true;
    }

    private static int ResolveDepthTier(World world, int tileX, int tileY)
    {
        var depth = 0;
        for (var offset = 1; offset <= 4 && tileY - offset >= 0; offset++)
        {
            if (!HasLiquid(world.GetTile(tileX, tileY - offset)))
            {
                break;
            }

            depth++;
        }

        return depth;
    }

    private static bool HasLiquidAbove(World world, int tileX, int tileY)
    {
        return tileY > 0 && HasLiquid(world.GetTile(tileX, tileY - 1));
    }

    private static bool HasLiquidAt(World world, int tileX, int tileY)
    {
        return world.TryGetTile(tileX, tileY, out var tile) && HasLiquid(tile);
    }

    private static bool HasLiquid(in TileInstance tile)
    {
        return tile.HasLiquid && tile.LiquidAmount > 0;
    }

    private static void ResolveVisibleTileBounds(
        World world,
        Camera2D camera,
        out int minTileX,
        out int maxTileX,
        out int minTileY,
        out int maxTileY)
    {
        var visible = camera.VisibleWorldRect;
        minTileX = WorldPixelToTile(visible.X);
        maxTileX = WorldPixelToTile((long)visible.X + visible.Width - 1L);
        minTileY = Math.Max(0, WorldPixelToTile(visible.Y));
        maxTileY = Math.Min(world.HeightTiles - 1, WorldPixelToTile((long)visible.Y + visible.Height - 1L));
        if (!world.IsHorizontallyInfinite)
        {
            minTileX = Math.Max(0, minTileX);
            maxTileX = Math.Min(world.WidthTiles - 1, maxTileX);
        }
    }

    private static int WorldToScreenX(long worldX, Camera2D camera, Rectangle viewport)
    {
        return SaturatingRound((worldX - (double)camera.Position.X) * camera.Zoom + viewport.Width * 0.5d + viewport.X);
    }

    private static int WorldToScreenY(long worldY, Camera2D camera, Rectangle viewport)
    {
        return SaturatingRound((worldY - (double)camera.Position.Y) * camera.Zoom + viewport.Height * 0.5d + viewport.Y);
    }

    private static int WorldPixelToTile(long worldPixel)
    {
        var tile = Math.Floor(worldPixel / (double)GameConstants.TileSize);
        return tile <= int.MinValue ? int.MinValue : tile >= int.MaxValue ? int.MaxValue : (int)tile;
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

    private static Rectangle Clip(Rectangle rectangle, Rectangle viewport)
    {
        return Rectangle.Intersect(rectangle, viewport);
    }

    private static Color WithOpacity(Color color, float opacity)
    {
        return new Color(color, Math.Clamp(opacity, 0f, 1f));
    }
}
