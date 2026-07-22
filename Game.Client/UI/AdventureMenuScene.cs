using Game.Client.Rendering;
using Game.Core.Settings;
using Microsoft.Xna.Framework;

namespace Game.Client.UI;

internal readonly record struct AdventureMenuSceneLayout(
    Rectangle Sky,
    Rectangle Valley,
    Rectangle Ground,
    Rectangle Soil,
    Rectangle SafeContent,
    Rectangle LeftSettlement,
    Rectangle RightSettlement,
    Rectangle FloatingIslandA,
    Rectangle FloatingIslandB,
    Rectangle FloatingIslandC,
    bool ShowFloatingIslands,
    bool ShowEdgeSettlements);

/// <summary>
/// Draws the main menu's deterministic living-world vista without loading or creating resources in Draw.
/// </summary>
public static class AdventureMenuScene
{
    private const int SkyBands = 18;

    internal static AdventureMenuSceneLayout Plan(Rectangle viewport)
    {
        var horizonY = viewport.Y + viewport.Height * 61 / 100;
        var groundHeight = Math.Clamp(viewport.Height * 13 / 100, 40, 112);
        var groundY = Math.Max(horizonY + 1, viewport.Bottom - groundHeight);
        var soilY = Math.Min(viewport.Bottom, groundY + Math.Clamp(groundHeight / 8, 5, 12));
        var safeInset = Math.Clamp(viewport.Width / 28, 18, 72);
        var safeTop = Math.Clamp(viewport.Height / 24, 14, 48);
        var showFloatingIslands = viewport.Width >= 900 && viewport.Height >= 500;
        var showEdgeSettlements = viewport.Width >= 1120 && viewport.Height >= 620;

        var islandWidthA = Math.Clamp(viewport.Width * 10 / 100, 92, 190);
        var islandWidthB = Math.Clamp(viewport.Width * 9 / 100, 84, 176);
        var islandWidthC = Math.Clamp(viewport.Width * 12 / 100, 104, 216);
        var islandAY = viewport.Y + viewport.Height * 28 / 100;
        var islandBY = viewport.Y + viewport.Height * 18 / 100;
        var islandCY = viewport.Y + viewport.Height * 32 / 100;

        return new AdventureMenuSceneLayout(
            new Rectangle(viewport.X, viewport.Y, viewport.Width, Math.Max(1, horizonY - viewport.Y)),
            new Rectangle(viewport.X, horizonY, viewport.Width, Math.Max(1, groundY - horizonY)),
            new Rectangle(viewport.X, groundY, viewport.Width, Math.Max(1, soilY - groundY)),
            new Rectangle(viewport.X, soilY, viewport.Width, Math.Max(1, viewport.Bottom - soilY)),
            new Rectangle(
                viewport.X + safeInset,
                viewport.Y + safeTop,
                Math.Max(1, viewport.Width - safeInset * 2),
                Math.Max(1, viewport.Height - safeTop * 2)),
            showEdgeSettlements
                ? new Rectangle(
                    viewport.X,
                    viewport.Y + viewport.Height * 17 / 100,
                    Math.Clamp(viewport.Width * 23 / 100, 180, 390),
                    Math.Max(1, groundY - (viewport.Y + viewport.Height * 17 / 100)))
                : Rectangle.Empty,
            showEdgeSettlements
                ? new Rectangle(
                    viewport.Right - Math.Clamp(viewport.Width * 21 / 100, 170, 360),
                    viewport.Y + viewport.Height * 25 / 100,
                    Math.Clamp(viewport.Width * 21 / 100, 170, 360),
                    Math.Max(1, groundY - (viewport.Y + viewport.Height * 25 / 100)))
                : Rectangle.Empty,
            showFloatingIslands
                ? CreateIslandBounds(viewport, groundY, 45, islandAY, islandWidthA)
                : Rectangle.Empty,
            showFloatingIslands
                ? CreateIslandBounds(viewport, groundY, 62, islandBY, islandWidthB)
                : Rectangle.Empty,
            showFloatingIslands
                ? CreateIslandBounds(viewport, groundY, 79, islandCY, islandWidthC)
                : Rectangle.Empty,
            showFloatingIslands,
            showEdgeSettlements);
    }

