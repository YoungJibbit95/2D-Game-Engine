using Game.Client.Configuration;
using Game.Client.Input;
using Game.Client.Rendering;
using Game.Client.UI;
using Game.Core.Settings;
using Game.Core.UI.Animation;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Game.Client.GameStates;

public sealed class MainMenuState : IGameState
{
    internal const string BackgroundAssetPath = "assets/MainMenuAdventureV1/main_menu_adventure_v1.png";

    private readonly GameStateManager _states;
    private readonly Action _exit;
    private readonly InputManager _input = new();
    private readonly List<MenuItem> _items;
    private readonly List<UiHitRegion> _hitRegions = new();
    private readonly UiPointerRouter _pointer = new();
    private readonly UiGamepadNavigator _gamepad = new();
    private readonly UiAnimationPlayer _introAnimation = new();
    private GameSettings _settings = GameSettings.CreateDefault();
    private Texture2D? _backgroundTexture;
    private int _selectedIndex;
    private bool _focusVisible = true;

    public MainMenuState(GameStateManager states, Action exit)
    {
        _states = states;
        _exit = exit;
        _items =
        [
            new MenuItem("SINGLEPLAYER", "SELECT OR CREATE WORLD", true, StartSingleplayer),
            new MenuItem("COOP SPLITSCREEN", "PLANNED FOR CONSOLE SUPPORT", false, Noop),
            new MenuItem("MULTIPLAYER", "PLANNED SERVER CLIENT SUPPORT", false, Noop),
            new MenuItem("SETTINGS", "VIDEO AUDIO INPUT DEBUG", true, OpenSettings),
            new MenuItem("EXIT", "CLOSE GAME", true, _exit)
        ];
    }

    public string Name => "MainMenu";

    public void Initialize()
    {
        _settings = LoadSettings();
        _pointer.Reset();
        _gamepad.Reset();
        _focusVisible = true;
        var duration = _settings.Ui.ReducedMotion ? 0.001f : 0.28f;
        _introAnimation.Play(UiAnimationClip.SlideFadeIn(duration, -20f));
    }

    public void LoadContent(ContentManager content)
    {
        ArgumentNullException.ThrowIfNull(content);
        _backgroundTexture?.Dispose();
        _backgroundTexture = null;

        var graphicsService = content.ServiceProvider.GetService(typeof(IGraphicsDeviceService)) as IGraphicsDeviceService
            ?? throw new InvalidOperationException("MonoGame graphics device service is unavailable.");
        var backgroundPath = Path.Combine(
            ClientPaths.FindGameDataRoot(),
            BackgroundAssetPath.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(backgroundPath))
        {
            using var stream = File.OpenRead(backgroundPath);
            _backgroundTexture = Texture2D.FromStream(graphicsService.GraphicsDevice, stream);
        }
    }

    public void FixedUpdate(float fixedDeltaSeconds)
    {
    }

    public void Update(double deltaSeconds)
    {
        _input.Update();
        _gamepad.Update(deltaSeconds);
        _pointer.Update(_input.MousePosition, _input.IsLeftMouseDown, _hitRegions);
        _introAnimation.Update((float)deltaSeconds, _settings.Ui.AnimationSpeed);

        if (_pointer.HoveredId >= 0 &&
            _pointer.HoveredId < _items.Count &&
            _items[_pointer.HoveredId].Enabled)
        {
            _selectedIndex = _pointer.HoveredId;
            _focusVisible = false;
        }

        if (_input.IsKeyPressed(Keys.Down) || _input.IsKeyPressed(Keys.S) || _gamepad.DownPressed)
        {
            MoveSelection(1);
            _focusVisible = true;
        }
        else if (_input.IsKeyPressed(Keys.Up) || _input.IsKeyPressed(Keys.W) || _gamepad.UpPressed)
        {
            MoveSelection(-1);
            _focusVisible = true;
        }

        if (_pointer.ClickedId >= 0 && _pointer.ClickedId < _items.Count)
        {
            _selectedIndex = _pointer.ClickedId;
            _focusVisible = false;
            ActivateSelected();
        }
        else if (_input.IsKeyPressed(Keys.Enter) || _input.IsKeyPressed(Keys.Space) || _gamepad.ConfirmPressed)
        {
            _focusVisible = true;
            ActivateSelected();
        }

        // Escape is intentionally ignored here so a stray key press never closes the game from the main menu.
    }

