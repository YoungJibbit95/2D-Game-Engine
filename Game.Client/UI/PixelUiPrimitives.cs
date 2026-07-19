using Game.Client.Rendering;
using Game.Core.Settings;
using Microsoft.Xna.Framework;

namespace Game.Client.UI;

public readonly record struct PixelUiState(
    bool Hovered = false,
    bool Pressed = false,
    bool Focused = false,
    bool Selected = false,
    bool Enabled = true);

public sealed class PixelUiMotionState
{
    public float Hover { get; private set; }

    public float Press { get; private set; }

    public float Focus { get; private set; }

    public float Selection { get; private set; }

    public float Emphasis => Math.Clamp(Hover * 0.4f + Focus * 0.75f + Selection * 0.25f - Press * 0.08f, 0f, 1f);

    public void Update(PixelUiState state, float deltaSeconds, float animationSpeed = 1f, bool reducedMotion = false)
    {
        if (reducedMotion)
        {
            Snap(state);
            return;
        }

        var elapsed = Math.Clamp(deltaSeconds, 0f, 0.25f) * Math.Clamp(animationSpeed, 0.1f, 4f);
        Hover = MoveTowards(Hover, Target(state.Hovered && state.Enabled), elapsed / 0.09f);
        Press = MoveTowards(Press, Target(state.Pressed && state.Enabled), elapsed / 0.055f);
        Focus = MoveTowards(Focus, Target(state.Focused && state.Enabled), elapsed / 0.14f);
        Selection = MoveTowards(Selection, Target(state.Selected && state.Enabled), elapsed / 0.12f);
    }

    public void Snap(PixelUiState state)
    {
        Hover = Target(state.Hovered && state.Enabled);
        Press = Target(state.Pressed && state.Enabled);
        Focus = Target(state.Focused && state.Enabled);
        Selection = Target(state.Selected && state.Enabled);
    }

    public void Reset()
    {
        Hover = 0f;
        Press = 0f;
        Focus = 0f;
        Selection = 0f;
    }

    private static float MoveTowards(float current, float target, float maximumDelta)
    {
        return current < target
            ? Math.Min(target, current + maximumDelta)
            : Math.Max(target, current - maximumDelta);
    }

    private static float Target(bool active)
    {
        return active ? 1f : 0f;
    }
}

public readonly record struct PixelUiToggleGeometry(
    Rectangle Bounds,
    Rectangle ValueBounds,
    Rectangle Track,
    Rectangle KnobOff,
    Rectangle KnobOn,
    Rectangle HitBounds)
{
    public Rectangle KnobAt(float transition)
    {
        var amount = Math.Clamp(transition, 0f, 1f);
        return new Rectangle(
            (int)MathF.Round(MathHelper.Lerp(KnobOff.X, KnobOn.X, amount)),
            KnobOff.Y,
            KnobOff.Width,
            KnobOff.Height);
    }
}

public readonly record struct PixelUiSliderGeometry(
    Rectangle Bounds,
    Rectangle Track,
    Rectangle Fill,
    Rectangle Thumb,
    Rectangle ValueBounds,
    Rectangle HitBounds);

public readonly record struct PixelUiStepperGeometry(
    Rectangle Bounds,
    Rectangle Decrement,
    Rectangle Value,
    Rectangle Increment);

public readonly record struct PixelUiCommandGeometry(
    Rectangle Bounds,
    Rectangle Icon,
    Rectangle Text,
    Rectangle HitBounds);

public static class PixelUiGeometry
{
    public static PixelUiToggleGeometry Toggle(Rectangle bounds, UiControlTokens tokens)
    {
        var height = Math.Max(1, Math.Min(bounds.Height, tokens.MinimumHeight));
        var width = Math.Max(height * 2, Math.Min(bounds.Width, tokens.ToggleWidth));
        var track = new Rectangle(bounds.Right - width, bounds.Center.Y - height / 2, width, height);
        var knobSize = Math.Max(8, height - 8);
        var knobY = track.Center.Y - knobSize / 2;
        var knobOff = new Rectangle(track.X + 4, knobY, knobSize, knobSize);
        var knobOn = new Rectangle(track.Right - knobSize - 4, knobY, knobSize, knobSize);
        var valueWidth = Math.Max(0, track.X - bounds.X - tokens.Gap);
        var valueBounds = new Rectangle(bounds.X, bounds.Y, valueWidth, bounds.Height);
        return new PixelUiToggleGeometry(bounds, valueBounds, track, knobOff, knobOn, track);
    }