    public static void Draw(RenderContext context, UiPalette palette, GameSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var layout = Plan(context.ViewportBounds);
        var time = settings.Ui.ReducedMotion
            ? 0d
            : context.Time.TotalSeconds * Math.Clamp(settings.Ui.AnimationSpeed, 0.1f, 4f);

        DrawSky(context, layout, palette, time);
        DrawClouds(context, layout, time);
        DrawMountains(context, layout);
        if (layout.ShowFloatingIslands)
        {
            DrawFloatingIsland(context, layout.FloatingIslandA, layout.Ground.Y, palette, time, 0);
            DrawFloatingIsland(context, layout.FloatingIslandB, layout.Ground.Y, palette, time, 1);
            DrawFloatingIsland(context, layout.FloatingIslandC, layout.Ground.Y, palette, time, 2);
        }

        DrawValleyRuins(context, layout, palette, time);
        DrawDistantForest(context, layout, time);
        if (layout.ShowEdgeSettlements)
        {
            DrawTreeSettlement(context, layout.LeftSettlement, palette, time, faceRight: true);
            DrawTreeSettlement(context, layout.RightSettlement, palette, time, faceRight: false);
        }

        DrawGround(context, layout, palette);
        DrawForegroundPlants(context, layout, palette, time);
        DrawLivingDetails(context, layout, palette, time);
        DrawEdgeShade(context, context.ViewportBounds, palette);
    }

    private static Rectangle CreateIslandBounds(
        Rectangle viewport,
        int groundY,
        int centerPercent,
        int top,
        int width)
    {
        var centerX = viewport.X + viewport.Width * centerPercent / 100;
        var x = Math.Clamp(centerX - width / 2, viewport.X, viewport.Right - width);
        return new Rectangle(x, top, width, Math.Max(1, groundY - top));
    }

    private static void DrawSky(
        RenderContext context,
        AdventureMenuSceneLayout layout,
        UiPalette palette,
        double time)
    {
        var top = new Color(20, 25, 75);
        var middle = new Color(90, 57, 124);
        var bottom = new Color(226, 119, 116);
        var bandHeight = Math.Max(1, (layout.Sky.Height + SkyBands - 1) / SkyBands);
        for (var band = 0; band < SkyBands; band++)
        {
            var y = layout.Sky.Y + band * bandHeight;
            var height = Math.Min(bandHeight + 1, layout.Sky.Bottom - y);
            if (height <= 0)
            {
                break;
            }

            var amount = band / (float)Math.Max(1, SkyBands - 1);
            var color = amount < 0.55f
                ? Color.Lerp(top, middle, amount / 0.55f)
                : Color.Lerp(middle, bottom, (amount - 0.55f) / 0.45f);
            context.SpriteBatch.Draw(
                context.Pixel,
                new Rectangle(layout.Sky.X, y, layout.Sky.Width, height),
                color);
        }

        var sun = new Point(
            layout.Sky.X + layout.Sky.Width * 88 / 100,
            layout.Sky.Y + layout.Sky.Height * 55 / 100);
        var sunRadius = Math.Clamp(layout.Sky.Height / 17, 18, 42);
        var sunColor = new Color(255, 207, 133);
        DrawPixelDisc(context, sun, sunRadius + 18, UiTheme.WithAlpha(sunColor, 0.045f));
        DrawPixelDisc(context, sun, sunRadius + 8, UiTheme.WithAlpha(sunColor, 0.10f));
        DrawPixelDisc(context, sun, sunRadius, sunColor);

        for (var index = 0; index < 47; index++)
        {
            var x = layout.Sky.X + 12 + PositiveModulo(index * 193 + 37, Math.Max(1, layout.Sky.Width - 24));
            var y = layout.Sky.Y + 10 + PositiveModulo(index * 83 + 11, Math.Max(1, layout.Sky.Height * 58 / 100));
            var pulse = 0.38f + 0.48f * (0.5f + 0.5f * MathF.Sin((float)time * 1.35f + index * 1.73f));
            var starColor = UiTheme.WithAlpha(index % 7 == 0 ? palette.Warning : palette.Text, pulse);
            var size = index % 11 == 0 ? 2 : 1;
            context.SpriteBatch.Draw(context.Pixel, new Rectangle(x, y, size, size), starColor);
            if (index % 13 == 0)
            {
                context.SpriteBatch.Draw(context.Pixel, new Rectangle(x - 2, y, size + 4, 1), UiTheme.WithAlpha(starColor, 0.42f));
                context.SpriteBatch.Draw(context.Pixel, new Rectangle(x, y - 2, 1, size + 4), UiTheme.WithAlpha(starColor, 0.42f));
            }
        }

        var cometTravel = Math.Max(1, layout.Sky.Width + 160);
        var cometX = layout.Sky.X - 80 + PositiveModulo((int)Math.Round(time * 18d), cometTravel);
        var cometY = layout.Sky.Y + Math.Max(34, layout.Sky.Height / 7);
        for (var segment = 0; segment < 8; segment++)
        {
            context.SpriteBatch.Draw(
                context.Pixel,
                new Rectangle(cometX - segment * 6, cometY + segment * 2, Math.Max(1, 7 - segment), 1),
                UiTheme.WithAlpha(palette.Text, 0.72f - segment * 0.075f));
        }
    }

