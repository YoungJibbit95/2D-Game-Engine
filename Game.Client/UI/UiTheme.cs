using Game.Client.Rendering;
using Game.Core.Settings;
using Microsoft.Xna.Framework;

namespace Game.Client.UI;

public readonly record struct UiPalette(
    Color Backdrop,
    Color Surface,
    Color SurfaceRaised,
    Color SurfaceHover,
    Color Accent,
    Color AccentSoft,
    Color Text,
    Color TextMuted,
    Color Warning,
    Color Danger);

public readonly record struct UiTypographyTokens(
    int DisplayScale,
    int TitleScale,
    int BodyScale,
    int CaptionScale,
    int BodyLineHeight,
    int DenseLineHeight);

public readonly record struct UiSpacingTokens(int Xs, int Sm, int Md, int Lg, int Xl, int Xxl);

public readonly record struct UiSurfaceSpec(
    int CornerRadius,
    int BorderThickness,
    Point ShadowOffset,
    float ShadowOpacity,
    int GlowSpread,
    float GlowOpacity,
    int GradientSteps);

public readonly record struct UiBackdropSpec(int BlurRadius, float Saturation, float TintOpacity);

public readonly record struct UiThemeContract(
    UiTypographyTokens Typography,
    UiSpacingTokens Spacing,
    UiSurfaceSpec Panel,
    UiSurfaceSpec Button,
    UiSurfaceSpec Tooltip,
    UiBackdropSpec Backdrop);

public static class UiTheme
{
    public static UiThemeContract Contract => ResolveContract();

    public static UiThemeContract ResolveContract(GameSettings? settings = null)
    {
        var ui = settings?.Ui ?? new UiSettings();
        var rendering = settings?.Rendering ?? new RenderingSettings();
        var accessibility = settings?.Accessibility ?? new AccessibilitySettings();
        var quality = Math.Clamp(rendering.UiEffectQuality, 0, 3);
        var qualityFactor = quality / 3f;
        var radius = Math.Clamp(ui.CornerRadiusPixels, 0, 16);
        var glowStrength = Math.Clamp(ui.GlowStrength, 0f, 1f);
        if (accessibility.ScreenFlashReduction)
        {
            glowStrength *= 0.5f;
        }

        var gradientSteps = quality switch
        {
            0 => 1,
            1 => 4,
            2 => 8,
            _ => 12
        };
        var glowSpread = quality == 0 || glowStrength <= 0f
            ? 0
            : Math.Clamp(1 + (int)MathF.Round(glowStrength * qualityFactor * 2f), 1, 3);
        var effectiveBlurRadius = quality == 0
            ? 0
            : Math.Clamp(
                (int)MathF.Round(rendering.BlurRadiusPixels * Math.Clamp(ui.BackdropBlurStrength, 0f, 1f) * Math.Max(0.34f, qualityFactor)),
                0,
                24);
        var textScale = Math.Clamp(ui.TextScale, 0.75f, 2f);

        return new UiThemeContract(
            new UiTypographyTokens(
                DisplayScale: ScaleText(4, textScale, 3, 6),
                TitleScale: ScaleText(3, textScale, 2, 4),
                BodyScale: ScaleText(2, textScale, 1, 3),
                CaptionScale: ScaleText(1, textScale, 1, 2),
                BodyLineHeight: Math.Clamp((int)MathF.Round(20f * textScale), 15, 34),
                DenseLineHeight: Math.Clamp((int)MathF.Round(14f * textScale), 11, 24)),
            new UiSpacingTokens(Xs: 4, Sm: 6, Md: 8, Lg: 12, Xl: 18, Xxl: 24),
            new UiSurfaceSpec(
                CornerRadius: radius,
                BorderThickness: ui.HighContrastPanels ? 2 : 1,
                ShadowOffset: quality == 0 ? new Point(2, 2) : new Point(5, 6),
                ShadowOpacity: quality == 0 ? 0.22f : 0.34f,
                GlowSpread: glowSpread,
                GlowOpacity: 0.42f * glowStrength * qualityFactor,
                GradientSteps: gradientSteps),
            new UiSurfaceSpec(
                CornerRadius: Math.Min(radius, 8),
                BorderThickness: ui.HighContrastPanels ? 2 : 1,
                ShadowOffset: quality == 0 ? new Point(1, 1) : new Point(2, 3),
                ShadowOpacity: quality == 0 ? 0.14f : 0.24f,
                GlowSpread: Math.Min(1, glowSpread),
                GlowOpacity: 0.32f * glowStrength * qualityFactor,
                GradientSteps: Math.Max(1, gradientSteps / 2)),
            new UiSurfaceSpec(
                CornerRadius: Math.Min(radius, 8),
                BorderThickness: ui.HighContrastPanels ? 2 : 1,
                ShadowOffset: quality == 0 ? new Point(2, 2) : new Point(3, 4),
                ShadowOpacity: quality == 0 ? 0.20f : 0.30f,
                GlowSpread: Math.Min(1, glowSpread),
                GlowOpacity: 0.32f * glowStrength * qualityFactor,
                GradientSteps: Math.Max(1, gradientSteps / 2)),
            new UiBackdropSpec(
                BlurRadius: effectiveBlurRadius,
                Saturation: Math.Clamp(0.82f + (accessibility.InterfaceContrast - 1f) * 0.12f, 0.72f, 0.96f),
                TintOpacity: Math.Clamp(ui.MenuBackdropOpacity, 0f, 1f)));
    }

