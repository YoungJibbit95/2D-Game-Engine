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

public sealed class WorldSelectState : IGameState
{
    private readonly GameStateManager _states;
    private readonly IGameState _backState;
    private readonly InputManager _input = new();
    private readonly UiAnimationPlayer _introAnimation = new();
    private readonly WorldSaveBrowser _worlds = new();
    private readonly List<UiHitRegion> _hitRegions = new();
    private readonly UiPointerRouter _pointer = new();
    private readonly UiGamepadNavigator _gamepad = new();
    private readonly UiScrollModel _scroll = new();
    private IReadOnlyList<WorldSaveEntry> _entries = Array.Empty<WorldSaveEntry>();
    private GameSettings _settings = GameSettings.CreateDefault();
    private int _selectedIndex;
    private Rectangle _listBounds;
    private bool _focusVisible = true;
    private UiTypographyTokens _typography = UiTheme.Contract.Typography;

    public WorldSelectState(GameStateManager states, IGameState backState)
    {
        _states = states;
        _backState = backState;
    }

    public string Name => "WorldSelect";

    public void Initialize()
    {
        _settings = LoadSettings();
        _entries = _worlds.ListWorlds();
        _selectedIndex = Math.Clamp(_selectedIndex, 0, Math.Max(0, OptionCount - 1));
        _pointer.Reset();
        _gamepad.Reset();
        _focusVisible = true;
        _introAnimation.Play(UiAnimationClip.SlideFadeIn(_settings.Ui.ReducedMotion ? 0.001f : 0.22f, -16f));
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

        if (_input.IsKeyPressed(Keys.Escape) || _gamepad.CancelPressed)
        {
            Back();
            return;
        }

        var scrolled = false;
        if (_input.ScrollDelta != 0 && _listBounds.Contains(_input.MousePosition))
        {
            _scroll.ScrollBy(_input.ScrollDelta < 0 ? 1 : -1);
            scrolled = true;
            _focusVisible = false;
        }

        if (!scrolled && _pointer.HoveredId >= 0 && _pointer.HoveredId < OptionCount)
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

        if (_pointer.ClickedId >= 0 && _pointer.ClickedId < OptionCount)
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

        if (_input.IsKeyPressed(Keys.R))
        {
            RefreshWorlds();
        }
    }

    public void Draw(RenderContext context)
    {
        var palette = UiTheme.Resolve(_settings);
        _typography = UiTheme.ResolveContract(_settings).Typography;
        var offsetY = (int)MathF.Round(_introAnimation.GetValue(UiAnimationProperty.OffsetY, 0f));
        DrawBackground(context, palette);

        var panelWidth = Math.Min(760, Math.Max(280, context.ViewportBounds.Width - 32));
        var panelHeight = Math.Min(520, Math.Max(300, context.ViewportBounds.Height - 28));
        var panel = new Rectangle(
            context.ViewportBounds.Width / 2 - panelWidth / 2,
            context.ViewportBounds.Height / 2 - panelHeight / 2 + offsetY,
            panelWidth,
            panelHeight);

        UiTheme.DrawPanel(context, panel, palette, _settings.Ui.PanelOpacity, settings: _settings);
        context.DebugText.Draw(new Vector2(panel.X + 22, panel.Y + 18), "WORLD SELECT", palette.Accent, _typography.TitleScale);
        context.DebugText.Draw(new Vector2(panel.X + 24, panel.Y + 52), Trim("CHOOSE A WORLD OR START A NEW ADVENTURE", Math.Max(1, (panel.Width - 48) / (6 * _typography.CaptionScale))), palette.TextMuted, _typography.CaptionScale);

        _hitRegions.Clear();
        DrawWorldRows(context, panel, palette);
        DrawFooterOptions(context, panel, palette);
        UiTheme.DrawCursorAccent(context, _input.MousePosition, palette, _settings);
    }

    public void Dispose()
    {
    }

    private int OptionCount => _entries.Count + 3;