    private static void DrawClouds(RenderContext context, AdventureMenuSceneLayout layout, double time)
    {
        var wrapWidth = Math.Max(1, layout.Sky.Width + 360);
        for (var index = 0; index < 7; index++)
        {
            var x = layout.Sky.X - 210 + PositiveModulo(
                (int)Math.Round(index * 307d + time * (3.2d + index * 0.55d)),
                wrapWidth);
            var y = layout.Sky.Y + 52 + PositiveModulo(index * 71, Math.Max(54, layout.Sky.Height * 58 / 100));
            var scale = index % 3 == 0 ? 2 : 1;
            var color = index % 2 == 0
                ? new Color(220, 134, 160)
                : new Color(174, 116, 157);
            DrawCloud(context, new Point(x, y), scale, UiTheme.WithAlpha(color, index < 3 ? 0.34f : 0.22f));
        }
    }

    private static void DrawMountains(RenderContext context, AdventureMenuSceneLayout layout)
    {
        var baseline = layout.Valley.Y + layout.Valley.Height * 58 / 100;
        var back = new Color(62, 69, 126);
        var middle = new Color(48, 71, 111);
        var front = new Color(39, 69, 91);

        DrawMountain(context, baseline, layout.Sky.X + layout.Sky.Width * 7 / 100, layout.Sky.Width * 9 / 100, layout.Sky.Height * 26 / 100, 8, back);
        DrawMountain(context, baseline, layout.Sky.X + layout.Sky.Width * 23 / 100, layout.Sky.Width * 12 / 100, layout.Sky.Height * 38 / 100, 8, back);
        DrawMountain(context, baseline, layout.Sky.X + layout.Sky.Width * 43 / 100, layout.Sky.Width * 11 / 100, layout.Sky.Height * 31 / 100, 8, back);
        DrawMountain(context, baseline, layout.Sky.X + layout.Sky.Width * 58 / 100, layout.Sky.Width * 13 / 100, layout.Sky.Height * 43 / 100, 8, back);
        DrawMountain(context, baseline, layout.Sky.X + layout.Sky.Width * 78 / 100, layout.Sky.Width * 12 / 100, layout.Sky.Height * 32 / 100, 8, back);
        DrawMountain(context, baseline, layout.Sky.X + layout.Sky.Width * 94 / 100, layout.Sky.Width * 11 / 100, layout.Sky.Height * 40 / 100, 8, back);

        var middleBaseline = layout.Valley.Y + layout.Valley.Height * 82 / 100;
        DrawMountain(context, middleBaseline, layout.Sky.X + layout.Sky.Width * 14 / 100, layout.Sky.Width * 17 / 100, layout.Sky.Height * 22 / 100, 7, middle);
        DrawMountain(context, middleBaseline, layout.Sky.X + layout.Sky.Width * 48 / 100, layout.Sky.Width * 20 / 100, layout.Sky.Height * 24 / 100, 7, middle);
        DrawMountain(context, middleBaseline, layout.Sky.X + layout.Sky.Width * 83 / 100, layout.Sky.Width * 19 / 100, layout.Sky.Height * 25 / 100, 7, middle);

        context.SpriteBatch.Draw(context.Pixel, layout.Valley, UiTheme.WithAlpha(front, 0.18f));
        var mistHeight = Math.Max(2, layout.Valley.Height / 5);
        context.SpriteBatch.Draw(
            context.Pixel,
            new Rectangle(layout.Valley.X, layout.Valley.Y, layout.Valley.Width, mistHeight),
            UiTheme.WithAlpha(new Color(118, 129, 158), 0.11f));
        context.SpriteBatch.Draw(
            context.Pixel,
            new Rectangle(layout.Valley.X, layout.Valley.Y + mistHeight, layout.Valley.Width, Math.Max(2, mistHeight / 2)),
            UiTheme.WithAlpha(new Color(93, 122, 142), 0.055f));
    }