    public static UiPalette Resolve(GameSettings? settings = null)
    {
        var theme = settings?.Ui.Theme ?? "Midnight";
        var palette = theme.ToUpperInvariant() switch
        {
            "EMBER" => new UiPalette(
                new Color(14, 11, 10),
                new Color(25, 22, 21),
                new Color(42, 35, 31),
                new Color(58, 45, 38),
                new Color(232, 157, 89),
                new Color(144, 91, 65),
                new Color(248, 241, 225),
                new Color(184, 169, 151),
                new Color(245, 209, 117),
                new Color(219, 86, 72)),
            "FOREST" => new UiPalette(
                new Color(9, 15, 13),
                new Color(16, 25, 22),
                new Color(29, 43, 36),
                new Color(39, 58, 49),
                new Color(114, 190, 126),
                new Color(67, 121, 88),
                new Color(229, 242, 229),
                new Color(156, 184, 164),
                new Color(225, 214, 111),
                new Color(210, 82, 78)),
            _ => new UiPalette(
                new Color(9, 12, 17),
                new Color(20, 24, 31),
                new Color(34, 39, 48),
                new Color(49, 58, 68),
                new Color(91, 194, 205),
                new Color(55, 117, 128),
                new Color(246, 242, 226),
                new Color(166, 177, 180),
                new Color(250, 196, 79),
                new Color(225, 78, 76))
        };

        if (settings is null)
        {
            return palette;
        }

        var contrast = Math.Clamp(settings.Accessibility.InterfaceContrast, 0.5f, 2f);
        if (settings.Ui.HighContrastPanels)
        {
            contrast = Math.Max(contrast, 1.35f);
            palette = palette with
            {
                Surface = Darken(palette.Surface, 0.22f),
                SurfaceRaised = Darken(palette.SurfaceRaised, 0.12f),
                Text = Color.White,
                TextMuted = Lighten(palette.TextMuted, 0.18f)
            };
        }

        palette = palette with
        {
            Surface = AdjustContrast(palette.Surface, contrast),
            SurfaceRaised = AdjustContrast(palette.SurfaceRaised, contrast),
            SurfaceHover = AdjustContrast(palette.SurfaceHover, contrast),
            Text = AdjustContrast(palette.Text, contrast),
            TextMuted = AdjustContrast(palette.TextMuted, contrast)
        };

        if (settings.Accessibility.ColorBlindFriendlyIndicators)
        {
            palette = palette with
            {
                Warning = new Color(255, 219, 92),
                Danger = new Color(235, 91, 179)
            };
        }

        return palette;
    }

    public static Color WithAlpha(Color color, float alpha)
    {
        return new Color(color.R, color.G, color.B, (byte)Math.Clamp((int)MathF.Round(alpha * 255f), 0, 255));
    }