    private void DrawWorldRows(RenderContext context, Rectangle panel, UiPalette palette)
    {
        var list = new Rectangle(panel.X + 22, panel.Y + 82, panel.Width - 44, panel.Height - 176);
        _listBounds = list;
        UiTheme.DrawPanel(context, list, palette, 0.68f, raised: false, settings: _settings);

        if (_entries.Count == 0)
        {
            context.DebugText.Draw(new Vector2(list.X + 16, list.Y + 24), "NO SAVED WORLDS YET", palette.TextMuted, _typography.BodyScale);
            context.DebugText.Draw(new Vector2(list.X + 16, list.Y + 54), "CREATE A WORLD TO BEGIN", palette.Warning, _typography.CaptionScale);
            return;
        }

        var visible = Math.Min(_entries.Count, Math.Max(1, (list.Height - 18) / 42));
        _scroll.Configure(_entries.Count, visible);
        var scroll = _scroll.Offset;
        var compact = list.Width < 660;
        for (var index = scroll; index < Math.Min(_entries.Count, scroll + visible); index++)
        {
            var row = index - scroll;
            var bounds = new Rectangle(list.X + 10, list.Y + 10 + row * 42, list.Width - 20, 36);
            var entry = _entries[index];
            var selected = _selectedIndex == index;
            var hovered = _pointer.HoveredId == index || bounds.Contains(_input.MousePosition);
            var pressed = _pointer.IsPressed(index);
            UiTheme.DrawButton(context, bounds, palette, selected, hovered, pressed: pressed, focused: _focusVisible && selected, settings: _settings);
            var pressOffset = pressed ? 1 : 0;
            var nameScale = compact ? 1 : Math.Min(2, _typography.BodyScale);
            context.DebugText.Draw(new Vector2(bounds.X + 10, bounds.Y + (compact ? 11 : 6) + pressOffset), Trim(entry.Name, compact ? 18 : 22), palette.Text, nameScale);
            context.DebugText.Draw(new Vector2(bounds.Right - (compact ? 248 : 270), bounds.Y + 7 + pressOffset), entry.SizeLabel, palette.TextMuted, 1);
            context.DebugText.Draw(new Vector2(bounds.Right - (compact ? 174 : 180), bounds.Y + 7 + pressOffset), $"SEED {entry.Seed}", palette.TextMuted, 1);
            context.DebugText.Draw(new Vector2(bounds.Right - 82, bounds.Y + 7 + pressOffset), Trim(entry.CreatedLabel, 10), palette.TextMuted, 1);
            _hitRegions.Add(new UiHitRegion(index, bounds));
        }

        if (_entries.Count > visible)
        {
            UiTheme.DrawScrollRail(context, new Rectangle(list.Right - 7, list.Y + 10, 4, list.Height - 20), scroll, visible, _entries.Count, palette);
        }
    }

    private void DrawFooterOptions(RenderContext context, Rectangle panel, UiPalette palette)
    {
        var createIndex = _entries.Count;
        var refreshIndex = _entries.Count + 1;
        var backIndex = _entries.Count + 2;
        var y = panel.Bottom - 76;
        var availableWidth = panel.Width - 44;
        var gap = 10;
        var backWidth = Math.Min(130, Math.Max(84, availableWidth / 5));
        var refreshWidth = Math.Min(150, Math.Max(96, availableWidth / 4));
        var createWidth = Math.Max(120, availableWidth - backWidth - refreshWidth - gap * 2);
        var create = new Rectangle(panel.X + 22, y, createWidth, 36);
        var refresh = new Rectangle(create.Right + gap, y, refreshWidth, 36);
        var back = new Rectangle(refresh.Right + gap, y, backWidth, 36);
        DrawOption(context, palette, create, "CREATE WORLD", createIndex);
        DrawOption(context, palette, refresh, "REFRESH", refreshIndex);
        DrawOption(context, palette, back, "BACK", backIndex);
    }

    private void DrawOption(RenderContext context, UiPalette palette, Rectangle bounds, string label, int index)
    {
        var selected = _selectedIndex == index;
        var hovered = _pointer.HoveredId == index || bounds.Contains(_input.MousePosition);
        var pressed = _pointer.IsPressed(index);
        UiTheme.DrawButton(context, bounds, palette, selected, hovered, pressed: pressed, focused: _focusVisible && selected, settings: _settings);
        context.DebugText.Draw(
            new Vector2(bounds.X + 14, bounds.Y + 10 + (pressed ? 1 : 0)),
            Trim(label, Math.Max(1, (bounds.Width - 22) / (6 * _typography.CaptionScale))),
            palette.Text,
            _typography.CaptionScale);
        _hitRegions.Add(new UiHitRegion(index, bounds));
    }

    private void MoveSelection(int direction)
    {
        _selectedIndex = ((_selectedIndex + direction) % OptionCount + OptionCount) % OptionCount;
        if (_selectedIndex < _entries.Count)
        {
            _scroll.EnsureVisible(_selectedIndex);
        }
    }

    private void ActivateSelected()
    {
        if (_selectedIndex < _entries.Count)
        {
            var entry = _entries[_selectedIndex];
            _states.ChangeState(new LoadingWorldState(_states, WorldLoadRequest.Singleplayer(entry.Seed, entry.Name)));
            return;
        }

        if (_selectedIndex == _entries.Count)
        {
            _states.ChangeState(new CreateWorldState(_states, this));
            return;
        }

        if (_selectedIndex == _entries.Count + 1)
        {
            RefreshWorlds();
            return;
        }

        Back();
    }

    private void RefreshWorlds()
    {
        _entries = _worlds.ListWorlds();
        _selectedIndex = Math.Clamp(_selectedIndex, 0, Math.Max(0, OptionCount - 1));
        _scroll.Configure(_entries.Count, _scroll.ViewCapacity);
        if (_selectedIndex < _entries.Count)
        {
            _scroll.EnsureVisible(_selectedIndex);
        }
    }

    private void Back()
    {
        _states.ChangeState(_backState);
    }

    private static void DrawBackground(RenderContext context, UiPalette palette)
    {
        context.SpriteBatch.Draw(context.Pixel, context.ViewportBounds, palette.Backdrop);
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(0, context.ViewportBounds.Height - 84, context.ViewportBounds.Width, 84), new Color(38, 34, 31));
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(0, context.ViewportBounds.Height - 84, context.ViewportBounds.Width, 6), palette.Accent);
    }

    private static string Trim(string value, int max)
    {
        return value.Length <= max ? value : value[..max];
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

}