    public void Draw(RenderContext context)
    {
        var palette = UiTheme.Resolve(_settings);
        var typography = UiTheme.ResolveContract(_settings).Typography;
        var fade = _introAnimation.GetValue(UiAnimationProperty.Opacity, 1f);
        var offsetY = (int)MathF.Round(_introAnimation.GetValue(UiAnimationProperty.OffsetY, 0f));
        DrawBackground(context, palette);
        var viewport = context.ViewportBounds;
        var compact = viewport.Height < 600 || viewport.Width < 860;
        var wide = viewport.Width >= 1100 && viewport.Height >= 620;
        var width = Math.Min(compact ? 420 : 370, Math.Max(260, viewport.Width - (compact ? 40 : 96)));
        var x = wide ? viewport.X + Math.Clamp(viewport.Width / 13, 54, 164) : viewport.Center.X - width / 2;
        var titleScale = compact
            ? Math.Min(4, typography.DisplayScale)
            : Math.Clamp(typography.DisplayScale + 1, 4, 7);
        var titleWidth = PixelTextWidth("YJSE", titleScale);
        var titleY = (compact ? 20 : 46) + offsetY;
        var titlePlateWidth = Math.Min(viewport.Width - 28, titleWidth + (compact ? 68 : 112));
        var titlePlate = new Rectangle(
            viewport.Center.X - titlePlateWidth / 2,
            titleY - (compact ? 9 : 16),
            titlePlateWidth,
            titleScale * 7 + (compact ? 46 : 58));
        DrawAdventureTitlePlate(context, titlePlate, palette, fade, compact);

        var titleX = viewport.Center.X - titleWidth / 2;
        context.DebugText.Draw(new Vector2(titleX + 4, titleY + 5), "YJSE", UiTheme.WithAlpha(palette.Backdrop, 0.78f * fade), titleScale);
        context.DebugText.Draw(new Vector2(titleX, titleY), "YJSE", UiTheme.WithAlpha(palette.Accent, fade), titleScale);
        var subtitle = "LIVING WORLDS  YOUR RULES";
        var subtitleScale = compact ? 1 : Math.Min(2, typography.BodyScale);
        var subtitleX = viewport.Center.X - PixelTextWidth(subtitle, subtitleScale) / 2;
        context.DebugText.Draw(
            new Vector2(subtitleX, titleY + titleScale * 7 + (compact ? 9 : 13)),
            subtitle,
            UiTheme.WithAlpha(palette.Text, fade),
            subtitleScale);

        _hitRegions.Clear();
        var startY = Math.Max(compact ? 106 : 214, titlePlate.Bottom + (compact ? 18 : 68));
        var itemHeight = compact ? 34 : 48;
        var itemStep = compact ? 40 : 58;
        var menuPanel = new Rectangle(
            x - 18,
            startY - 36,
            width + 36,
            itemStep * (_items.Count - 1) + itemHeight + 56);
        var panelTitle = "CHOOSE YOUR PATH";
        context.DebugText.Draw(
            new Vector2(x + 2, menuPanel.Y + 12),
            panelTitle,
            UiTheme.WithAlpha(palette.Warning, 0.88f * fade),
            1);
        context.SpriteBatch.Draw(
            context.Pixel,
            new Rectangle(x + 2, menuPanel.Y + 27, width * 3 / 5, 2),
            UiTheme.WithAlpha(palette.Warning, 0.54f * fade));

        for (var index = 0; index < _items.Count; index++)
        {
            var bounds = new Rectangle(x, startY + index * itemStep, width, itemHeight);
            DrawMenuItem(context, _items[index], bounds, index, index == _selectedIndex, palette, fade, compact, typography);
            _hitRegions.Add(new UiHitRegion(index, bounds, _items[index].Enabled));
        }

        if (!compact)
        {
            var footer = new Rectangle(x, menuPanel.Bottom + 5, width, 26);
            DrawStatusRibbon(context, footer, palette, fade);
        }

        UiTheme.DrawCursorAccent(context, _input.MousePosition, palette, _settings);
    }