    public static void DrawBackdrop(RenderContext context, UiPalette palette, float opacity, GameSettings? settings = null)
    {
        // The contract exposes blur/saturation for an effect-backed renderer. The pixel fallback is deterministic.
        var backdrop = ResolveContract(settings).Backdrop;
        var blurMix = backdrop.BlurRadius / 24f;
        var tint = Math.Clamp(opacity, 0f, 1f) * Math.Clamp(0.72f + backdrop.TintOpacity * 0.28f + blurMix * 0.12f, 0f, 1f);
        context.SpriteBatch.Draw(context.Pixel, context.ViewportBounds, WithAlpha(palette.Backdrop, tint));
        var horizon = new Rectangle(0, context.ViewportBounds.Height / 2, context.ViewportBounds.Width, context.ViewportBounds.Height / 2);
        context.SpriteBatch.Draw(context.Pixel, horizon, WithAlpha(palette.AccentSoft, (0.04f + blurMix * 0.08f) * tint));
    }

    public static void DrawPanel(RenderContext context, Rectangle bounds, UiPalette palette, float opacity, bool raised = true, GameSettings? settings = null)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        var spec = ResolveContract(settings).Panel;
        var shadow = new Rectangle(
            bounds.X + spec.ShadowOffset.X,
            bounds.Y + spec.ShadowOffset.Y,
            bounds.Width,
            bounds.Height);
        DrawRoundedRectangle(context, shadow, WithAlpha(Color.Black, spec.ShadowOpacity * opacity), spec.CornerRadius);

        if (raised && spec.GlowSpread > 0 && spec.GlowOpacity > 0f)
        {
            var glow = Inflate(bounds, spec.GlowSpread);
            DrawRoundedBorder(context, glow, WithAlpha(palette.Accent, spec.GlowOpacity * opacity), spec.CornerRadius + spec.GlowSpread, 1);
        }

        var top = raised ? Lighten(palette.SurfaceRaised, 0.08f) : Lighten(palette.Surface, 0.04f);
        var bottom = raised ? Darken(palette.SurfaceRaised, 0.10f) : Darken(palette.Surface, 0.08f);
        DrawRoundedGradient(context, bounds, WithAlpha(top, opacity), WithAlpha(bottom, opacity), spec.CornerRadius, spec.GradientSteps);
        DrawRoundedBorder(context, bounds, WithAlpha(palette.AccentSoft, 0.76f * opacity), spec.CornerRadius, spec.BorderThickness);

