using Game.Client.Rendering;
using Game.Core.Settings;
using Microsoft.Xna.Framework;

namespace Game.Client.UI;

public enum PixelMenuSceneMood
{
    Meadow,
    Twilight,
    Workshop
}

internal readonly record struct PixelMenuSceneLayout(
    Rectangle Sky,
    Rectangle Horizon,
    Rectangle Ground,
    Rectangle Soil,
    Rectangle SafeContent,
    bool ShowLargeTree,
    bool ShowCampfire);

/// <summary>
/// Draws the lightweight, deterministic living-sandbox vignette shared by menu states.
/// It intentionally uses the one-pixel texture so menu presentation never performs asset IO.
/// </summary>
public static class PixelMenuScene
{
    private const int SkyBands = 12;

    internal static PixelMenuSceneLayout Plan(Rectangle viewport)
    {
        var horizonY = viewport.Y + viewport.Height * 69 / 100;
        var groundHeight = Math.Clamp(viewport.Height * 10 / 100, 28, 76);
        var groundY = Math.Max(viewport.Y, viewport.Bottom - groundHeight);
        var soilY = Math.Min(viewport.Bottom, groundY + Math.Clamp(groundHeight / 5, 5, 10));
        var safeInset = Math.Clamp(viewport.Width / 24, 18, 64);
        return new PixelMenuSceneLayout(
            new Rectangle(viewport.X, viewport.Y, viewport.Width, Math.Max(1, horizonY - viewport.Y)),
            new Rectangle(viewport.X, horizonY, viewport.Width, Math.Max(1, groundY - horizonY)),
            new Rectangle(viewport.X, groundY, viewport.Width, Math.Max(1, soilY - groundY)),
            new Rectangle(viewport.X, soilY, viewport.Width, Math.Max(1, viewport.Bottom - soilY)),
            new Rectangle(
                viewport.X + safeInset,
                viewport.Y + Math.Clamp(viewport.Height / 18, 16, 42),
                Math.Max(1, viewport.Width - safeInset * 2),
                Math.Max(1, viewport.Height - Math.Clamp(viewport.Height / 18, 16, 42) * 2)),
            viewport.Width >= 900 && viewport.Height >= 500,
            viewport.Width >= 760 && viewport.Height >= 420);
    }

    public static void Draw(
        RenderContext context,
        UiPalette palette,
        GameSettings settings,
        PixelMenuSceneMood mood = PixelMenuSceneMood.Meadow)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var layout = Plan(context.ViewportBounds);
        var time = settings.Ui.ReducedMotion
            ? 0d
            : context.Time.TotalSeconds * Math.Clamp(settings.Ui.AnimationSpeed, 0.1f, 4f);

        DrawSky(context, layout, palette, mood, time);
        DrawClouds(context, layout, palette, mood, time);
        DrawMountainRanges(context, layout, palette, mood);
        DrawFloatingIslands(context, layout, palette, mood, time);
        DrawHills(context, layout, palette, mood);
        DrawDistantForest(context, layout, palette, time);
        if (layout.ShowLargeTree)
        {
            DrawLargeTree(context, layout, palette, time);
        }

