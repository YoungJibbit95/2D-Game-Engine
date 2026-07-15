using Game.Client.Configuration;
using Game.Client.Input;
using Game.Client.Rendering;
using Game.Client.UI;
using Game.Core.Settings;
using Game.Core.UI.Animation;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Input;

namespace Game.Client.GameStates;

public sealed class MainMenuState : IGameState
{
    private readonly GameStateManager _states;
    private readonly Action _exit;
    private readonly InputManager _input = new();
    private readonly List<MenuItem> _items;
    private readonly List<UiHitRegion> _hitRegions = new();
    private readonly UiPointerRouter _pointer = new();
    private readonly UiGamepadNavigator _gamepad = new();
    private readonly UiAnimationPlayer _introAnimation = new();
    private GameSettings _settings = GameSettings.CreateDefault();
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
        var compact = context.ViewportBounds.Height < 560;

        var titleX = Math.Max(24, context.ViewportBounds.Width / 2 - 250);
        var titleScale = compact ? Math.Min(4, typography.DisplayScale) : typography.DisplayScale;
        var subtitleScale = compact ? 1 : typography.BodyScale;
        var titleY = compact ? 52 : 64;
        var subtitleY = titleY + titleScale * 7 + 10;
        context.DebugText.Draw(new Vector2(titleX, titleY + offsetY), "YJSE", palette.Accent, titleScale);
        context.DebugText.Draw(new Vector2(titleX + 4, subtitleY + offsetY), "YOUNGJIBBITS ENGINE", palette.TextMuted, subtitleScale);

        _hitRegions.Clear();
        var startY = (compact ? 120 : 172) + offsetY;
        var width = Math.Min(520, Math.Max(180, context.ViewportBounds.Width - 48));
        var x = context.ViewportBounds.Width / 2 - width / 2;
        var itemHeight = compact ? 38 : 48;
        var itemStep = compact ? 46 : 62;
        var menuPanel = new Rectangle(x - 12, startY - 12, width + 24, itemStep * (_items.Count - 1) + itemHeight + 24);
        UiTheme.DrawPanel(context, menuPanel, palette, _settings.Ui.PanelOpacity * fade, raised: false, settings: _settings);

        for (var index = 0; index < _items.Count; index++)
        {
            var bounds = new Rectangle(x, startY + index * itemStep, width, itemHeight);
            DrawMenuItem(context, _items[index], bounds, index, index == _selectedIndex, palette, fade, compact, typography);
            _hitRegions.Add(new UiHitRegion(index, bounds, _items[index].Enabled));
        }

        UiTheme.DrawCursorAccent(context, _input.MousePosition, palette, _settings);
    }

    public void Dispose()
    {
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

        var text = item.Enabled ? palette.Text : UiTheme.WithAlpha(palette.TextMuted, 0.62f * fade);
        var hint = item.Enabled ? palette.TextMuted : UiTheme.WithAlpha(palette.TextMuted, 0.45f * fade);
        var pressOffset = pressed ? 1 : 0;
        var bodyScale = compact ? Math.Min(2, typography.BodyScale) : typography.BodyScale;
        var captionScale = compact ? 1 : typography.CaptionScale;
        context.DebugText.Draw(new Vector2(bounds.X + 16, bounds.Y + (compact ? 5 : 8) + pressOffset), item.Label, text, bodyScale);
        context.DebugText.Draw(new Vector2(bounds.X + 16, bounds.Y + (compact ? 23 : 28) + pressOffset), item.Hint, hint, captionScale);

        if (!item.Enabled)
        {
            context.DebugText.Draw(
                new Vector2(bounds.Right - (compact ? 70 : 118), bounds.Y + (compact ? 15 : 17)),
                "PLANNED",
                palette.Warning,
                compact ? 1 : typography.BodyScale);
        }
    }

    private static void DrawBackground(RenderContext context, UiPalette palette)
    {
        context.SpriteBatch.Draw(context.Pixel, context.ViewportBounds, palette.Backdrop);
        var skyBand = new Rectangle(0, context.ViewportBounds.Height / 3, context.ViewportBounds.Width, context.ViewportBounds.Height * 2 / 3);
        context.SpriteBatch.Draw(context.Pixel, skyBand, UiTheme.WithAlpha(palette.AccentSoft, 0.32f));
        var horizon = new Rectangle(0, context.ViewportBounds.Height * 2 / 3, context.ViewportBounds.Width, context.ViewportBounds.Height / 3);
        context.SpriteBatch.Draw(context.Pixel, horizon, UiTheme.WithAlpha(palette.SurfaceHover, 0.38f));
        var ground = new Rectangle(0, context.ViewportBounds.Height - 86, context.ViewportBounds.Width, 86);
        context.SpriteBatch.Draw(context.Pixel, ground, new Color(45, 39, 35));
        var grass = new Rectangle(0, ground.Y, context.ViewportBounds.Width, 8);
        context.SpriteBatch.Draw(context.Pixel, grass, palette.Accent);
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