    private static void DrawFloatingIsland(
        RenderContext context,
        Rectangle bounds,
        int groundY,
        UiPalette palette,
        double time,
        int variant)
    {
        var bob = (int)MathF.Round(MathF.Sin((float)time * (0.38f + variant * 0.07f) + variant * 2.1f) * 3f);
        var surfaceY = bounds.Y + bob;
        var centerX = bounds.Center.X;
        var halfWidth = bounds.Width / 2;
        var depth = Math.Clamp(bounds.Width * 48 / 100, 38, 92);
        var rockDark = new Color(43, 48, 71);
        var rock = new Color(65, 67, 86);
        var earth = new Color(83, 65, 61);
        var moss = Color.Lerp(new Color(77, 139, 75), palette.Accent, 0.18f);
        var mossLight = Color.Lerp(moss, palette.Warning, 0.20f);

        context.SpriteBatch.Draw(
            context.Pixel,
            new Rectangle(bounds.X, surfaceY, bounds.Width, 7),
            mossLight);
        context.SpriteBatch.Draw(
            context.Pixel,
            new Rectangle(bounds.X + 5, surfaceY + 7, Math.Max(1, bounds.Width - 10), 9),
            earth);

        for (var row = 0; row < depth; row += 3)
        {
            var amount = row / (float)Math.Max(1, depth);
            var rowHalfWidth = Math.Max(2, (int)MathF.Round(halfWidth * MathF.Pow(1f - amount, 0.72f)));
            var color = row < depth / 3 ? rock : rockDark;
            context.SpriteBatch.Draw(
                context.Pixel,
                new Rectangle(centerX - rowHalfWidth, surfaceY + 16 + row, rowHalfWidth * 2, 4),
                color);
        }

        for (var root = 0; root < 7; root++)
        {
            var rootX = bounds.X + bounds.Width * (root + 1) / 8;
            var rootLength = 12 + PositiveModulo(root * 23 + variant * 17, Math.Max(13, depth));
            context.SpriteBatch.Draw(
                context.Pixel,
                new Rectangle(rootX, surfaceY + 15 + depth / 3, root % 3 == 0 ? 3 : 2, rootLength),
                rockDark);
        }

        var waterfallX = bounds.X + bounds.Width * (variant % 2 == 0 ? 72 : 31) / 100;
        DrawWaterfall(
            context,
            new Rectangle(waterfallX, surfaceY + 3, Math.Clamp(bounds.Width / 18, 5, 10), Math.Max(1, groundY - surfaceY - 12)),
            time,
            variant);

        DrawIslandTree(context, new Point(bounds.X + bounds.Width * (variant == 1 ? 65 : 34) / 100, surfaceY), palette, time, variant);
        for (var flower = 0; flower < 5; flower++)
        {
            var x = bounds.X + 12 + PositiveModulo(flower * 31 + variant * 13, Math.Max(1, bounds.Width - 24));
            var color = flower % 2 == 0 ? palette.Warning : new Color(230, 126, 175);
            context.SpriteBatch.Draw(context.Pixel, new Rectangle(x, surfaceY - 4, 2, 4), moss);
            context.SpriteBatch.Draw(context.Pixel, new Rectangle(x - 1, surfaceY - 6, 4, 3), color);
        }
    }

