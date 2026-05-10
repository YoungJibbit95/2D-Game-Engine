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
    private readonly List<Rectangle> _itemBounds = new();
    private readonly UiAnimationPlayer _introAnimation = new();
    private GameSettings _settings = GameSettings.CreateDefault();
    private int _selectedIndex;

    public MainMenuState(GameStateManager states, Action exit)
    {
        _states = states;
        _exit = exit;
        _items =
        [
            new MenuItem("SINGLEPLAYER", "CREATE A LOCAL WORLD", true, StartSingleplayer),
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
        _introAnimation.Update((float)deltaSeconds, _settings.Ui.AnimationSpeed);

        if (_input.IsKeyPressed(Keys.Down) || _input.IsKeyPressed(Keys.S))
        {
            MoveSelection(1);
        }
        else if (_input.IsKeyPressed(Keys.Up) || _input.IsKeyPressed(Keys.W))
        {
            MoveSelection(-1);
        }

        UpdateMouseSelection();

        if (_input.IsKeyPressed(Keys.Enter) || _input.IsKeyPressed(Keys.Space) || _input.IsLeftMousePressed)
        {
            ActivateSelected();
        }

        // Escape is intentionally ignored here so a stray key press never closes the game from the main menu.
    }

    public void Draw(RenderContext context)
    {
        var palette = UiTheme.Resolve(_settings);
        var fade = _introAnimation.GetValue(UiAnimationProperty.Opacity, 1f);
        var offsetY = (int)MathF.Round(_introAnimation.GetValue(UiAnimationProperty.OffsetY, 0f));
        DrawBackground(context, palette);

        var titleX = Math.Max(36, context.ViewportBounds.Width / 2 - 250);
        context.DebugText.Draw(new Vector2(titleX, 64 + offsetY), "TERRARIA LIKE ENGINE", palette.Accent, 4);
        context.DebugText.Draw(new Vector2(titleX + 4, 112 + offsetY), "SANDBOX ACTION GAMEBUILDING CORE", palette.TextMuted, 2);

        _itemBounds.Clear();
        var startY = 172 + offsetY;
        var width = Math.Min(520, context.ViewportBounds.Width - 72);
        var x = Math.Max(36, context.ViewportBounds.Width / 2 - width / 2);

        for (var index = 0; index < _items.Count; index++)
        {
            var bounds = new Rectangle(x, startY + index * 62, width, 48);
            _itemBounds.Add(bounds);
            DrawMenuItem(context, _items[index], bounds, index == _selectedIndex, palette, fade);
        }

        var footerY = context.ViewportBounds.Height - 48;
        if (_settings.Ui.ShowControlHints)
        {
            context.DebugText.Draw(new Vector2(36, footerY), "ENTER SELECT   ESC STAYS HERE   F10 CONSOLE IN GAME", palette.TextMuted, 2);
        }
    }

    public void Dispose()
    {
    }

    private void DrawMenuItem(RenderContext context, MenuItem item, Rectangle bounds, bool selected, UiPalette palette, float fade)
    {
        var hovered = bounds.Contains(_input.MousePosition);
        UiTheme.DrawButton(context, bounds, palette, selected, hovered, item.Enabled);

        var text = item.Enabled ? palette.Text : UiTheme.WithAlpha(palette.TextMuted, 0.62f * fade);
        var hint = item.Enabled ? palette.TextMuted : UiTheme.WithAlpha(palette.TextMuted, 0.45f * fade);
        context.DebugText.Draw(new Vector2(bounds.X + 16, bounds.Y + 8), item.Label, text, 2);
        context.DebugText.Draw(new Vector2(bounds.X + 16, bounds.Y + 28), item.Hint, hint, 1);

        if (!item.Enabled)
        {
            context.DebugText.Draw(new Vector2(bounds.Right - 118, bounds.Y + 17), "PLANNED", palette.Warning, 2);
        }
    }

    private static void DrawBackground(RenderContext context, UiPalette palette)
    {
        context.SpriteBatch.Draw(context.Pixel, context.ViewportBounds, palette.Backdrop);
        var horizon = new Rectangle(0, context.ViewportBounds.Height / 2, context.ViewportBounds.Width, context.ViewportBounds.Height / 2);
        context.SpriteBatch.Draw(context.Pixel, horizon, UiTheme.WithAlpha(palette.AccentSoft, 0.55f));
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

    private void UpdateMouseSelection()
    {
        for (var index = 0; index < _itemBounds.Count; index++)
        {
            if (_itemBounds[index].Contains(_input.MousePosition))
            {
                _selectedIndex = index;
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
        _states.ChangeState(new LoadingWorldState(_states, WorldLoadRequest.Singleplayer(seed: 1337)));
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