    public static PixelUiSliderGeometry Slider(Rectangle bounds, float normalized, UiControlTokens tokens)
    {
        var amount = Math.Clamp(normalized, 0f, 1f);
        var valueWidth = Math.Clamp(bounds.Width / 4, 50, 72);
        var trackWidth = Math.Max(1, bounds.Width - valueWidth - tokens.Gap * 2);
        var track = new Rectangle(
            bounds.X,
            bounds.Center.Y - tokens.SliderTrackHeight / 2,
            trackWidth,
            tokens.SliderTrackHeight);
        var fillWidth = (int)MathF.Round(track.Width * amount);
        var fill = new Rectangle(track.X, track.Y, fillWidth, track.Height);
        var thumbWidth = Math.Min(tokens.SliderThumbWidth, Math.Max(1, track.Width));
        var thumbHeight = Math.Min(bounds.Height, Math.Max(track.Height + 8, tokens.CompactHeight - 8));
        var thumbCenter = track.X + (int)MathF.Round((track.Width - 1) * amount);
        var thumb = new Rectangle(
            Math.Clamp(thumbCenter - thumbWidth / 2, track.X, Math.Max(track.X, track.Right - thumbWidth)),
            bounds.Center.Y - thumbHeight / 2,
            thumbWidth,
            thumbHeight);
        var value = new Rectangle(track.Right + tokens.Gap, bounds.Y, bounds.Right - track.Right - tokens.Gap, bounds.Height);
        var hit = new Rectangle(track.X, bounds.Y, track.Width, bounds.Height);
        return new PixelUiSliderGeometry(bounds, track, fill, thumb, value, hit);
    }

    public static PixelUiStepperGeometry Stepper(Rectangle bounds, UiControlTokens tokens)
    {
        var gap = tokens.Gap;
        var buttonWidth = Math.Clamp(bounds.Height, 24, 34);
        var valueWidth = Math.Min(112, Math.Max(48, bounds.Width - buttonWidth * 2 - gap * 2));
        var totalWidth = buttonWidth * 2 + valueWidth + gap * 2;
        var x = bounds.Right - Math.Min(bounds.Width, totalWidth);
        var decrement = new Rectangle(x, bounds.Y, buttonWidth, bounds.Height);
        var value = new Rectangle(decrement.Right + gap, bounds.Y, valueWidth, bounds.Height);
        var increment = new Rectangle(value.Right + gap, bounds.Y, buttonWidth, bounds.Height);
        return new PixelUiStepperGeometry(bounds, decrement, value, increment);
    }

    public static Rectangle Segment(Rectangle bounds, int index, int count, UiControlTokens tokens)
    {
        if (count <= 0 || index < 0 || index >= count)
        {
            return Rectangle.Empty;
        }

        var totalGap = tokens.Gap * (count - 1);
        var available = Math.Max(0, bounds.Width - totalGap);
        var start = bounds.X + available * index / count + tokens.Gap * index;
        var end = bounds.X + available * (index + 1) / count + tokens.Gap * index;
        return new Rectangle(start, bounds.Y, Math.Max(0, end - start), bounds.Height);
    }

    public static PixelUiCommandGeometry Command(Rectangle bounds, UiControlTokens tokens)
    {
        var iconSize = Math.Min(tokens.IconBoxSize, Math.Max(1, bounds.Height - 6));
        var icon = new Rectangle(bounds.X + 4, bounds.Center.Y - iconSize / 2, iconSize, iconSize);
        var textX = icon.Right + tokens.Gap;
        var text = new Rectangle(textX, bounds.Y, Math.Max(0, bounds.Right - textX - 6), bounds.Height);
        return new PixelUiCommandGeometry(bounds, icon, text, bounds);
    }
}