    private static void DrawIslandTree(
        RenderContext context,
        Point root,
        UiPalette palette,
        double time,
        int variant)
    {
        var height = 24 + variant * 5;
        var sway = (int)MathF.Round(MathF.Sin((float)time * 0.56f + variant) * 2f);
        var bark = new Color(86, 57, 49);
        var leafDark = new Color(33, 83, 66);
        var leaf = Color.Lerp(new Color(52, 126, 77), palette.Accent, 0.14f);
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(root.X - 2, root.Y - height, 5, height), bark);
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(root.X - 17, root.Y - height + 9, 20, 4), bark);
        DrawPixelDisc(context, new Point(root.X - 12 + sway, root.Y - height - 2), 12, leafDark);
        DrawPixelDisc(context, new Point(root.X + sway, root.Y - height - 9), 15, leaf);
        DrawPixelDisc(context, new Point(root.X + 13 + sway, root.Y - height + 1), 11, leafDark);
    }

    private static void DrawValleyRuins(
        RenderContext context,
        AdventureMenuSceneLayout layout,
        UiPalette palette,
        double time)
    {
        var centerX = layout.Valley.X + layout.Valley.Width * 61 / 100;
        var baseY = layout.Ground.Y;
        var width = Math.Clamp(layout.Valley.Width * 22 / 100, 180, 430);
        var height = Math.Clamp(layout.Sky.Height * 31 / 100, 82, 210);
        var stoneDark = new Color(39, 63, 67);
        var stone = new Color(54, 82, 75);
        var moss = Color.Lerp(new Color(55, 116, 69), palette.Accent, 0.12f);

        context.SpriteBatch.Draw(context.Pixel, new Rectangle(centerX - width / 2, baseY - 18, width, 18), stoneDark);
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(centerX - width * 36 / 100, baseY - height / 2, width * 72 / 100, height / 2), stone);
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(centerX - width / 5, baseY - height, width * 2 / 5, height), stoneDark);
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(centerX - width / 2, baseY - height * 3 / 5, width, 8), moss);

        for (var column = 0; column < 6; column++)
        {
            var x = centerX - width * 42 / 100 + column * width * 17 / 100;
            var columnHeight = height * (38 + PositiveModulo(column * 13, 23)) / 100;
            context.SpriteBatch.Draw(context.Pixel, new Rectangle(x, baseY - columnHeight, 8, columnHeight), stoneDark);
            context.SpriteBatch.Draw(context.Pixel, new Rectangle(x - 3, baseY - columnHeight, 14, 5), moss);
        }

        for (var window = 0; window < 3; window++)
        {
            var x = centerX - width / 8 + window * width / 8;
            var y = baseY - height * 58 / 100 + PositiveModulo(window * 17, 14);
            DrawPixelDisc(context, new Point(x, y), 7, UiTheme.WithAlpha(palette.Warning, 0.10f));
            context.SpriteBatch.Draw(context.Pixel, new Rectangle(x - 2, y - 3, 5, 8), UiTheme.WithAlpha(palette.Warning, 0.48f));
        }

        DrawWaterfall(
            context,
            new Rectangle(centerX + width / 8, baseY - height + 12, Math.Clamp(width / 28, 7, 13), height - 20),
            time,
            4);
    }

    private static void DrawDistantForest(RenderContext context, AdventureMenuSceneLayout layout, double time)
    {
        var baseline = layout.Ground.Y;
        var back = new Color(31, 75, 76);
        var front = new Color(24, 61, 58);
        var stride = Math.Clamp(layout.Valley.Width / 34, 24, 52);
        for (var x = layout.Valley.X - stride; x < layout.Valley.Right + stride; x += stride)
        {
            var index = (x - layout.Valley.X) / Math.Max(1, stride);
            var height = 34 + PositiveModulo(index * 29, 66);
            var sway = (int)MathF.Round(MathF.Sin((float)time * 0.31f + index) * 1.5f);
            var color = index % 3 == 0 ? back : front;
            context.SpriteBatch.Draw(context.Pixel, new Rectangle(x + stride / 2 - 2, baseline - height, 5, height), color);
            DrawPixelDisc(context, new Point(x + stride / 2 + sway, baseline - height), 10 + PositiveModulo(index * 7, 11), color);
            DrawPixelDisc(context, new Point(x + stride / 2 - 8 + sway, baseline - height + 8), 8, color);
        }

        context.SpriteBatch.Draw(context.Pixel, layout.Valley, UiTheme.WithAlpha(new Color(31, 79, 74), 0.10f));
    }

    private static void DrawTreeSettlement(
        RenderContext context,
        Rectangle bounds,
        UiPalette palette,
        double time,
        bool faceRight)
    {
        var trunkX = faceRight ? bounds.X + bounds.Width * 21 / 100 : bounds.Right - bounds.Width * 25 / 100;
        var trunkWidth = Math.Clamp(bounds.Width / 12, 20, 38);
        var trunkTop = bounds.Y + bounds.Height * 13 / 100;
        var groundY = bounds.Bottom;
        var barkDark = new Color(49, 40, 42);
        var bark = new Color(83, 54, 43);
        var barkLight = new Color(128, 79, 49);
        var leafDark = new Color(21, 60, 48);
        var leaf = Color.Lerp(new Color(35, 104, 59), palette.Accent, 0.12f);
        var sway = (int)MathF.Round(MathF.Sin((float)time * 0.43f + (faceRight ? 0f : 2.4f)) * 3f);

        context.SpriteBatch.Draw(context.Pixel, new Rectangle(trunkX, trunkTop, trunkWidth, Math.Max(1, groundY - trunkTop + 4)), barkDark);
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(trunkX + trunkWidth / 4, trunkTop + 8, Math.Max(3, trunkWidth / 5), Math.Max(1, groundY - trunkTop - 10)), bark);
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(trunkX + trunkWidth / 3, trunkTop + 18, 3, Math.Max(1, groundY - trunkTop - 24)), barkLight);

        var branchY = bounds.Y + bounds.Height * 45 / 100;
        var branchX = faceRight ? trunkX + trunkWidth / 2 : bounds.X + bounds.Width * 18 / 100;
        var branchWidth = Math.Max(40, bounds.Width * 63 / 100);
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(branchX, branchY, branchWidth, 9), barkDark);
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(branchX + 6, branchY, Math.Max(1, branchWidth - 12), 3), barkLight);

        var canopyX = faceRight ? bounds.X + bounds.Width * 18 / 100 : bounds.Right - bounds.Width * 24 / 100;
        DrawPixelDisc(context, new Point(canopyX + sway, bounds.Y + 34), Math.Clamp(bounds.Width / 5, 42, 76), leafDark);
        DrawPixelDisc(context, new Point(canopyX + (faceRight ? 48 : -48) + sway, bounds.Y + 50), Math.Clamp(bounds.Width / 6, 36, 64), leaf);
        DrawPixelDisc(context, new Point(canopyX + (faceRight ? -30 : 30) + sway, bounds.Y + 70), Math.Clamp(bounds.Width / 7, 32, 58), leaf);

        for (var cluster = 0; cluster < 12; cluster++)
        {
            var spread = Math.Clamp(bounds.Width / 3, 54, 104);
            var clusterX = canopyX - spread + PositiveModulo(cluster * 37 + (faceRight ? 11 : 29), spread * 2);
            var clusterY = bounds.Y + 18 + PositiveModulo(cluster * 23, Math.Max(24, bounds.Height / 5));
            var radius = 8 + PositiveModulo(cluster * 11, 9);
            var clusterColor = cluster % 3 == 0
                ? Color.Lerp(leaf, palette.Warning, 0.13f)
                : cluster % 2 == 0 ? leaf : leafDark;
            DrawPixelDisc(context, new Point(clusterX + sway, clusterY), radius, clusterColor);
            if (cluster % 4 == 0)
            {
                context.SpriteBatch.Draw(
                    context.Pixel,
                    new Rectangle(clusterX + sway - 2, clusterY - radius / 2, 6, 3),
                    UiTheme.WithAlpha(palette.Warning, 0.20f));
            }
        }

        var platformX = faceRight ? branchX + branchWidth / 5 : branchX + branchWidth / 7;
        var platformWidth = Math.Clamp(bounds.Width * 45 / 100, 82, 158);
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(platformX, branchY - 5, platformWidth, 7), barkLight);
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(platformX + 7, branchY - 44, platformWidth - 14, 39), new Color(68, 54, 50));
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(platformX + 2, branchY - 47, platformWidth - 4, 5), new Color(112, 75, 54));
        for (var window = 0; window < 2; window++)
        {
            DrawLantern(
                context,
                new Point(platformX + 22 + window * Math.Max(28, platformWidth - 44), branchY - 25),
                palette,
                time + window * 0.7d);
        }

        for (var vine = 0; vine < 5; vine++)
        {
            var vineX = platformX + 8 + PositiveModulo(vine * 31, Math.Max(1, platformWidth - 16));
            DrawVine(context, vineX, branchY + 2, 22 + PositiveModulo(vine * 17, 52), palette, time, vine);
        }

        var lanternX = faceRight ? branchX + branchWidth - 14 : branchX + 12;
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(lanternX, branchY + 7, 2, 20), barkLight);
        DrawLantern(context, new Point(lanternX + 1, branchY + 31), palette, time + 1.3d);
    }

    private static void DrawGround(RenderContext context, AdventureMenuSceneLayout layout, UiPalette palette)
    {
        var grass = Color.Lerp(new Color(63, 130, 59), palette.Accent, 0.12f);
        var grassLight = Color.Lerp(grass, palette.Warning, 0.24f);
        var earth = new Color(73, 54, 49);
        var rock = new Color(38, 39, 47);
        context.SpriteBatch.Draw(context.Pixel, layout.Ground, grass);
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(layout.Ground.X, layout.Ground.Y, layout.Ground.Width, Math.Min(5, layout.Ground.Height)), grassLight);
        context.SpriteBatch.Draw(context.Pixel, layout.Soil, earth);
        if (layout.Soil.Height > 12)
        {
            context.SpriteBatch.Draw(
                context.Pixel,
                new Rectangle(layout.Soil.X, layout.Soil.Y + layout.Soil.Height * 58 / 100, layout.Soil.Width, layout.Soil.Height - layout.Soil.Height * 58 / 100),
                rock);
        }

        for (var x = layout.Soil.X + 11; x < layout.Soil.Right; x += 31)
        {
            var y = layout.Soil.Y + 7 + PositiveModulo(x * 7, Math.Max(1, layout.Soil.Height - 8));
            var color = x % 3 == 0 ? palette.Warning : palette.TextMuted;
            context.SpriteBatch.Draw(context.Pixel, new Rectangle(x, y, 3, 2), UiTheme.WithAlpha(color, 0.18f));
        }
    }

    private static void DrawForegroundPlants(
        RenderContext context,
        AdventureMenuSceneLayout layout,
        UiPalette palette,
        double time)
    {
        var grass = Color.Lerp(new Color(87, 159, 65), palette.Accent, 0.16f);
        for (var x = layout.Ground.X + 5; x < layout.Ground.Right; x += 17)
        {
            var phase = x * 0.057f + (float)time * 0.72f;
            var lean = (int)MathF.Round(MathF.Sin(phase) * 2.5f);
            var height = 6 + PositiveModulo(x * 13, 13);
            context.SpriteBatch.Draw(context.Pixel, new Rectangle(x, layout.Ground.Y - height, 2, height + 2), grass);
            context.SpriteBatch.Draw(context.Pixel, new Rectangle(x + lean, layout.Ground.Y - height, 5, 2), grass);
            if (x % 5 == 0)
            {
                context.SpriteBatch.Draw(context.Pixel, new Rectangle(x - 1, layout.Ground.Y - height - 3, 5, 3), x % 10 == 0 ? palette.Warning : new Color(190, 100, 190));
            }
        }

        for (var cluster = 0; cluster < 7; cluster++)
        {
            var x = layout.Ground.X + PositiveModulo(cluster * 277 + 91, Math.Max(1, layout.Ground.Width));
            var radius = 8 + PositiveModulo(cluster * 7, 9);
            DrawPixelDisc(context, new Point(x, layout.Ground.Y - radius / 2), radius, new Color(30, 89, 56));
        }
    }

    private static void DrawLivingDetails(
        RenderContext context,
        AdventureMenuSceneLayout layout,
        UiPalette palette,
        double time)
    {
        var groundY = layout.Ground.Y;
        var fireflyColor = new Color(255, 224, 112);
        for (var index = 0; index < 23; index++)
        {
            var baseX = layout.Valley.X + PositiveModulo(index * 109 + 47, Math.Max(1, layout.Valley.Width));
            var x = baseX + (int)MathF.Round(MathF.Sin((float)time * 0.72f + index * 1.9f) * 12f);
            var y = groundY - 28 - PositiveModulo(index * 47, Math.Max(30, layout.Valley.Height + 70)) +
                (int)MathF.Round(MathF.Cos((float)time * 1.04f + index) * 6f);
            var glow = 0.42f + 0.48f * (0.5f + 0.5f * MathF.Sin((float)time * 2.3f + index * 1.7f));
            context.SpriteBatch.Draw(context.Pixel, new Rectangle(x - 3, y - 3, 7, 7), UiTheme.WithAlpha(fireflyColor, glow * 0.12f));
            context.SpriteBatch.Draw(context.Pixel, new Rectangle(x, y, 2, 2), UiTheme.WithAlpha(fireflyColor, glow));
        }

        var slimeTravel = Math.Max(1, layout.Ground.Width * 30 / 100);
        var slimeX = layout.Ground.X + layout.Ground.Width * 61 / 100 + PositiveModulo((int)Math.Round(time * 11d), slimeTravel);
        var hop = (int)MathF.Round(MathF.Abs(MathF.Sin((float)time * 2.4f)) * 7f);
        DrawSlime(context, new Point(slimeX, groundY - 10 - hop), palette);

        var butterflyX = layout.Valley.X + layout.Valley.Width * 38 / 100 + (int)MathF.Round(MathF.Sin((float)time * 0.63f) * 72f);
        var butterflyY = groundY - 90 + (int)MathF.Round(MathF.Cos((float)time * 1.27f) * 22f);
        DrawButterfly(context, new Point(butterflyX, butterflyY), palette, time);

        DrawBird(context, new Point(layout.Sky.X + layout.Sky.Width * 72 / 100, layout.Sky.Y + layout.Sky.Height * 20 / 100), time, 0);
        DrawBird(context, new Point(layout.Sky.X + layout.Sky.Width * 76 / 100, layout.Sky.Y + layout.Sky.Height * 24 / 100), time, 1);
    }

    private static void DrawWaterfall(RenderContext context, Rectangle bounds, double time, int phase)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        var waterDark = new Color(64, 148, 181, 156);
        var water = new Color(116, 207, 218, 190);
        var foam = new Color(204, 240, 224, 178);
        context.SpriteBatch.Draw(context.Pixel, bounds, waterDark);
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(bounds.X + 1, bounds.Y, Math.Max(1, bounds.Width / 3), bounds.Height), water);
        var stride = 18;
        var offset = PositiveModulo((int)Math.Round(time * 12d) + phase * 7, stride);
        for (var y = bounds.Y - stride + offset; y < bounds.Bottom; y += stride)
        {
            var height = Math.Min(4, bounds.Bottom - y);
            if (height > 0 && y >= bounds.Y)
            {
                context.SpriteBatch.Draw(context.Pixel, new Rectangle(bounds.X, y, bounds.Width, height), UiTheme.WithAlpha(foam, 0.46f));
            }
        }

        DrawPixelDisc(context, new Point(bounds.Center.X, bounds.Bottom), Math.Max(5, bounds.Width), UiTheme.WithAlpha(foam, 0.18f));
    }

    private static void DrawLantern(RenderContext context, Point center, UiPalette palette, double time)
    {
        var pulse = 0.78f + 0.16f * MathF.Sin((float)time * 4.8f);
        DrawPixelDisc(context, center, 13, UiTheme.WithAlpha(palette.Warning, 0.075f * pulse));
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(center.X - 4, center.Y - 6, 8, 12), new Color(54, 43, 42));
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(center.X - 2, center.Y - 4, 4, 8), UiTheme.WithAlpha(palette.Warning, pulse));
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(center.X - 5, center.Y - 7, 10, 2), new Color(119, 74, 47));
    }

    private static void DrawVine(
        RenderContext context,
        int x,
        int y,
        int length,
        UiPalette palette,
        double time,
        int phase)
    {
        var color = Color.Lerp(new Color(41, 111, 59), palette.Accent, 0.16f);
        for (var segment = 0; segment < length; segment += 6)
        {
            var sway = (int)MathF.Round(MathF.Sin((float)time * 0.55f + phase + segment * 0.08f) * 2f);
            context.SpriteBatch.Draw(context.Pixel, new Rectangle(x + sway, y + segment, 2, Math.Min(7, length - segment)), color);
            if (segment % 12 == 0)
            {
                context.SpriteBatch.Draw(context.Pixel, new Rectangle(x + sway + (phase % 2 == 0 ? 2 : -3), y + segment + 2, 4, 2), color);
            }
        }
    }

    private static void DrawSlime(RenderContext context, Point origin, UiPalette palette)
    {
        var body = Color.Lerp(new Color(56, 182, 151), palette.Accent, 0.18f);
        var shadow = new Color(29, 93, 91);
        DrawPixelDisc(context, new Point(origin.X, origin.Y + 2), 10, body);
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(origin.X - 10, origin.Y + 2, 21, 9), body);
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(origin.X - 6, origin.Y + 1, 3, 3), palette.Text);
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(origin.X + 4, origin.Y + 1, 3, 3), palette.Text);
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(origin.X - 7, origin.Y + 10, 15, 2), shadow);
    }

    private static void DrawButterfly(RenderContext context, Point origin, UiPalette palette, double time)
    {
        var flap = MathF.Sin((float)time * 8f) > 0f ? 0 : 2;
        var wing = Color.Lerp(new Color(120, 180, 242), palette.Accent, 0.18f);
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(origin.X - 6, origin.Y - 3 + flap, 5, 6 - flap), wing);
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(origin.X + 2, origin.Y - 3 + flap, 5, 6 - flap), wing);
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(origin.X, origin.Y - 2, 2, 7), new Color(45, 42, 54));
    }

    private static void DrawBird(RenderContext context, Point origin, double time, int phase)
    {
        var flap = (int)MathF.Round(MathF.Sin((float)time * 4.2f + phase) * 2f);
        var color = new Color(35, 42, 73, 205);
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(origin.X - 7, origin.Y + flap, 7, 2), color);
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(origin.X, origin.Y - flap, 7, 2), color);
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(origin.X - 1, origin.Y, 3, 2), color);
    }

    private static void DrawCloud(RenderContext context, Point origin, int scale, Color color)
    {
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(origin.X, origin.Y + 7 * scale, 58 * scale, 8 * scale), color);
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(origin.X + 8 * scale, origin.Y + 3 * scale, 18 * scale, 9 * scale), color);
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(origin.X + 23 * scale, origin.Y, 18 * scale, 12 * scale), color);
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(origin.X + 39 * scale, origin.Y + 5 * scale, 14 * scale, 8 * scale), color);
    }

    private static void DrawMountain(
        RenderContext context,
        int baseline,
        int centerX,
        int halfWidth,
        int height,
        int step,
        Color color)
    {
        if (halfWidth <= 0 || height <= 0)
        {
            return;
        }

        for (var x = centerX - halfWidth; x <= centerX + halfWidth; x += step)
        {
            var distance = Math.Abs(x - centerX) / (float)halfWidth;
            var ridge = 1f - distance;
            var shoulder = 1f - distance * distance;
            var columnHeight = Math.Max(1, (int)MathF.Round(height * (ridge * 0.62f + shoulder * 0.38f)));
            context.SpriteBatch.Draw(context.Pixel, new Rectangle(x, baseline - columnHeight, step + 1, columnHeight), color);
        }
    }

    private static void DrawPixelDisc(RenderContext context, Point center, int radius, Color color)
    {
        if (radius <= 0 || color.A == 0)
        {
            return;
        }

        var rowStep = radius >= 22 ? 3 : 2;
        for (var y = -radius; y <= radius; y += rowStep)
        {
            var normalized = y / (float)radius;
            var halfWidth = (int)MathF.Round(radius * MathF.Sqrt(Math.Max(0f, 1f - normalized * normalized)));
            context.SpriteBatch.Draw(
                context.Pixel,
                new Rectangle(center.X - halfWidth, center.Y + y, halfWidth * 2 + 1, rowStep + 1),
                color);
        }
    }

    private static void DrawEdgeShade(RenderContext context, Rectangle viewport, UiPalette palette)
    {
        var horizontal = Math.Clamp(viewport.Width / 32, 16, 64);
        var vertical = Math.Clamp(viewport.Height / 26, 12, 42);
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(viewport.X, viewport.Y, horizontal, viewport.Height), UiTheme.WithAlpha(palette.Backdrop, 0.26f));
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(viewport.Right - horizontal, viewport.Y, horizontal, viewport.Height), UiTheme.WithAlpha(palette.Backdrop, 0.26f));
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(viewport.X, viewport.Y, viewport.Width, vertical), UiTheme.WithAlpha(palette.Backdrop, 0.18f));
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(viewport.X, viewport.Bottom - vertical, viewport.Width, vertical), UiTheme.WithAlpha(palette.Backdrop, 0.28f));
    }

    private static int PositiveModulo(int value, int modulus)
    {
        if (modulus <= 0)
        {
            return 0;
        }

        var result = value % modulus;
        return result < 0 ? result + modulus : result;
    }
}