    public void Dispose()
    {
        _backgroundTexture?.Dispose();
        _backgroundTexture = null;
    }

    private void DrawBackground(RenderContext context, UiPalette palette)
    {
        if (_backgroundTexture is null)
        {
            AdventureMenuScene.Draw(context, palette, _settings);
            return;
        }

        var placement = MainMenuBackgroundCropPlanner.Resolve(
            context.ViewportBounds,
            new Point(_backgroundTexture.Width, _backgroundTexture.Height));
        context.SpriteBatch.Draw(
            _backgroundTexture,
            placement.Destination,
            placement.Source,
            Color.White);
        MainMenuAmbientOverlay.Draw(context, palette, _settings);
    }

    private void DrawMenuItem(
        RenderContext context,
        MenuItem item,
        Rectangle bounds,
        int index,
        bool selected,
        UiPalette palette,
        float fade,
        bool compact,
        UiTypographyTokens typography)
    {
        var hovered = _pointer.HoveredId == index || bounds.Contains(_input.MousePosition);
        var pressed = _pointer.IsPressed(index);
        UiTheme.DrawButton(context, bounds, palette, selected, hovered, item.Enabled, pressed, _focusVisible && selected, _settings);
        var iconSize = compact ? 24 : 30;
        var iconBounds = new Rectangle(
            bounds.X + (compact ? 7 : 9),
            bounds.Center.Y - iconSize / 2,
            iconSize,
            iconSize);
        DrawMenuGlyph(context, iconBounds, index, item.Enabled, selected || hovered, palette, fade);

        if (selected && item.Enabled)
        {
            var pulse = _settings.Ui.ReducedMotion ? 0.72f : 0.62f + 0.22f * MathF.Sin((float)context.Time.TotalSeconds * 3.4f);
            var markerX = bounds.Right - (compact ? 16 : 20);
            var markerY = bounds.Center.Y - 4;
            var markerColor = UiTheme.WithAlpha(palette.Warning, pulse * fade);
            context.SpriteBatch.Draw(context.Pixel, new Rectangle(markerX + 3, markerY, 4, 2), markerColor);
            context.SpriteBatch.Draw(context.Pixel, new Rectangle(markerX + 1, markerY + 2, 8, 4), markerColor);
            context.SpriteBatch.Draw(context.Pixel, new Rectangle(markerX + 3, markerY + 6, 4, 2), markerColor);
            var glintWidth = Math.Max(10, (int)MathF.Round((bounds.Width - 64) * (0.28f + pulse * 0.18f)));
            context.SpriteBatch.Draw(context.Pixel, new Rectangle(bounds.X + 48, bounds.Bottom - 4, glintWidth, 2), UiTheme.WithAlpha(palette.Accent, 0.48f * fade));
        }

        var text = item.Enabled ? UiTheme.WithAlpha(palette.Text, fade) : UiTheme.WithAlpha(palette.TextMuted, 0.58f * fade);
        var hint = item.Enabled ? UiTheme.WithAlpha(palette.TextMuted, 0.86f * fade) : UiTheme.WithAlpha(palette.TextMuted, 0.38f * fade);
        var pressOffset = pressed ? 1 : 0;
        var bodyScale = compact ? Math.Min(2, typography.BodyScale) : typography.BodyScale;
        var captionScale = compact ? 1 : typography.CaptionScale;
        var textX = bounds.X + (compact ? 39 : 50) + (selected && item.Enabled ? 3 : 0);
        context.DebugText.Draw(new Vector2(textX, bounds.Y + (compact ? 3 : 7) + pressOffset), item.Label, text, bodyScale);
        context.DebugText.Draw(new Vector2(textX, bounds.Y + (compact ? 21 : 28) + pressOffset), item.Hint, hint, captionScale);

        if (!item.Enabled)
        {
            context.DebugText.Draw(
                new Vector2(bounds.Right - (compact ? 55 : 68), bounds.Y + (compact ? 4 : 8)),
                "SOON",
                UiTheme.WithAlpha(palette.Warning, 0.76f * fade),
                1);
        }
    }