public static class PixelUiInteraction
{
    public static float SliderNormalizedAt(Rectangle hitBounds, int pointerX)
    {
        if (hitBounds.Width <= 1)
        {
            return 0f;
        }

        return Math.Clamp((pointerX - hitBounds.X) / (float)(hitBounds.Width - 1), 0f, 1f);
    }

    public static bool ResolveToggle(bool currentValue, bool activated, bool enabled = true)
    {
        return activated && enabled ? !currentValue : currentValue;
    }
}

public enum PixelUiCommandIcon
{
    Command,
    Save,
    Reset,
    Keyboard
}

public static class PixelUiPrimitives
{
    public static void DrawGlassSurface(
        RenderContext context,
        Rectangle bounds,
        UiPalette palette,
        float opacity,
        GameSettings settings,
        bool raised = true)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        var contract = UiTheme.ResolveContract(settings);
        UiTheme.DrawPanel(context, bounds, palette, opacity, raised, settings);
        var inset = Math.Min(8, Math.Max(2, contract.Spacing.Sm));
        var highlight = new Rectangle(
            bounds.X + inset,
            bounds.Y + 3,
            Math.Max(0, bounds.Width - inset * 2),
            1);
        context.SpriteBatch.Draw(context.Pixel, highlight, UiTheme.WithAlpha(Color.White, opacity * 0.13f));