        var highlight = new Rectangle(bounds.X + spec.CornerRadius, bounds.Y + 2, Math.Max(0, bounds.Width - spec.CornerRadius * 2), 1);
        context.SpriteBatch.Draw(context.Pixel, highlight, WithAlpha(Color.White, 0.10f * opacity));
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(bounds.X + 9, bounds.Y + 7, 7, 2), WithAlpha(palette.Warning, 0.78f * opacity));
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(bounds.Right - 16, bounds.Y + 7, 7, 2), WithAlpha(palette.Warning, 0.78f * opacity));
    }

    public static void DrawButton(
        RenderContext context,
        Rectangle bounds,
        UiPalette palette,
        bool selected,
        bool hovered,
        bool enabled = true,
        bool pressed = false,
        bool focused = false,
        GameSettings? settings = null)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        var spec = ResolveContract(settings).Button;
        var activePressed = enabled && pressed;
        var fill = selected
            ? palette.SurfaceHover
            : hovered
                ? palette.SurfaceRaised
                : palette.Surface;
        if (activePressed)
        {
            fill = Darken(fill, 0.18f);
        }

        var alpha = enabled ? 0.96f : 0.48f;
        if (enabled && !activePressed && (hovered || selected))
        {
            var shadow = new Rectangle(bounds.X + spec.ShadowOffset.X, bounds.Y + spec.ShadowOffset.Y, bounds.Width, bounds.Height);
            DrawRoundedRectangle(context, shadow, WithAlpha(Color.Black, spec.ShadowOpacity), spec.CornerRadius);
        }

        DrawRoundedGradient(
            context,
            bounds,
            WithAlpha(Lighten(fill, activePressed ? 0.01f : 0.08f), alpha),
            WithAlpha(Darken(fill, activePressed ? 0.16f : 0.08f), alpha),
            spec.CornerRadius,
            spec.GradientSteps);

        var border = !enabled
            ? palette.SurfaceHover
            : selected
                ? palette.Accent
                : hovered
                    ? palette.Warning
                    : palette.SurfaceHover;
        DrawRoundedBorder(context, bounds, WithAlpha(border, enabled ? 0.96f : 0.42f), spec.CornerRadius, selected ? 2 : 1);

        if (activePressed)
        {
            DrawRoundedBorder(context, Inflate(bounds, -2), WithAlpha(Color.Black, 0.28f), Math.Max(1, spec.CornerRadius - 2), 1);
        }

        if (focused && enabled)
        {
            DrawFocusFrame(context, Inflate(bounds, 2), palette, spec.CornerRadius + 2, settings);
        }
    }

    public static void DrawSlot(RenderContext context, Rectangle bounds, UiPalette palette, bool selected, bool hovered, float opacity = 0.94f)
    {
        var inset = Inflate(bounds, -2);
        DrawRoundedRectangle(context, bounds, WithAlpha(selected ? palette.AccentSoft : palette.Backdrop, 0.72f * opacity), 4);
        DrawRoundedRectangle(context, inset, WithAlpha(hovered ? palette.SurfaceHover : palette.Surface, 0.94f * opacity), 3);
        DrawRoundedBorder(context, bounds, WithAlpha(selected ? palette.Accent : hovered ? palette.Warning : palette.SurfaceHover, selected ? 1f : 0.72f), 4, selected ? 2 : 1);
    }

    public static void DrawHeader(RenderContext context, Rectangle bounds, UiPalette palette, float opacity = 0.92f, GameSettings? settings = null)
    {
        var contract = ResolveContract(settings);
        DrawRoundedGradient(
            context,
            bounds,
            WithAlpha(Lighten(palette.Surface, 0.08f), opacity),
            WithAlpha(palette.Surface, opacity),
            Math.Min(contract.Panel.CornerRadius, 8),
            Math.Max(1, contract.Panel.GradientSteps / 2));
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(bounds.X + 6, bounds.Bottom - 2, Math.Max(0, bounds.Width - 12), 2), WithAlpha(palette.Accent, 0.82f * opacity));
    }

    public static void DrawProgressBar(RenderContext context, Rectangle bounds, float progress, UiPalette palette)
    {
        progress = Math.Clamp(progress, 0f, 1f);
        DrawRoundedRectangle(context, bounds, WithAlpha(palette.SurfaceRaised, 0.86f), 4);
        var fill = new Rectangle(bounds.X, bounds.Y, (int)MathF.Round(bounds.Width * progress), bounds.Height);
        DrawRoundedRectangle(context, fill, WithAlpha(palette.Accent, 0.94f), 4);
        DrawRoundedBorder(context, bounds, WithAlpha(palette.AccentSoft, 0.8f), 4, 1);
    }

    public static void DrawTooltipSurface(RenderContext context, Rectangle bounds, UiPalette palette, float opacity = 0.98f, GameSettings? settings = null)
    {
        var spec = ResolveContract(settings).Tooltip;
        var shadow = new Rectangle(bounds.X + spec.ShadowOffset.X, bounds.Y + spec.ShadowOffset.Y, bounds.Width, bounds.Height);
        DrawRoundedRectangle(context, shadow, WithAlpha(Color.Black, spec.ShadowOpacity * opacity), spec.CornerRadius);
        DrawRoundedGradient(context, bounds, WithAlpha(Lighten(palette.SurfaceRaised, 0.07f), opacity), WithAlpha(palette.Surface, opacity), spec.CornerRadius, spec.GradientSteps);
        DrawRoundedBorder(context, bounds, WithAlpha(palette.Warning, 0.72f * opacity), spec.CornerRadius, spec.BorderThickness);
    }

    public static void DrawScrollRail(RenderContext context, Rectangle rail, int offset, int visibleItems, int totalItems, UiPalette palette)
    {
        if (rail.Width <= 0 || rail.Height <= 0 || totalItems <= visibleItems)
        {
            return;
        }

        DrawRoundedRectangle(context, rail, WithAlpha(palette.SurfaceHover, 0.62f), Math.Min(3, rail.Width / 2));
        var thumbHeight = Math.Max(18, rail.Height * visibleItems / totalItems);
        var travel = Math.Max(0, rail.Height - thumbHeight);
        var maximumOffset = Math.Max(1, totalItems - visibleItems);
        var thumbY = rail.Y + travel * Math.Clamp(offset, 0, maximumOffset) / maximumOffset;
        DrawRoundedRectangle(context, new Rectangle(rail.X, thumbY, rail.Width, thumbHeight), palette.Accent, Math.Min(3, rail.Width / 2));
    }

    public static void DrawFocusFrame(RenderContext context, Rectangle bounds, UiPalette palette, int cornerRadius = 6, GameSettings? settings = null)
    {
        var thickness = settings?.Accessibility.HighContrastInteractionOutline == true ? 3 : 2;
        DrawRoundedBorder(context, bounds, palette.Warning, Math.Clamp(cornerRadius, 0, 16), thickness);
    }

    public static void DrawCursorAccent(RenderContext context, Point position, UiPalette palette, GameSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (!settings.Ui.LargeCursor)
        {
            return;
        }

        const int size = 14;
        var shadow = new Color(0, 0, 0, 210);
        for (var row = 0; row < size; row++)
        {
            var width = Math.Max(2, row / 2 + 2);
            context.SpriteBatch.Draw(context.Pixel, new Rectangle(position.X + 2, position.Y + row + 2, width, 2), shadow);
            context.SpriteBatch.Draw(context.Pixel, new Rectangle(position.X, position.Y + row, width, 2), palette.Warning);
        }

        DrawRoundedBorder(context, new Rectangle(position.X - 4, position.Y - 4, 23, 23), WithAlpha(palette.Text, 0.82f), 6, 1);
    }

    public static void DrawRoundedRectangle(RenderContext context, Rectangle bounds, Color color, int cornerRadius)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0 || color.A == 0)
        {
            return;
        }

        var radius = ClampRadius(bounds, cornerRadius);
        if (radius <= 0)
        {
            context.SpriteBatch.Draw(context.Pixel, bounds, color);
            return;
        }

        var middleHeight = bounds.Height - radius * 2;
        if (middleHeight > 0)
        {
            context.SpriteBatch.Draw(context.Pixel, new Rectangle(bounds.X, bounds.Y + radius, bounds.Width, middleHeight), color);
        }

        for (var y = 0; y < radius; y++)
        {
            var inset = CornerInset(radius, y);
            var width = Math.Max(0, bounds.Width - inset * 2);
            if (width == 0)
            {
                continue;
            }

            context.SpriteBatch.Draw(context.Pixel, new Rectangle(bounds.X + inset, bounds.Y + y, width, 1), color);
            context.SpriteBatch.Draw(context.Pixel, new Rectangle(bounds.X + inset, bounds.Bottom - y - 1, width, 1), color);
        }
    }

    public static void DrawRoundedBorder(RenderContext context, Rectangle bounds, Color color, int cornerRadius, int thickness)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0 || color.A == 0)
        {
            return;
        }

        var safeThickness = Math.Clamp(thickness, 1, Math.Max(1, Math.Min(bounds.Width, bounds.Height) / 2));
        for (var layer = 0; layer < safeThickness; layer++)
        {
            var current = Inflate(bounds, -layer);
            if (current.Width <= 0 || current.Height <= 0)
            {
                break;
            }

            DrawSingleRoundedBorder(context, current, color, Math.Max(0, cornerRadius - layer));
        }
    }

    public static void DrawBorder(RenderContext context, Rectangle bounds, Color color, int thickness)
    {
        DrawRoundedBorder(context, bounds, color, 0, thickness);
    }

    private static void DrawRoundedGradient(RenderContext context, Rectangle bounds, Color top, Color bottom, int cornerRadius, int steps)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        var radius = ClampRadius(bounds, cornerRadius);
        for (var y = 0; y < radius; y++)
        {
            DrawGradientRow(context, bounds, top, bottom, y, CornerInset(radius, y));
            var bottomY = bounds.Height - y - 1;
            if (bottomY != y)
            {
                DrawGradientRow(context, bounds, top, bottom, bottomY, CornerInset(radius, y));
            }
        }

        var middleStart = radius;
        var middleEnd = bounds.Height - radius;
        var middleHeight = middleEnd - middleStart;
        if (middleHeight <= 0)
        {
            return;
        }

        var bands = Math.Clamp(steps, 1, middleHeight);
        for (var band = 0; band < bands; band++)
        {
            var localY0 = middleStart + middleHeight * band / bands;
            var localY1 = middleStart + middleHeight * (band + 1) / bands;
            var sampleY = (localY0 + localY1 - 1) / 2;
            var color = Color.Lerp(top, bottom, bounds.Height == 1 ? 0f : sampleY / (float)(bounds.Height - 1));
            context.SpriteBatch.Draw(
                context.Pixel,
                new Rectangle(bounds.X, bounds.Y + localY0, bounds.Width, Math.Max(1, localY1 - localY0)),
                color);
        }
    }

    private static void DrawGradientRow(RenderContext context, Rectangle bounds, Color top, Color bottom, int localY, int inset)
    {
        var width = bounds.Width - inset * 2;
        if (width <= 0)
        {
            return;
        }

        var color = Color.Lerp(top, bottom, bounds.Height == 1 ? 0f : localY / (float)(bounds.Height - 1));
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(bounds.X + inset, bounds.Y + localY, width, 1), color);
    }

    private static void DrawSingleRoundedBorder(RenderContext context, Rectangle bounds, Color color, int cornerRadius)
    {
        var radius = ClampRadius(bounds, cornerRadius);
        if (radius <= 0)
        {
            context.SpriteBatch.Draw(context.Pixel, new Rectangle(bounds.X, bounds.Y, bounds.Width, 1), color);
            context.SpriteBatch.Draw(context.Pixel, new Rectangle(bounds.X, bounds.Bottom - 1, bounds.Width, 1), color);
            context.SpriteBatch.Draw(context.Pixel, new Rectangle(bounds.X, bounds.Y, 1, bounds.Height), color);
            context.SpriteBatch.Draw(context.Pixel, new Rectangle(bounds.Right - 1, bounds.Y, 1, bounds.Height), color);
            return;
        }

        context.SpriteBatch.Draw(context.Pixel, new Rectangle(bounds.X + radius, bounds.Y, Math.Max(0, bounds.Width - radius * 2), 1), color);
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(bounds.X + radius, bounds.Bottom - 1, Math.Max(0, bounds.Width - radius * 2), 1), color);
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(bounds.X, bounds.Y + radius, 1, Math.Max(0, bounds.Height - radius * 2)), color);
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(bounds.Right - 1, bounds.Y + radius, 1, Math.Max(0, bounds.Height - radius * 2)), color);

        for (var y = 0; y < radius; y++)
        {
            var inset = CornerInset(radius, y);
            context.SpriteBatch.Draw(context.Pixel, new Rectangle(bounds.X + inset, bounds.Y + y, 1, 1), color);
            context.SpriteBatch.Draw(context.Pixel, new Rectangle(bounds.Right - inset - 1, bounds.Y + y, 1, 1), color);
            context.SpriteBatch.Draw(context.Pixel, new Rectangle(bounds.X + inset, bounds.Bottom - y - 1, 1, 1), color);
            context.SpriteBatch.Draw(context.Pixel, new Rectangle(bounds.Right - inset - 1, bounds.Bottom - y - 1, 1, 1), color);
        }
    }

    private static int CornerInset(int radius, int y)
    {
        var sampleY = radius - y - 0.5f;
        var inside = Math.Max(0f, radius * radius - sampleY * sampleY);
        return Math.Max(0, (int)MathF.Ceiling(radius - MathF.Sqrt(inside)));
    }

    private static int ClampRadius(Rectangle bounds, int cornerRadius)
    {
        return Math.Clamp(cornerRadius, 0, Math.Max(0, Math.Min(bounds.Width, bounds.Height) / 2));
    }

    private static Rectangle Inflate(Rectangle bounds, int amount)
    {
        return new Rectangle(bounds.X - amount, bounds.Y - amount, bounds.Width + amount * 2, bounds.Height + amount * 2);
    }

    private static Color Lighten(Color color, float amount)
    {
        return Color.Lerp(color, Color.White, Math.Clamp(amount, 0f, 1f));
    }

    private static Color Darken(Color color, float amount)
    {
        return Color.Lerp(color, Color.Black, Math.Clamp(amount, 0f, 1f));
    }

    private static int ScaleText(int baseScale, float scale, int minimum, int maximum)
    {
        return Math.Clamp((int)MathF.Floor(baseScale * scale + 0.001f), minimum, maximum);
    }

    private static Color AdjustContrast(Color color, float contrast)
    {
        static byte Channel(byte value, float amount)
        {
            return (byte)Math.Clamp((int)MathF.Round(128f + (value - 128f) * amount), 0, 255);
        }

        return new Color(Channel(color.R, contrast), Channel(color.G, contrast), Channel(color.B, contrast), color.A);
    }
}