        DrawGround(context, layout, palette);
        DrawGrass(context, layout, palette, time);
        DrawLivingDetails(context, layout, palette, mood, time);
        DrawEdgeShade(context, context.ViewportBounds, palette);
    }

    private static void DrawSky(
        RenderContext context,
        PixelMenuSceneLayout layout,
        UiPalette palette,
        PixelMenuSceneMood mood,
        double time)
    {
        var top = mood switch
        {
            PixelMenuSceneMood.Twilight => new Color(18, 26, 57),
            PixelMenuSceneMood.Workshop => new Color(31, 35, 57),
            _ => new Color(44, 105, 145)
        };
        var bottom = mood switch
        {
            PixelMenuSceneMood.Twilight => new Color(115, 70, 96),
            PixelMenuSceneMood.Workshop => new Color(137, 79, 72),
            _ => new Color(121, 188, 174)
        };
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
            context.SpriteBatch.Draw(
                context.Pixel,
                new Rectangle(layout.Sky.X, y, layout.Sky.Width, height),
                Color.Lerp(top, bottom, amount));
        }

        var orbX = layout.Sky.X + layout.Sky.Width * 79 / 100;
        var orbY = layout.Sky.Y + Math.Clamp(layout.Sky.Height / 5, 42, 118);
        var orbRadius = Math.Clamp(layout.Sky.Height / 14, 18, 38);
        var orbColor = mood == PixelMenuSceneMood.Meadow
            ? new Color(255, 226, 139)
            : new Color(245, 226, 198);
        DrawPixelDisc(context, new Point(orbX, orbY), orbRadius + 7, UiTheme.WithAlpha(orbColor, 0.10f));
        DrawPixelDisc(context, new Point(orbX, orbY), orbRadius, orbColor);

        if (mood != PixelMenuSceneMood.Meadow)
        {
            for (var index = 0; index < 19; index++)
            {
                var x = layout.Sky.X + PositiveModulo(index * 137 + 41, Math.Max(1, layout.Sky.Width));
                var y = layout.Sky.Y + 18 + PositiveModulo(index * 67, Math.Max(1, layout.Sky.Height * 3 / 5));
                var twinkle = 0.38f + 0.42f * (0.5f + 0.5f * MathF.Sin((float)time * 1.6f + index * 1.91f));
                var size = index % 5 == 0 ? 2 : 1;
                context.SpriteBatch.Draw(context.Pixel, new Rectangle(x, y, size, size), UiTheme.WithAlpha(palette.Text, twinkle));
            }
        }
    }

    private static void DrawClouds(
        RenderContext context,
        PixelMenuSceneLayout layout,
        UiPalette palette,
        PixelMenuSceneMood mood,
        double time)
    {
        var cloudColor = mood == PixelMenuSceneMood.Meadow
            ? new Color(226, 239, 222)
            : Color.Lerp(palette.TextMuted, palette.SurfaceHover, 0.35f);
        var wrapWidth = Math.Max(1, layout.Sky.Width + 260);
        for (var index = 0; index < 4; index++)
        {
            var speed = 5d + index * 1.8d;
            var x = layout.Sky.X - 150 + PositiveModulo((int)Math.Round(index * 271d + time * speed), wrapWidth);
            var y = layout.Sky.Y + 52 + index * Math.Max(24, layout.Sky.Height / 10);
            var scale = index % 2 == 0 ? 2 : 1;
            DrawCloud(context, new Point(x, y), scale, UiTheme.WithAlpha(cloudColor, mood == PixelMenuSceneMood.Meadow ? 0.58f : 0.26f));
        }
    }

    private static void DrawMountainRanges(
        RenderContext context,
        PixelMenuSceneLayout layout,
        UiPalette palette,
        PixelMenuSceneMood mood)
    {
        var baseline = layout.Horizon.Bottom;
        var far = mood == PixelMenuSceneMood.Meadow
            ? new Color(75, 105, 139)
            : Color.Lerp(new Color(50, 55, 99), palette.AccentSoft, 0.10f);
        var near = mood == PixelMenuSceneMood.Meadow
            ? new Color(52, 79, 108)
            : Color.Lerp(new Color(39, 45, 79), palette.SurfaceHover, 0.18f);
        var snow = mood == PixelMenuSceneMood.Meadow
            ? new Color(185, 208, 211)
            : Color.Lerp(palette.TextMuted, palette.SurfaceHover, 0.32f);

        DrawMountainPeak(context, baseline, layout.Sky.X + layout.Sky.Width * 12 / 100, layout.Sky.Width * 15 / 100, layout.Sky.Height * 28 / 100, far, snow);
        DrawMountainPeak(context, baseline, layout.Sky.X + layout.Sky.Width * 36 / 100, layout.Sky.Width * 19 / 100, layout.Sky.Height * 39 / 100, near, snow);
        DrawMountainPeak(context, baseline, layout.Sky.X + layout.Sky.Width * 66 / 100, layout.Sky.Width * 18 / 100, layout.Sky.Height * 31 / 100, far, snow);
        DrawMountainPeak(context, baseline, layout.Sky.X + layout.Sky.Width * 91 / 100, layout.Sky.Width * 20 / 100, layout.Sky.Height * 43 / 100, near, snow);
    }

    private static void DrawFloatingIslands(
        RenderContext context,
        PixelMenuSceneLayout layout,
        UiPalette palette,
        PixelMenuSceneMood mood,
        double time)
    {
        if (layout.Sky.Width < 520 || layout.Sky.Height < 210)
        {
            return;
        }

        var rock = mood == PixelMenuSceneMood.Meadow
            ? new Color(52, 63, 76)
            : Color.Lerp(new Color(45, 42, 65), palette.SurfaceRaised, 0.22f);
        var moss = mood == PixelMenuSceneMood.Meadow
            ? new Color(75, 126, 70)
            : Color.Lerp(new Color(70, 105, 75), palette.Accent, 0.12f);
        var water = mood == PixelMenuSceneMood.Meadow
            ? new Color(112, 193, 211)
            : Color.Lerp(new Color(97, 160, 204), palette.Accent, 0.16f);
        var drift = (int)MathF.Round(MathF.Sin((float)time * 0.22f) * 2f);

        DrawFloatingIsland(
            context,
            new Point(layout.Sky.X + layout.Sky.Width * 43 / 100, layout.Sky.Y + layout.Sky.Height * 36 / 100 + drift),
            Math.Clamp(layout.Sky.Width / 18, 42, 84),
            rock,
            moss,
            water,
            palette,
            variant: 0);
        DrawFloatingIsland(
            context,
            new Point(layout.Sky.X + layout.Sky.Width * 67 / 100, layout.Sky.Y + layout.Sky.Height * 24 / 100 - drift),
            Math.Clamp(layout.Sky.Width / 23, 34, 68),
            rock,
            moss,
            water,
            palette,
            variant: 1);
        if (layout.Sky.Width >= 980)
        {
            DrawFloatingIsland(
                context,
                new Point(layout.Sky.X + layout.Sky.Width * 79 / 100, layout.Sky.Y + layout.Sky.Height * 43 / 100 + drift),
                Math.Clamp(layout.Sky.Width / 20, 38, 76),
                rock,
                moss,
                water,
                palette,
                variant: 2);
        }
    }

    private static void DrawFloatingIsland(
        RenderContext context,
        Point center,
        int halfWidth,
        Color rock,
        Color moss,
        Color water,
        UiPalette palette,
        int variant)
    {
        var topY = center.Y;
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(center.X - halfWidth, topY, halfWidth * 2, 5), rock);
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(center.X - halfWidth + 2, topY - 3, halfWidth * 2 - 4, 4), moss);
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(center.X - halfWidth + 7, topY - 5, Math.Max(4, halfWidth * 2 - 14), 2), Color.Lerp(moss, palette.Warning, 0.16f));

        const int slices = 7;
        for (var slice = 0; slice < slices; slice++)
        {
            var y = topY + 5 + slice * 5;
            var inset = slice * halfWidth / (slices + 1);
            var width = Math.Max(3, halfWidth * 2 - inset * 2);
            context.SpriteBatch.Draw(
                context.Pixel,
                new Rectangle(center.X - width / 2, y, width, 6),
                Color.Lerp(rock, palette.Backdrop, slice / (float)(slices + 2)));
        }

        var rootX = center.X - halfWidth / 3 + variant * Math.Max(2, halfWidth / 5);
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(rootX, topY + 5, 3, 29 + variant * 5), Color.Lerp(rock, palette.Backdrop, 0.42f));
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(center.X + halfWidth / 4, topY + 4, 2, 18 + variant * 4), moss);

        var treeX = center.X - halfWidth / 3;
        var treeHeight = 18 + variant * 3;
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(treeX, topY - treeHeight, 3, treeHeight), new Color(73, 49, 35));
        DrawPixelDisc(context, new Point(treeX + 1, topY - treeHeight), 8 + variant * 2, Color.Lerp(moss, palette.Accent, 0.12f));

        var fallX = center.X + halfWidth / 3;
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(fallX, topY + 3, 2, 31 + variant * 7), UiTheme.WithAlpha(water, 0.78f));
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(fallX + 2, topY + 7, 1, 25 + variant * 7), UiTheme.WithAlpha(Color.White, 0.26f));
    }

    private static void DrawMountainPeak(
        RenderContext context,
        int baseline,
        int centerX,
        int halfWidth,
        int height,
        Color rock,
        Color snow)
    {
        if (halfWidth <= 0 || height <= 0)
        {
            return;
        }

        var step = Math.Clamp(halfWidth / 18, 4, 11);
        for (var x = centerX - halfWidth; x <= centerX + halfWidth; x += step)
        {
            var distance = Math.Abs(x - centerX) / (float)halfWidth;
            var ridgeNoise = PositiveModulo((x - centerX) / Math.Max(1, step) * 17, 13);
            var columnHeight = Math.Max(1, (int)MathF.Round(height * (1f - distance)) + ridgeNoise - 6);
            context.SpriteBatch.Draw(context.Pixel, new Rectangle(x, baseline - columnHeight, step + 1, columnHeight), rock);
            if (columnHeight > height * 3 / 5)
            {
                var cap = Math.Clamp(columnHeight / 7, 3, 10);
                context.SpriteBatch.Draw(context.Pixel, new Rectangle(x, baseline - columnHeight, step + 1, cap), UiTheme.WithAlpha(snow, 0.72f));
            }
        }
    }

    private static void DrawHills(
        RenderContext context,
        PixelMenuSceneLayout layout,
        UiPalette palette,
        PixelMenuSceneMood mood)
    {
        var back = mood == PixelMenuSceneMood.Meadow
            ? new Color(61, 102, 102)
            : Color.Lerp(palette.SurfaceHover, palette.AccentSoft, 0.16f);
        var front = mood == PixelMenuSceneMood.Meadow
            ? new Color(46, 75, 72)
            : Color.Lerp(palette.SurfaceRaised, palette.AccentSoft, 0.12f);
        var baseline = layout.Horizon.Bottom;
        DrawPixelHill(context, baseline, layout.Horizon.X + layout.Horizon.Width * 18 / 100, layout.Horizon.Width * 28 / 100, layout.Sky.Height * 27 / 100, 10, back);
        DrawPixelHill(context, baseline, layout.Horizon.X + layout.Horizon.Width * 55 / 100, layout.Horizon.Width * 33 / 100, layout.Sky.Height * 20 / 100, 9, back);
        DrawPixelHill(context, baseline, layout.Horizon.X + layout.Horizon.Width * 88 / 100, layout.Horizon.Width * 26 / 100, layout.Sky.Height * 31 / 100, 10, back);
        DrawPixelHill(context, baseline, layout.Horizon.X + layout.Horizon.Width * 7 / 100, layout.Horizon.Width * 22 / 100, layout.Sky.Height * 15 / 100, 8, front);
        DrawPixelHill(context, baseline, layout.Horizon.X + layout.Horizon.Width * 73 / 100, layout.Horizon.Width * 38 / 100, layout.Sky.Height * 17 / 100, 8, front);
    }

    private static void DrawDistantForest(
        RenderContext context,
        PixelMenuSceneLayout layout,
        UiPalette palette,
        double time)
    {
        var baseline = layout.Horizon.Bottom;
        var treeColor = Color.Lerp(palette.Surface, new Color(31, 62, 52), 0.48f);
        var stride = Math.Clamp(layout.Horizon.Width / 24, 28, 54);
        for (var x = layout.Horizon.X - stride; x < layout.Horizon.Right + stride; x += stride)
        {
            var index = (x - layout.Horizon.X) / Math.Max(1, stride);
            var height = 26 + PositiveModulo(index * 17, 27);
            var sway = (int)MathF.Round(MathF.Sin((float)time * 0.34f + index) * 1.2f);
            context.SpriteBatch.Draw(context.Pixel, new Rectangle(x + stride / 2 - 2, baseline - height, 4, height), treeColor);
            DrawPixelDisc(context, new Point(x + stride / 2 + sway, baseline - height), 8 + PositiveModulo(index * 7, 7), treeColor);
        }

        context.SpriteBatch.Draw(context.Pixel, layout.Horizon, UiTheme.WithAlpha(treeColor, 0.34f));
    }

    private static void DrawLargeTree(
        RenderContext context,
        PixelMenuSceneLayout layout,
        UiPalette palette,
        double time)
    {
        var groundY = layout.Ground.Y;
        var trunkX = layout.Sky.X + layout.Sky.Width * 84 / 100;
        var trunkWidth = Math.Clamp(layout.Sky.Width / 64, 18, 30);
        var trunkTop = layout.Sky.Y + layout.Sky.Height * 26 / 100;
        var bark = new Color(76, 50, 38);
        var barkLight = new Color(119, 75, 48);
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(trunkX, trunkTop, trunkWidth, groundY - trunkTop + 5), bark);
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(trunkX + 4, trunkTop + 8, 4, groundY - trunkTop - 4), barkLight);
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(trunkX - 24, groundY - 6, 30, 8), bark);
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(trunkX + trunkWidth - 2, groundY - 5, 32, 7), bark);

        var branchColor = bark;
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(trunkX - 72, trunkTop + 54, 80, 9), branchColor);
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(trunkX + trunkWidth - 4, trunkTop + 86, 74, 8), branchColor);
        var leaf = Color.Lerp(new Color(42, 102, 61), palette.Accent, 0.18f);
        var leafDark = Color.Lerp(new Color(26, 70, 48), palette.Surface, 0.12f);
        var sway = (int)MathF.Round(MathF.Sin((float)time * 0.58f) * 2f);
        DrawPixelDisc(context, new Point(trunkX - 42 + sway, trunkTop + 38), 46, leafDark);
        DrawPixelDisc(context, new Point(trunkX + 4 + sway, trunkTop + 12), 58, leaf);
        DrawPixelDisc(context, new Point(trunkX + 62 + sway, trunkTop + 55), 44, leafDark);
        DrawPixelDisc(context, new Point(trunkX + 30 + sway, trunkTop + 48), 52, leaf);
        for (var index = 0; index < 8; index++)
        {
            var leafX = trunkX - 58 + index * 19 + sway;
            var leafY = trunkTop + 9 + PositiveModulo(index * 23, 55);
            context.SpriteBatch.Draw(context.Pixel, new Rectangle(leafX, leafY, 7, 4), UiTheme.WithAlpha(palette.Warning, 0.22f));
        }
    }

    private static void DrawGround(RenderContext context, PixelMenuSceneLayout layout, UiPalette palette)
    {
        var grass = Color.Lerp(new Color(69, 120, 64), palette.Accent, 0.12f);
        var grassLight = Color.Lerp(grass, palette.Warning, 0.16f);
        var soil = new Color(80, 57, 43);
        var deepSoil = new Color(47, 38, 37);
        context.SpriteBatch.Draw(context.Pixel, layout.Ground, grass);
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(layout.Ground.X, layout.Ground.Y, layout.Ground.Width, Math.Min(4, layout.Ground.Height)), grassLight);
        context.SpriteBatch.Draw(context.Pixel, layout.Soil, soil);
        if (layout.Soil.Height > 12)
        {
            context.SpriteBatch.Draw(
                context.Pixel,
                new Rectangle(layout.Soil.X, layout.Soil.Y + layout.Soil.Height * 2 / 3, layout.Soil.Width, layout.Soil.Height - layout.Soil.Height * 2 / 3),
                deepSoil);
        }

        for (var x = layout.Soil.X + 13; x < layout.Soil.Right; x += 37)
        {
            var y = layout.Soil.Y + 8 + PositiveModulo(x * 5, Math.Max(1, layout.Soil.Height - 9));
            context.SpriteBatch.Draw(context.Pixel, new Rectangle(x, y, 3, 2), UiTheme.WithAlpha(palette.TextMuted, 0.20f));
        }
    }

    private static void DrawGrass(
        RenderContext context,
        PixelMenuSceneLayout layout,
        UiPalette palette,
        double time)
    {
        var color = Color.Lerp(new Color(104, 160, 74), palette.Accent, 0.14f);
        for (var x = layout.Ground.X + 7; x < layout.Ground.Right; x += 19)
        {
            var phase = x * 0.071f + (float)time * 0.8f;
            var lean = (int)MathF.Round(MathF.Sin(phase) * 2f);
            var height = 5 + PositiveModulo(x * 11, 8);
            context.SpriteBatch.Draw(context.Pixel, new Rectangle(x, layout.Ground.Y - height, 2, height + 1), color);
            context.SpriteBatch.Draw(context.Pixel, new Rectangle(x + lean, layout.Ground.Y - height, 4, 2), color);
        }
    }

    private static void DrawLivingDetails(
        RenderContext context,
        PixelMenuSceneLayout layout,
        UiPalette palette,
        PixelMenuSceneMood mood,
        double time)
    {
        var groundY = layout.Ground.Y;
        var travel = Math.Max(1, layout.Ground.Width / 4);
        var squirrelX = layout.Ground.X + layout.Ground.Width * 60 / 100 + PositiveModulo((int)Math.Round(time * 13d), travel);
        var squirrelHop = (int)MathF.Round(MathF.Abs(MathF.Sin((float)time * 2.2f)) * 3f);
        DrawSquirrel(context, new Point(squirrelX, groundY - 13 - squirrelHop), palette);

        if (layout.ShowCampfire)
        {
            var fireX = layout.Ground.X + layout.Ground.Width * 58 / 100;
            DrawCampfire(context, new Point(fireX, groundY), palette, time);
            DrawBoar(context, new Point(layout.Ground.X + layout.Ground.Width * 73 / 100, groundY - 16), palette, time);
        }

        var fireflyColor = mood == PixelMenuSceneMood.Meadow ? new Color(255, 225, 112) : palette.Warning;
        for (var index = 0; index < 11; index++)
        {
            var baseX = layout.Sky.X + layout.Sky.Width * 48 / 100 + PositiveModulo(index * 83, Math.Max(1, layout.Sky.Width / 2));
            var x = baseX + (int)MathF.Round(MathF.Sin((float)time * 0.9f + index * 2.1f) * 10f);
            var y = groundY - 30 - PositiveModulo(index * 47, Math.Max(28, layout.Sky.Height / 3)) +
                (int)MathF.Round(MathF.Cos((float)time * 1.2f + index) * 5f);
            var glow = 0.45f + 0.4f * (0.5f + 0.5f * MathF.Sin((float)time * 2.7f + index * 1.7f));
            context.SpriteBatch.Draw(context.Pixel, new Rectangle(x - 2, y - 2, 5, 5), UiTheme.WithAlpha(fireflyColor, glow * 0.16f));
            context.SpriteBatch.Draw(context.Pixel, new Rectangle(x, y, 2, 2), UiTheme.WithAlpha(fireflyColor, glow));
        }
    }

    private static void DrawSquirrel(RenderContext context, Point origin, UiPalette palette)
    {
        var fur = Color.Lerp(new Color(174, 104, 60), palette.Warning, 0.18f);
        var shadow = new Color(99, 58, 43);
        DrawPixelDisc(context, new Point(origin.X - 5, origin.Y + 3), 6, fur);
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(origin.X - 2, origin.Y, 13, 9), fur);
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(origin.X + 7, origin.Y - 4, 7, 7), fur);
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(origin.X + 8, origin.Y - 7, 3, 4), shadow);
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(origin.X + 11, origin.Y - 1, 2, 2), palette.Text);
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(origin.X, origin.Y + 8, 4, 3), shadow);
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(origin.X + 8, origin.Y + 7, 5, 3), shadow);
    }

    private static void DrawBoar(RenderContext context, Point origin, UiPalette palette, double time)
    {
        var body = Color.Lerp(new Color(77, 66, 60), palette.SurfaceHover, 0.18f);
        var snout = new Color(142, 94, 77);
        var bob = (int)MathF.Round(MathF.Sin((float)time * 1.7f) * 1f);
        UiTheme.DrawRoundedRectangle(context, new Rectangle(origin.X, origin.Y + bob, 30, 14), body, 5);
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(origin.X + 25, origin.Y + 5 + bob, 10, 8), snout);
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(origin.X + 23, origin.Y + 1 + bob, 4, 5), body);
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(origin.X + 27, origin.Y + 3 + bob, 2, 2), palette.Text);
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(origin.X + 4, origin.Y + 12 + bob, 4, 7), body);
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(origin.X + 22, origin.Y + 12 + bob, 4, 7), body);
    }

    private static void DrawCampfire(RenderContext context, Point origin, UiPalette palette, double time)
    {
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(origin.X - 12, origin.Y - 4, 24, 4), new Color(62, 47, 42));
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(origin.X - 10, origin.Y - 7, 19, 3), new Color(119, 72, 44));
        var flicker = (int)MathF.Round(MathF.Sin((float)time * 7.2f) * 2f);
        DrawPixelDisc(context, new Point(origin.X, origin.Y - 15), 12, UiTheme.WithAlpha(palette.Warning, 0.12f));
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(origin.X - 5, origin.Y - 19, 10, 13), new Color(235, 94, 55));
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(origin.X - 3 + flicker, origin.Y - 25, 7, 14), palette.Warning);
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(origin.X - 1, origin.Y - 17, 4, 9), new Color(255, 240, 160));
    }

    private static void DrawCloud(RenderContext context, Point origin, int scale, Color color)
    {
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(origin.X, origin.Y + 7 * scale, 52 * scale, 9 * scale), color);
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(origin.X + 9 * scale, origin.Y + 3 * scale, 17 * scale, 10 * scale), color);
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(origin.X + 24 * scale, origin.Y, 15 * scale, 13 * scale), color);
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(origin.X + 39 * scale, origin.Y + 5 * scale, 9 * scale, 8 * scale), color);
    }

    private static void DrawPixelHill(
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
            var columnHeight = Math.Max(1, (int)MathF.Round(height * (1f - distance * distance)));
            context.SpriteBatch.Draw(context.Pixel, new Rectangle(x, baseline - columnHeight, step + 1, columnHeight), color);
        }
    }

    private static void DrawPixelDisc(RenderContext context, Point center, int radius, Color color)
    {
        if (radius <= 0 || color.A == 0)
        {
            return;
        }

        var rowStep = radius >= 20 ? 3 : 2;
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
        var horizontal = Math.Clamp(viewport.Width / 40, 12, 40);
        var vertical = Math.Clamp(viewport.Height / 32, 8, 28);
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(viewport.X, viewport.Y, horizontal, viewport.Height), UiTheme.WithAlpha(palette.Backdrop, 0.18f));
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(viewport.Right - horizontal, viewport.Y, horizontal, viewport.Height), UiTheme.WithAlpha(palette.Backdrop, 0.18f));
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(viewport.X, viewport.Y, viewport.Width, vertical), UiTheme.WithAlpha(palette.Backdrop, 0.16f));
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(viewport.X, viewport.Bottom - vertical, viewport.Width, vertical), UiTheme.WithAlpha(palette.Backdrop, 0.18f));
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