        if (contract.Panel.GlowOpacity > 0f)
        {
            var accent = new Rectangle(bounds.X + inset, bounds.Bottom - 3, Math.Max(0, bounds.Width / 3), 1);
            context.SpriteBatch.Draw(
                context.Pixel,
                accent,
                UiTheme.WithAlpha(palette.Accent, opacity * (0.25f + contract.Panel.GlowOpacity)));
        }
    }

    public static void DrawMeter(
        RenderContext context,
        Rectangle bounds,
        float normalized,
        UiPalette palette,
        Color fillColor,
        float opacity,
        GameSettings settings,
        bool segmented = false,
        bool emphasized = false)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        var amount = float.IsFinite(normalized) ? Math.Clamp(normalized, 0f, 1f) : 0f;
        var radius = Math.Min(UiTheme.ResolveContract(settings).Button.CornerRadius, Math.Max(1, bounds.Height / 2));
        UiTheme.DrawRoundedRectangle(context, bounds, UiTheme.WithAlpha(palette.Backdrop, opacity * 0.82f), radius);
        var fillWidth = (int)MathF.Round(bounds.Width * amount);
        if (fillWidth > 0)
        {
            var fill = new Rectangle(bounds.X, bounds.Y, fillWidth, bounds.Height);
            UiTheme.DrawRoundedRectangle(context, fill, UiTheme.WithAlpha(fillColor, opacity), Math.Min(radius, fillWidth / 2));
            if (fill.Height >= 4)
            {
                var shine = new Rectangle(fill.X + Math.Min(2, fill.Width), fill.Y + 2, Math.Max(0, fill.Width - 4), 1);
                context.SpriteBatch.Draw(context.Pixel, shine, UiTheme.WithAlpha(Color.White, opacity * 0.18f));
            }
        }

        if (segmented && bounds.Width >= 40)
        {
            const int segmentCount = 10;
            for (var segment = 1; segment < segmentCount; segment++)
            {
                var x = bounds.X + bounds.Width * segment / segmentCount;
                context.SpriteBatch.Draw(
                    context.Pixel,
                    new Rectangle(x, bounds.Y + 2, 1, Math.Max(0, bounds.Height - 4)),
                    UiTheme.WithAlpha(palette.Backdrop, opacity * 0.38f));
            }
        }

        UiTheme.DrawRoundedBorder(
            context,
            bounds,
            UiTheme.WithAlpha(emphasized ? palette.Warning : palette.AccentSoft, opacity * 0.88f),
            radius,
            emphasized || settings.Accessibility.HighContrastInteractionOutline ? 2 : 1);
    }

    public static void DrawStatusChip(
        RenderContext context,
        Rectangle bounds,
        UiPalette palette,
        Color accent,
        float opacity,
        GameSettings settings)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        var radius = Math.Min(UiTheme.ResolveContract(settings).Button.CornerRadius, Math.Max(1, bounds.Height / 2));
        UiTheme.DrawRoundedRectangle(context, bounds, UiTheme.WithAlpha(palette.SurfaceRaised, opacity), radius);
        UiTheme.DrawRoundedBorder(context, bounds, UiTheme.WithAlpha(accent, opacity * 0.76f), radius, 1);
        context.SpriteBatch.Draw(
            context.Pixel,
            new Rectangle(bounds.X + 4, bounds.Center.Y - 1, Math.Min(10, Math.Max(0, bounds.Width - 8)), 2),
            UiTheme.WithAlpha(accent, opacity));
    }

    public static void DrawToggle(
        RenderContext context,
        Rectangle bounds,
        string value,
        bool isOn,
        UiPalette palette,
        PixelUiState state,
        PixelUiMotionState motion,
        UiTypographyTokens typography,
        GameSettings settings)
    {
        var tokens = UiTheme.ResolveContract(settings).Controls;
        var geometry = PixelUiGeometry.Toggle(bounds, tokens);
        DrawChrome(context, geometry.Track, palette, state with { Selected = isOn }, motion, settings);
        var knob = geometry.KnobAt(settings.Ui.ReducedMotion ? (isOn ? 1f : 0f) : motion.Selection);
        UiTheme.DrawRoundedRectangle(context, knob, isOn ? palette.Warning : palette.TextMuted, Math.Min(4, knob.Height / 2));
        DrawRightAlignedText(context, geometry.ValueBounds, value, isOn ? palette.Text : palette.TextMuted, typography.CaptionScale, state.Pressed ? 1 : 0);
    }

    public static void DrawSlider(
        RenderContext context,
        Rectangle bounds,
        string value,
        float normalized,
        UiPalette palette,
        PixelUiState state,
        PixelUiMotionState motion,
        UiTypographyTokens typography,
        GameSettings settings)
    {
        var contract = UiTheme.ResolveContract(settings);
        var geometry = PixelUiGeometry.Slider(bounds, normalized, contract.Controls);
        DrawGlow(context, geometry.Track, palette, motion.Emphasis, contract.Button.CornerRadius);
        UiTheme.DrawRoundedRectangle(context, geometry.Track, UiTheme.WithAlpha(palette.Backdrop, state.Enabled ? 0.9f : 0.48f), contract.Controls.SliderTrackHeight / 2);
        UiTheme.DrawRoundedRectangle(context, geometry.Fill, state.Pressed ? palette.Warning : palette.Accent, contract.Controls.SliderTrackHeight / 2);
        UiTheme.DrawRoundedBorder(context, geometry.Track, UiTheme.WithAlpha(palette.SurfaceHover, 0.9f), contract.Controls.SliderTrackHeight / 2, 1);
        UiTheme.DrawRoundedRectangle(context, geometry.Thumb, state.Pressed ? palette.Text : palette.Warning, Math.Min(4, geometry.Thumb.Width / 2));
        UiTheme.DrawRoundedBorder(context, geometry.Thumb, UiTheme.WithAlpha(palette.Backdrop, 0.82f), Math.Min(4, geometry.Thumb.Width / 2), 1);
        if (state.Focused)
        {
            UiTheme.DrawFocusFrame(context, Inflate(geometry.HitBounds, contract.Controls.FocusRingOffset), palette, contract.Button.CornerRadius, settings);
        }

        DrawRightAlignedText(context, geometry.ValueBounds, value, palette.Text, typography.CaptionScale, state.Pressed ? 1 : 0);
    }

    public static void DrawStepper(
        RenderContext context,
        Rectangle bounds,
        string value,
        UiPalette palette,
        PixelUiState state,
        PixelUiMotionState motion,
        bool decrementHovered,
        bool decrementPressed,
        bool incrementHovered,
        bool incrementPressed,
        UiTypographyTokens typography,
        GameSettings settings)
    {
        var geometry = PixelUiGeometry.Stepper(bounds, UiTheme.ResolveContract(settings).Controls);
        DrawChrome(context, geometry.Decrement, palette, state with { Hovered = decrementHovered, Pressed = decrementPressed, Selected = false }, motion, settings);
        DrawChrome(context, geometry.Value, palette, state with { Hovered = false, Pressed = false, Selected = true }, motion, settings);
        DrawChrome(context, geometry.Increment, palette, state with { Hovered = incrementHovered, Pressed = incrementPressed, Selected = false }, motion, settings);
        DrawCenteredText(context, geometry.Decrement, "-", palette.Text, typography.CaptionScale, decrementPressed ? 1 : 0);
        DrawCenteredText(context, geometry.Value, value, palette.Text, typography.CaptionScale, 0);
        DrawCenteredText(context, geometry.Increment, "+", palette.Text, typography.CaptionScale, incrementPressed ? 1 : 0);
    }

    public static void DrawSegment(
        RenderContext context,
        Rectangle bounds,
        string label,
        UiPalette palette,
        PixelUiState state,
        PixelUiMotionState motion,
        UiTypographyTokens typography,
        GameSettings settings)
    {
        DrawChrome(context, bounds, palette, state, motion, settings);
        if (state.Selected)
        {
            var marker = new Rectangle(bounds.X + 5, bounds.Bottom - 3, Math.Max(0, bounds.Width - 10), 2);
            UiTheme.DrawRoundedRectangle(context, marker, palette.Accent, 1);
        }

        DrawCenteredText(context, bounds, label, state.Selected ? palette.Text : palette.TextMuted, typography.CaptionScale, state.Pressed ? 1 : 0);
    }

    public static void DrawCommand(
        RenderContext context,
        Rectangle bounds,
        string label,
        PixelUiCommandIcon icon,
        UiPalette palette,
        PixelUiState state,
        PixelUiMotionState motion,
        UiTypographyTokens typography,
        GameSettings settings)
    {
        var geometry = PixelUiGeometry.Command(bounds, UiTheme.ResolveContract(settings).Controls);
        DrawChrome(context, geometry.Bounds, palette, state, motion, settings);
        UiTheme.DrawRoundedRectangle(context, geometry.Icon, UiTheme.WithAlpha(state.Selected ? palette.AccentSoft : palette.Backdrop, 0.9f), 3);
        UiTheme.DrawRoundedBorder(context, geometry.Icon, UiTheme.WithAlpha(palette.Accent, 0.82f), 3, 1);
        DrawIcon(context, geometry.Icon, icon, state.Pressed ? palette.Text : palette.Warning);
        var maxCharacters = Math.Max(1, geometry.Text.Width / Math.Max(6, 6 * typography.CaptionScale));
        var visible = label.Length <= maxCharacters ? label : label[..Math.Max(1, maxCharacters - 1)] + ".";
        context.DebugText.Draw(
            new Vector2(geometry.Text.X, geometry.Text.Y + Math.Max(1, (geometry.Text.Height - 7 * typography.CaptionScale) / 2) + (state.Pressed ? 1 : 0)),
            visible,
            state.Enabled ? palette.Text : palette.TextMuted,
            typography.CaptionScale);
    }

    public static void DrawTooltip(
        RenderContext context,
        Rectangle bounds,
        UiPalette palette,
        float reveal,
        float opacity,
        GameSettings settings)
    {
        var amount = settings.Ui.ReducedMotion ? 1f : Math.Clamp(reveal, 0f, 1f);
        UiTheme.DrawTooltipSurface(context, bounds, palette, opacity * (0.72f + amount * 0.28f), settings);
        var accentWidth = (int)MathF.Round(Math.Max(0, bounds.Width - 16) * amount);
        context.SpriteBatch.Draw(
            context.Pixel,
            new Rectangle(bounds.X + 8, bounds.Bottom - 2, accentWidth, 1),
            UiTheme.WithAlpha(palette.Accent, 0.5f * amount));
    }

    private static void DrawChrome(
        RenderContext context,
        Rectangle bounds,
        UiPalette palette,
        PixelUiState state,
        PixelUiMotionState motion,
        GameSettings settings)
    {
        var contract = UiTheme.ResolveContract(settings);
        DrawGlow(context, bounds, palette, motion.Emphasis, contract.Button.CornerRadius);
        UiTheme.DrawButton(
            context,
            bounds,
            palette,
            state.Selected,
            state.Hovered,
            state.Enabled,
            state.Pressed,
            state.Focused,
            settings);
        var inner = Inflate(bounds, -2);
        UiTheme.DrawRoundedBorder(
            context,
            inner,
            UiTheme.WithAlpha(state.Selected ? palette.Accent : Color.White, state.Selected ? 0.3f : 0.08f + motion.Hover * 0.08f),
            Math.Max(1, contract.Button.CornerRadius - 2),
            1);
    }

    private static void DrawGlow(RenderContext context, Rectangle bounds, UiPalette palette, float emphasis, int cornerRadius)
    {
        if (emphasis <= 0f)
        {
            return;
        }

        UiTheme.DrawRoundedBorder(context, Inflate(bounds, 1), UiTheme.WithAlpha(palette.Accent, 0.12f + emphasis * 0.24f), cornerRadius + 1, 1);
    }

    private static void DrawIcon(RenderContext context, Rectangle bounds, PixelUiCommandIcon icon, Color color)
    {
        var x = bounds.X + Math.Max(2, (bounds.Width - 12) / 2);
        var y = bounds.Y + Math.Max(2, (bounds.Height - 12) / 2);
        switch (icon)
        {
            case PixelUiCommandIcon.Save:
                UiTheme.DrawRoundedBorder(context, new Rectangle(x, y, 12, 12), color, 2, 1);
                context.SpriteBatch.Draw(context.Pixel, new Rectangle(x + 3, y + 1, 6, 4), color);
                context.SpriteBatch.Draw(context.Pixel, new Rectangle(x + 3, y + 8, 6, 3), color);
                break;
            case PixelUiCommandIcon.Reset:
                context.SpriteBatch.Draw(context.Pixel, new Rectangle(x + 2, y + 2, 8, 2), color);
                context.SpriteBatch.Draw(context.Pixel, new Rectangle(x + 1, y + 3, 2, 6), color);
                context.SpriteBatch.Draw(context.Pixel, new Rectangle(x + 3, y + 9, 7, 2), color);
                context.SpriteBatch.Draw(context.Pixel, new Rectangle(x, y + 1, 4, 2), color);
                break;
            case PixelUiCommandIcon.Keyboard:
                UiTheme.DrawRoundedBorder(context, new Rectangle(x, y + 2, 12, 8), color, 2, 1);
                for (var column = 0; column < 3; column++)
                {
                    context.SpriteBatch.Draw(context.Pixel, new Rectangle(x + 2 + column * 3, y + 4, 2, 2), color);
                }

                context.SpriteBatch.Draw(context.Pixel, new Rectangle(x + 3, y + 7, 6, 1), color);
                break;
            default:
                context.SpriteBatch.Draw(context.Pixel, new Rectangle(x + 2, y + 2, 2, 2), color);
                context.SpriteBatch.Draw(context.Pixel, new Rectangle(x + 4, y + 4, 2, 2), color);
                context.SpriteBatch.Draw(context.Pixel, new Rectangle(x + 2, y + 6, 2, 2), color);
                context.SpriteBatch.Draw(context.Pixel, new Rectangle(x + 7, y + 7, 3, 2), color);
                break;
        }
    }

    private static void DrawCenteredText(RenderContext context, Rectangle bounds, string text, Color color, int scale, int offsetY)
    {
        var width = text.Length * 6 * scale;
        context.DebugText.Draw(
            new Vector2(bounds.Center.X - width / 2, bounds.Y + Math.Max(1, (bounds.Height - 7 * scale) / 2) + offsetY),
            text,
            color,
            scale);
    }

    private static void DrawRightAlignedText(RenderContext context, Rectangle bounds, string text, Color color, int scale, int offsetY)
    {
        var width = text.Length * 6 * scale;
        context.DebugText.Draw(
            new Vector2(Math.Max(bounds.X, bounds.Right - width), bounds.Y + Math.Max(1, (bounds.Height - 7 * scale) / 2) + offsetY),
            text,
            color,
            scale);
    }

    private static Rectangle Inflate(Rectangle bounds, int amount)
    {
        return new Rectangle(bounds.X - amount, bounds.Y - amount, bounds.Width + amount * 2, bounds.Height + amount * 2);
    }
}
