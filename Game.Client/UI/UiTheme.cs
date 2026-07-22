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

public readonly record struct UiControlTokens(
    int MinimumHeight,
    int CompactHeight,
    int Gap,
    int LayerInset,
    int SliderTrackHeight,
    int SliderThumbWidth,
    int ToggleWidth,
    int IconBoxSize,
    int FocusRingOffset);

public readonly record struct UiThemeContract(
    UiTypographyTokens Typography,
    UiSpacingTokens Spacing,
    UiSurfaceSpec Panel,
    UiSurfaceSpec Button,
    UiSurfaceSpec Tooltip,
    UiControlTokens Controls,
    UiBackdropSpec Backdrop);

public readonly record struct UiMaterialPalette(
    Color FrameShadow,
    Color WoodDark,
    Color Wood,
    Color WoodLight,
    Color BrassDark,
    Color Brass,
    Color BrassLight,
    Color ParchmentDark,
    Color Parchment,
    Color Ink);

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
            new UiControlTokens(
                MinimumHeight: Math.Clamp((int)MathF.Round(28f * Math.Clamp(textScale, 0.85f, 1.25f)), 24, 34),
                CompactHeight: Math.Clamp((int)MathF.Round(24f * Math.Clamp(textScale, 0.85f, 1.2f)), 21, 29),
                Gap: 4,
                LayerInset: 2,
                SliderTrackHeight: quality == 0 ? 6 : 8,
                SliderThumbWidth: ui.LargeCursor ? 13 : 9,
                ToggleWidth: ui.LargeCursor ? 80 : 72,
                IconBoxSize: ui.LargeCursor ? 24 : 22,
                FocusRingOffset: accessibility.HighContrastInteractionOutline ? 3 : 2),
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
                new Color(20, 12, 12),
                new Color(31, 23, 22),
                new Color(52, 36, 30),
                new Color(72, 48, 36),
                new Color(242, 164, 83),
                new Color(160, 86, 58),
                new Color(248, 241, 225),
                new Color(196, 176, 151),
                new Color(255, 216, 112),
                new Color(219, 86, 72)),
            "FOREST" => new UiPalette(
                new Color(8, 17, 15),
                new Color(17, 29, 24),
                new Color(29, 48, 37),
                new Color(42, 68, 49),
                new Color(117, 203, 126),
                new Color(65, 130, 87),
                new Color(235, 245, 226),
                new Color(167, 194, 161),
                new Color(242, 211, 103),
                new Color(210, 82, 78)),
            _ => new UiPalette(
                new Color(7, 13, 21),
                new Color(18, 27, 34),
                new Color(30, 44, 49),
                new Color(43, 65, 66),
                new Color(99, 205, 163),
                new Color(48, 126, 111),
                new Color(246, 242, 226),
                new Color(174, 188, 180),
                new Color(255, 198, 82),
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

    public static UiMaterialPalette ResolveMaterials(UiPalette palette)
    {
        return new UiMaterialPalette(
            FrameShadow: new Color(12, 9, 8),
            WoodDark: Color.Lerp(new Color(42, 27, 20), palette.Backdrop, 0.14f),
            Wood: Color.Lerp(new Color(78, 50, 33), palette.SurfaceRaised, 0.14f),
            WoodLight: Color.Lerp(new Color(126, 82, 45), palette.Warning, 0.09f),
            BrassDark: new Color(91, 63, 31),
            Brass: Color.Lerp(new Color(177, 126, 52), palette.Warning, 0.24f),
            BrassLight: Color.Lerp(new Color(238, 197, 102), palette.Warning, 0.20f),
            ParchmentDark: new Color(111, 82, 52),
            Parchment: new Color(203, 174, 122),
            Ink: new Color(45, 33, 25));
    }

    public static Color WithAlpha(Color color, float alpha)
    {
        var clamped = Math.Clamp(alpha, 0f, 1f);
        return new Color(
            (byte)Math.Clamp((int)MathF.Round(color.R * clamped), 0, 255),
            (byte)Math.Clamp((int)MathF.Round(color.G * clamped), 0, 255),
            (byte)Math.Clamp((int)MathF.Round(color.B * clamped), 0, 255),
            (byte)Math.Clamp((int)MathF.Round(255f * clamped), 0, 255));
    }

    public static float ContrastRatio(Color foreground, Color background)
    {
        var lighter = Math.Max(RelativeLuminance(foreground), RelativeLuminance(background));
        var darker = Math.Min(RelativeLuminance(foreground), RelativeLuminance(background));
        return (lighter + 0.05f) / (darker + 0.05f);
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

        var borderWidth = Math.Clamp(context.ViewportBounds.Width / 80, 6, 24);
        var borderHeight = Math.Clamp(context.ViewportBounds.Height / 60, 6, 18);
        context.SpriteBatch.Draw(
            context.Pixel,
            new Rectangle(0, 0, borderWidth, context.ViewportBounds.Height),
            WithAlpha(palette.Backdrop, 0.18f * tint));
        context.SpriteBatch.Draw(
            context.Pixel,
            new Rectangle(context.ViewportBounds.Right - borderWidth, 0, borderWidth, context.ViewportBounds.Height),
            WithAlpha(palette.Backdrop, 0.18f * tint));
        context.SpriteBatch.Draw(
            context.Pixel,
            new Rectangle(0, 0, context.ViewportBounds.Width, borderHeight),
            WithAlpha(palette.Backdrop, 0.14f * tint));

        var motionTime = settings?.Ui.ReducedMotion == false ? context.Time.TotalSeconds : 0d;
        for (var index = 0; index < 8; index++)
        {
            var x = context.ViewportBounds.Width * (index + 1) / 9;
            var baseY = context.ViewportBounds.Height * (23 + index * 7 % 51) / 100;
            var drift = (int)MathF.Round(MathF.Sin((float)motionTime * 0.7f + index * 1.3f) * 4f);
            context.SpriteBatch.Draw(
                context.Pixel,
                new Rectangle(x + drift, baseY, 2, 2),
                WithAlpha(index % 3 == 0 ? palette.Warning : palette.Accent, 0.14f * tint));
        }
    }

    public static void DrawPanel(RenderContext context, Rectangle bounds, UiPalette palette, float opacity, bool raised = true, GameSettings? settings = null)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        var spec = ResolveContract(settings).Panel;
        var materials = ResolveMaterials(palette);
        var shadow = new Rectangle(
            bounds.X + spec.ShadowOffset.X,
            bounds.Y + spec.ShadowOffset.Y,
            bounds.Width,
            bounds.Height);
        DrawRoundedRectangle(context, shadow, WithAlpha(materials.FrameShadow, spec.ShadowOpacity * opacity), spec.CornerRadius);

        if (raised && spec.GlowSpread > 0 && spec.GlowOpacity > 0f)
        {
            var glow = Inflate(bounds, spec.GlowSpread);
            DrawRoundedBorder(context, glow, WithAlpha(palette.Accent, spec.GlowOpacity * opacity), spec.CornerRadius + spec.GlowSpread, 1);
        }

        var top = raised
            ? Color.Lerp(Lighten(palette.SurfaceRaised, 0.06f), materials.Wood, 0.20f)
            : Color.Lerp(Lighten(palette.Surface, 0.03f), materials.WoodDark, 0.10f);
        var bottom = raised
            ? Color.Lerp(Darken(palette.SurfaceRaised, 0.13f), materials.WoodDark, 0.22f)
            : Color.Lerp(Darken(palette.Surface, 0.10f), materials.FrameShadow, 0.12f);
        DrawRoundedGradient(context, bounds, WithAlpha(top, opacity), WithAlpha(bottom, opacity), spec.CornerRadius, spec.GradientSteps);
        DrawRoundedBorder(context, bounds, WithAlpha(materials.FrameShadow, 0.98f * opacity), spec.CornerRadius, Math.Max(2, spec.BorderThickness));
        DrawRoundedBorder(context, Inflate(bounds, -2), WithAlpha(materials.BrassDark, 0.90f * opacity), Math.Max(1, spec.CornerRadius - 2), 1);
        DrawAdventureFrame(context, bounds, materials, opacity, raised);
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
        var materials = ResolveMaterials(palette);
        var activePressed = enabled && pressed;
        var fill = selected
            ? Color.Lerp(materials.Wood, palette.SurfaceHover, 0.36f)
            : hovered
                ? Color.Lerp(materials.WoodDark, palette.SurfaceRaised, 0.52f)
                : Color.Lerp(materials.WoodDark, palette.Surface, 0.64f);
        if (activePressed)
        {
            fill = Darken(fill, 0.18f);
        }

        var alpha = enabled ? 0.97f : 0.48f;
        if (enabled && !activePressed && (hovered || selected))
        {
            var shadow = new Rectangle(bounds.X + spec.ShadowOffset.X, bounds.Y + spec.ShadowOffset.Y, bounds.Width, bounds.Height);
            DrawRoundedRectangle(context, shadow, WithAlpha(materials.FrameShadow, spec.ShadowOpacity), spec.CornerRadius);
        }

        DrawRoundedGradient(
            context,
            bounds,
            WithAlpha(Lighten(fill, activePressed ? 0.01f : 0.10f), alpha),
            WithAlpha(Darken(fill, activePressed ? 0.18f : 0.10f), alpha),
            spec.CornerRadius,
            spec.GradientSteps);

        var border = !enabled
            ? materials.Wood
            : selected
                ? materials.BrassLight
                : hovered
                    ? materials.Brass
                    : materials.WoodLight;
        DrawRoundedBorder(context, bounds, WithAlpha(materials.FrameShadow, enabled ? 0.96f : 0.42f), spec.CornerRadius, selected ? 2 : 1);
        DrawRoundedBorder(context, Inflate(bounds, -1), WithAlpha(border, enabled ? 0.88f : 0.35f), Math.Max(1, spec.CornerRadius - 1), 1);

        if (bounds.Width >= 28 && bounds.Height >= 16)
        {
            context.SpriteBatch.Draw(
                context.Pixel,
                new Rectangle(bounds.X + 6, bounds.Y + 3, Math.Max(1, bounds.Width - 12), 1),
                WithAlpha(materials.WoodLight, alpha * 0.46f));
            context.SpriteBatch.Draw(
                context.Pixel,
                new Rectangle(bounds.X + 7, bounds.Bottom - 4, Math.Max(1, bounds.Width - 14), 1),
                WithAlpha(materials.FrameShadow, alpha * 0.52f));
        }

        if (enabled && (selected || hovered) && bounds.Width >= 36)
        {
            var markerColor = selected ? materials.BrassLight : palette.Accent;
            var markerHeight = Math.Max(8, bounds.Height - 12);
            context.SpriteBatch.Draw(
                context.Pixel,
                new Rectangle(bounds.X + (activePressed ? 4 : 3), bounds.Center.Y - markerHeight / 2, selected ? 3 : 2, markerHeight),
                WithAlpha(markerColor, selected ? 0.96f : 0.72f));
        }

        if (activePressed)
        {
            DrawRoundedBorder(context, Inflate(bounds, -2), WithAlpha(Color.Black, 0.30f), Math.Max(1, spec.CornerRadius - 2), 1);
        }

        if (focused && enabled)
        {
            DrawFocusFrame(context, Inflate(bounds, 2), palette, spec.CornerRadius + 2, settings);
        }
    }

    public static void DrawSlot(RenderContext context, Rectangle bounds, UiPalette palette, bool selected, bool hovered, float opacity = 0.94f)
    {
        var materials = ResolveMaterials(palette);
        var inset = Inflate(bounds, -3);
        DrawRoundedRectangle(context, bounds, WithAlpha(materials.FrameShadow, 0.94f * opacity), 4);
        DrawRoundedRectangle(context, Inflate(bounds, -1), WithAlpha(selected ? materials.Brass : materials.Wood, (selected ? 0.92f : 0.76f) * opacity), 3);
        DrawRoundedRectangle(context, inset, WithAlpha(hovered ? palette.SurfaceHover : palette.Backdrop, 0.96f * opacity), 2);
        DrawRoundedBorder(context, bounds, WithAlpha(selected ? materials.BrassLight : hovered ? palette.Accent : materials.WoodLight, selected ? 1f : 0.74f), 4, selected ? 2 : 1);
        if (bounds.Width >= 18 && bounds.Height >= 18)
        {
            context.SpriteBatch.Draw(context.Pixel, new Rectangle(inset.X + 2, inset.Y + 2, Math.Max(2, inset.Width - 4), 1), WithAlpha(Color.White, 0.09f * opacity));
            context.SpriteBatch.Draw(context.Pixel, new Rectangle(inset.X + 2, inset.Bottom - 3, Math.Max(2, inset.Width - 4), 1), WithAlpha(Color.Black, 0.28f * opacity));
            if (selected)
            {
                context.SpriteBatch.Draw(context.Pixel, new Rectangle(bounds.Right - 7, bounds.Y + 3, 3, 3), WithAlpha(materials.BrassLight, opacity));
            }
        }
    }

    public static void DrawHeader(RenderContext context, Rectangle bounds, UiPalette palette, float opacity = 0.92f, GameSettings? settings = null)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        var contract = ResolveContract(settings);
        var materials = ResolveMaterials(palette);
        DrawRoundedGradient(
            context,
            bounds,
            WithAlpha(materials.WoodLight, opacity),
            WithAlpha(materials.WoodDark, opacity),
            Math.Min(contract.Panel.CornerRadius, 8),
            Math.Max(1, contract.Panel.GradientSteps / 2));
        DrawRoundedBorder(context, bounds, WithAlpha(materials.FrameShadow, 0.96f * opacity), Math.Min(contract.Panel.CornerRadius, 8), 2);
        DrawRoundedBorder(context, Inflate(bounds, -2), WithAlpha(materials.BrassDark, 0.80f * opacity), Math.Max(1, Math.Min(contract.Panel.CornerRadius, 8) - 2), 1);
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(bounds.X + 8, bounds.Bottom - 4, Math.Max(0, bounds.Width - 16), 1), WithAlpha(materials.BrassLight, 0.50f * opacity));
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(bounds.X + 6, bounds.Bottom - 3, Math.Max(0, bounds.Width - 12), 2), WithAlpha(palette.Accent, 0.72f * opacity));
        DrawHeaderCarving(context, bounds, materials, opacity);
    }

    public static void DrawProgressBar(RenderContext context, Rectangle bounds, float progress, UiPalette palette)
    {
        progress = Math.Clamp(progress, 0f, 1f);
        DrawRoundedRectangle(context, bounds, WithAlpha(palette.SurfaceRaised, 0.86f), 4);
        var fill = new Rectangle(bounds.X, bounds.Y, (int)MathF.Round(bounds.Width * progress), bounds.Height);
        DrawRoundedGradient(context, fill, WithAlpha(Lighten(palette.Accent, 0.10f), 0.96f), WithAlpha(Darken(palette.Accent, 0.13f), 0.96f), 4, 4);
        DrawRoundedBorder(context, bounds, WithAlpha(palette.AccentSoft, 0.8f), 4, 1);
        for (var x = bounds.X + 12; x < fill.Right; x += 12)
        {
            context.SpriteBatch.Draw(context.Pixel, new Rectangle(x, bounds.Y + 2, 1, Math.Max(1, bounds.Height - 4)), WithAlpha(palette.Backdrop, 0.18f));
        }
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

    private static void DrawAdventureFrame(
        RenderContext context,
        Rectangle bounds,
        UiMaterialPalette materials,
        float opacity,
        bool raised)
    {
        if (bounds.Width < 36 || bounds.Height < 24)
        {
            return;
        }

        var rail = Math.Clamp(Math.Min(bounds.Width, bounds.Height) / 18, 3, 7);
        var topRail = new Rectangle(bounds.X + 3, bounds.Y + 3, Math.Max(1, bounds.Width - 6), rail);
        var bottomRail = new Rectangle(bounds.X + 3, bounds.Bottom - rail - 3, Math.Max(1, bounds.Width - 6), rail);
        context.SpriteBatch.Draw(context.Pixel, topRail, WithAlpha(materials.Wood, opacity * 0.92f));
        context.SpriteBatch.Draw(context.Pixel, bottomRail, WithAlpha(materials.WoodDark, opacity * 0.94f));
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(topRail.X + 3, topRail.Y + 1, Math.Max(1, topRail.Width - 6), 1), WithAlpha(materials.WoodLight, opacity * 0.56f));
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(bottomRail.X + 3, bottomRail.Bottom - 2, Math.Max(1, bottomRail.Width - 6), 1), WithAlpha(materials.FrameShadow, opacity * 0.70f));

        if (bounds.Height >= 48)
        {
            context.SpriteBatch.Draw(context.Pixel, new Rectangle(bounds.X + 3, bounds.Y + rail, rail, Math.Max(1, bounds.Height - rail * 2)), WithAlpha(materials.WoodDark, opacity * 0.90f));
            context.SpriteBatch.Draw(context.Pixel, new Rectangle(bounds.Right - rail - 3, bounds.Y + rail, rail, Math.Max(1, bounds.Height - rail * 2)), WithAlpha(materials.Wood, opacity * 0.90f));
        }

        for (var index = 0; index < 3; index++)
        {
            var segmentWidth = Math.Max(4, bounds.Width / 9);
            var x = bounds.X + 10 + index * Math.Max(12, (bounds.Width - 24) / 3);
            var width = Math.Min(segmentWidth, Math.Max(1, bounds.Right - x - 8));
            if (width > 0)
            {
                context.SpriteBatch.Draw(context.Pixel, new Rectangle(x, bounds.Y + 5 + index % 2, width, 1), WithAlpha(materials.WoodLight, opacity * 0.28f));
                context.SpriteBatch.Draw(context.Pixel, new Rectangle(x + 3, bounds.Bottom - 6 - index % 2, Math.Max(1, width - 3), 1), WithAlpha(materials.FrameShadow, opacity * 0.42f));
            }
        }

        var fitting = WithAlpha(raised ? materials.Brass : materials.BrassDark, opacity * 0.92f);
        var fittingLight = WithAlpha(materials.BrassLight, opacity * 0.82f);
        DrawCornerFitting(context, bounds.X + 5, bounds.Y + 5, 1, 1, fitting, fittingLight);
        DrawCornerFitting(context, bounds.Right - 6, bounds.Y + 5, -1, 1, fitting, fittingLight);
        DrawCornerFitting(context, bounds.X + 5, bounds.Bottom - 6, 1, -1, fitting, fittingLight);
        DrawCornerFitting(context, bounds.Right - 6, bounds.Bottom - 6, -1, -1, fitting, fittingLight);
    }

    private static void DrawCornerFitting(
        RenderContext context,
        int x,
        int y,
        int horizontal,
        int vertical,
        Color color,
        Color highlight)
    {
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(horizontal > 0 ? x : x - 9, y, 10, 2), color);
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(x, vertical > 0 ? y : y - 9, 2, 10), color);
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(x, y, 2, 2), highlight);
    }

    private static void DrawHeaderCarving(
        RenderContext context,
        Rectangle bounds,
        UiMaterialPalette materials,
        float opacity)
    {
        if (bounds.Width < 86 || bounds.Height < 18)
        {
            return;
        }

        var center = bounds.Center.X;
        var y = bounds.Y + Math.Max(5, bounds.Height / 3);
        var color = WithAlpha(materials.Brass, opacity * 0.78f);
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(bounds.X + 12, y, Math.Max(8, bounds.Width / 9), 2), color);
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(bounds.Right - 12 - Math.Max(8, bounds.Width / 9), y, Math.Max(8, bounds.Width / 9), 2), color);
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(center - 4, bounds.Y + 4, 8, 2), color);
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(center - 2, bounds.Y + 2, 4, 6), WithAlpha(materials.BrassLight, opacity * 0.66f));
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

    private static float RelativeLuminance(Color color)
    {
        static float Channel(byte value)
        {
            var normalized = value / 255f;
            return normalized <= 0.04045f
                ? normalized / 12.92f
                : MathF.Pow((normalized + 0.055f) / 1.055f, 2.4f);
        }

        return Channel(color.R) * 0.2126f + Channel(color.G) * 0.7152f + Channel(color.B) * 0.0722f;
    }
}