    private void DrawAdventureTitlePlate(
        RenderContext context,
        Rectangle bounds,
        UiPalette palette,
        float fade,
        bool compact)
    {
        var materials = UiTheme.ResolveMaterials(palette);
        var ropeTop = Math.Max(context.ViewportBounds.Y, bounds.Y - (compact ? 10 : 22));
        var ropeHeight = Math.Max(1, bounds.Y - ropeTop + 3);
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(bounds.X + 30, ropeTop, 3, ropeHeight), UiTheme.WithAlpha(materials.WoodLight, 0.72f * fade));
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(bounds.Right - 33, ropeTop, 3, ropeHeight), UiTheme.WithAlpha(materials.WoodLight, 0.72f * fade));
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(bounds.X + 7, bounds.Y + 7, bounds.Width, bounds.Height), UiTheme.WithAlpha(materials.FrameShadow, 0.58f * fade));
        UiTheme.DrawPanel(context, bounds, palette, _settings.Ui.PanelOpacity * 0.92f * fade, raised: false, settings: _settings);
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(bounds.X + 10, bounds.Y + 8, bounds.Width - 20, 4), UiTheme.WithAlpha(materials.WoodLight, 0.78f * fade));
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(bounds.X + 13, bounds.Bottom - 8, bounds.Width - 26, 3), UiTheme.WithAlpha(materials.BrassDark, 0.70f * fade));
        DrawCornerBolt(context, new Point(bounds.X + 12, bounds.Y + 12), materials.Brass, fade);
        DrawCornerBolt(context, new Point(bounds.Right - 13, bounds.Y + 12), materials.Brass, fade);

        for (var leaf = 0; leaf < 6; leaf++)
        {
            var x = bounds.X + 18 + leaf * Math.Max(12, (bounds.Width - 36) / 6);
            var y = bounds.Bottom - 9 + (leaf % 2 == 0 ? -2 : 1);
            context.SpriteBatch.Draw(context.Pixel, new Rectangle(x, y, 7, 3), UiTheme.WithAlpha(palette.Accent, 0.58f * fade));
            context.SpriteBatch.Draw(context.Pixel, new Rectangle(x + 2, y - 3, 3, 7), UiTheme.WithAlpha(palette.AccentSoft, 0.44f * fade));
        }
    }

    private void DrawAdventureMenuFrame(RenderContext context, Rectangle bounds, UiPalette palette, float fade)
    {
        var materials = UiTheme.ResolveMaterials(palette);
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(bounds.X + 8, bounds.Y + 8, bounds.Width, bounds.Height), UiTheme.WithAlpha(materials.FrameShadow, 0.62f * fade));
        UiTheme.DrawPanel(context, bounds, palette, _settings.Ui.PanelOpacity * 0.88f * fade, raised: false, settings: _settings);
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(bounds.X + 7, bounds.Y + 5, bounds.Width - 14, 4), UiTheme.WithAlpha(materials.WoodLight, 0.72f * fade));
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(bounds.X + 7, bounds.Bottom - 9, bounds.Width - 14, 4), UiTheme.WithAlpha(materials.WoodDark, 0.82f * fade));
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(bounds.X + 6, bounds.Y + 8, 4, bounds.Height - 16), UiTheme.WithAlpha(materials.Wood, 0.72f * fade));
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(bounds.Right - 10, bounds.Y + 8, 4, bounds.Height - 16), UiTheme.WithAlpha(materials.Wood, 0.72f * fade));
        DrawCornerBolt(context, new Point(bounds.X + 10, bounds.Y + 10), materials.Brass, fade);
        DrawCornerBolt(context, new Point(bounds.Right - 11, bounds.Y + 10), materials.Brass, fade);
        DrawCornerBolt(context, new Point(bounds.X + 10, bounds.Bottom - 11), materials.BrassDark, fade);
        DrawCornerBolt(context, new Point(bounds.Right - 11, bounds.Bottom - 11), materials.BrassDark, fade);
    }

    private void DrawWorldBadge(RenderContext context, Rectangle bounds, UiPalette palette, float fade)
    {
        var materials = UiTheme.ResolveMaterials(palette);
        UiTheme.DrawPanel(context, bounds, palette, _settings.Ui.PanelOpacity * 0.72f * fade, raised: false, settings: _settings);
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(bounds.X + 10, bounds.Y + 9, 28, 3), UiTheme.WithAlpha(materials.Brass, 0.76f * fade));
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(bounds.X + 10, bounds.Bottom - 12, 28, 3), UiTheme.WithAlpha(materials.BrassDark, 0.72f * fade));
        context.DebugText.Draw(new Vector2(bounds.X + 48, bounds.Y + 9), "A LIVING WORLD AWAITS", UiTheme.WithAlpha(palette.Text, 0.92f * fade), 1);
        context.DebugText.Draw(new Vector2(bounds.X + 48, bounds.Y + 29), "BUILD  EXPLORE  SURVIVE", UiTheme.WithAlpha(palette.TextMuted, 0.82f * fade), 1);
        var pulse = _settings.Ui.ReducedMotion ? 0.76f : 0.62f + 0.22f * MathF.Sin((float)context.Time.TotalSeconds * 2.7f);
        DrawCornerBolt(context, new Point(bounds.X + 24, bounds.Center.Y), palette.Warning, pulse * fade);
    }

    private void DrawStatusRibbon(RenderContext context, Rectangle bounds, UiPalette palette, float fade)
    {
        var materials = UiTheme.ResolveMaterials(palette);
        UiTheme.DrawPanel(context, bounds, palette, _settings.Ui.PanelOpacity * 0.72f * fade, raised: false, settings: _settings);
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(bounds.X + 8, bounds.Center.Y, 18, 2), UiTheme.WithAlpha(materials.Brass, 0.68f * fade));
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(bounds.Right - 26, bounds.Center.Y, 18, 2), UiTheme.WithAlpha(materials.Brass, 0.68f * fade));
        var text = "ARROWS / WASD    ENTER TO CHOOSE";
        context.DebugText.Draw(
            new Vector2(bounds.Center.X - PixelTextWidth(text, 1) / 2, bounds.Y + 9),
            text,
            UiTheme.WithAlpha(palette.Text, 0.82f * fade),
            1);
    }

    private static void DrawMenuGlyph(
        RenderContext context,
        Rectangle bounds,
        int index,
        bool enabled,
        bool highlighted,
        UiPalette palette,
        float fade)
    {
        var frame = enabled ? palette.AccentSoft : palette.SurfaceRaised;
        var icon = enabled ? palette.Accent : palette.TextMuted;
        var detail = enabled ? palette.Warning : palette.SurfaceHover;
        UiTheme.DrawRoundedRectangle(context, bounds, UiTheme.WithAlpha(frame, (highlighted ? 0.72f : 0.42f) * fade), 4);
        var center = bounds.Center;
        switch (index)
        {
            case 0:
                context.SpriteBatch.Draw(context.Pixel, new Rectangle(bounds.X + 4, center.Y + 3, bounds.Width - 8, 3), UiTheme.WithAlpha(icon, fade));
                context.SpriteBatch.Draw(context.Pixel, new Rectangle(center.X - 4, center.Y + 6, 8, 5), UiTheme.WithAlpha(palette.SurfaceRaised, fade));
                context.SpriteBatch.Draw(context.Pixel, new Rectangle(center.X - 1, center.Y - 7, 3, 10), UiTheme.WithAlpha(detail, fade));
                context.SpriteBatch.Draw(context.Pixel, new Rectangle(center.X - 6, center.Y - 9, 12, 5), UiTheme.WithAlpha(icon, fade));
                break;
            case 1:
                context.SpriteBatch.Draw(context.Pixel, new Rectangle(center.X - 8, center.Y - 8, 6, 6), UiTheme.WithAlpha(icon, fade));
                context.SpriteBatch.Draw(context.Pixel, new Rectangle(center.X + 2, center.Y - 6, 6, 6), UiTheme.WithAlpha(detail, fade));
                context.SpriteBatch.Draw(context.Pixel, new Rectangle(center.X - 10, center.Y, 10, 9), UiTheme.WithAlpha(icon, fade));
                context.SpriteBatch.Draw(context.Pixel, new Rectangle(center.X, center.Y + 1, 10, 8), UiTheme.WithAlpha(detail, fade));
                break;
            case 2:
                context.SpriteBatch.Draw(context.Pixel, new Rectangle(center.X - 8, center.Y - 1, 17, 3), UiTheme.WithAlpha(icon, fade));
                context.SpriteBatch.Draw(context.Pixel, new Rectangle(center.X - 1, center.Y - 8, 3, 17), UiTheme.WithAlpha(icon, fade));
                DrawCornerBolt(context, new Point(center.X - 8, center.Y), detail, fade);
                DrawCornerBolt(context, new Point(center.X + 8, center.Y), detail, fade);
                DrawCornerBolt(context, new Point(center.X, center.Y - 8), detail, fade);
                DrawCornerBolt(context, center, icon, fade);
                break;
            case 3:
                context.SpriteBatch.Draw(context.Pixel, new Rectangle(center.X - 8, center.Y - 5, 17, 11), UiTheme.WithAlpha(icon, fade));
                context.SpriteBatch.Draw(context.Pixel, new Rectangle(center.X - 5, center.Y - 8, 11, 17), UiTheme.WithAlpha(icon, fade));
                context.SpriteBatch.Draw(context.Pixel, new Rectangle(center.X - 3, center.Y - 3, 7, 7), UiTheme.WithAlpha(palette.Surface, fade));
                context.SpriteBatch.Draw(context.Pixel, new Rectangle(center.X - 1, center.Y - 1, 3, 3), UiTheme.WithAlpha(detail, fade));
                break;
            default:
                context.SpriteBatch.Draw(context.Pixel, new Rectangle(center.X - 7, center.Y - 9, 14, 19), UiTheme.WithAlpha(icon, fade));
                context.SpriteBatch.Draw(context.Pixel, new Rectangle(center.X - 3, center.Y - 5, 7, 15), UiTheme.WithAlpha(palette.Surface, fade));
                context.SpriteBatch.Draw(context.Pixel, new Rectangle(center.X + 1, center.Y + 1, 2, 2), UiTheme.WithAlpha(detail, fade));
                context.SpriteBatch.Draw(context.Pixel, new Rectangle(center.X - 9, center.Y + 9, 19, 3), UiTheme.WithAlpha(detail, fade));
                break;
        }
    }

    private static void DrawCornerBolt(RenderContext context, Point center, Color color, float alpha)
    {
        var tinted = UiTheme.WithAlpha(color, Math.Clamp(alpha, 0f, 1f));
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(center.X - 1, center.Y - 3, 3, 7), tinted);
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(center.X - 3, center.Y - 1, 7, 3), tinted);
    }

    private static int PixelTextWidth(string text, int scale)
    {
        return text.Length == 0 ? 0 : text.Length * 6 * scale - scale;
    }

    private void MoveSelection(int direction)
    {
        if (_items.Count == 0)
        {
            return;
        }

        var next = _selectedIndex;
        for (var attempt = 0; attempt < _items.Count; attempt++)
        {
            next = ((next + direction) % _items.Count + _items.Count) % _items.Count;
            if (_items[next].Enabled)
            {
                _selectedIndex = next;
                return;
            }
        }
    }

    private void ActivateSelected()
    {
        if (_selectedIndex < 0 || _selectedIndex >= _items.Count || !_items[_selectedIndex].Enabled)
        {
            return;
        }

        _items[_selectedIndex].Action();
    }

    private void StartSingleplayer()
    {
        _states.ChangeState(new WorldSelectState(_states, this));
    }

    private void OpenSettings()
    {
        _states.ChangeState(new SettingsState(_states, this));
    }

    private static void Noop()
    {
    }

    private static GameSettings LoadSettings()
    {
        try
        {
            return new GameSettingsService().LoadOrCreate(ClientPaths.SettingsPath());
        }
        catch (InvalidDataException)
        {
            return GameSettings.CreateDefault();
        }
    }

    private sealed record MenuItem(string Label, string Hint, bool Enabled, Action Action);
}
