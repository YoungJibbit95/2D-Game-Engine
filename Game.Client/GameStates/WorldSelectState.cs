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
    private readonly List<HitZone> _hitZones = new();
    private IReadOnlyList<WorldSaveEntry> _entries = Array.Empty<WorldSaveEntry>();
    private GameSettings _settings = GameSettings.CreateDefault();
    private int _selectedIndex;

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
        _introAnimation.Update((float)deltaSeconds, _settings.Ui.AnimationSpeed);

        if (_input.IsKeyPressed(Keys.Escape))
        {
            Back();
            return;
        }

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

        if (_input.IsKeyPressed(Keys.R))
        {
            _entries = _worlds.ListWorlds();
            _selectedIndex = Math.Clamp(_selectedIndex, 0, Math.Max(0, OptionCount - 1));
        }
    }

    public void Draw(RenderContext context)
    {
        var palette = UiTheme.Resolve(_settings);
        var offsetY = (int)MathF.Round(_introAnimation.GetValue(UiAnimationProperty.OffsetY, 0f));
        DrawBackground(context, palette);

        var panelWidth = Math.Min(760, context.ViewportBounds.Width - 56);
        var panelHeight = Math.Min(520, context.ViewportBounds.Height - 56);
        var panel = new Rectangle(
            context.ViewportBounds.Width / 2 - panelWidth / 2,
            context.ViewportBounds.Height / 2 - panelHeight / 2 + offsetY,
            panelWidth,
            panelHeight);

        UiTheme.DrawPanel(context, panel, palette, _settings.Ui.PanelOpacity);
        context.DebugText.Draw(new Vector2(panel.X + 22, panel.Y + 18), "WORLD SELECT", palette.Accent, 3);
        context.DebugText.Draw(new Vector2(panel.X + 24, panel.Y + 52), "ENTER PLAY   CREATE WORLD   R REFRESH   ESC BACK", palette.TextMuted, 1);

        _hitZones.Clear();
        DrawWorldRows(context, panel, palette);
        DrawFooterOptions(context, panel, palette);
    }

    public void Dispose()
    {
    }

    private int OptionCount => _entries.Count + 2;

    private void DrawWorldRows(RenderContext context, Rectangle panel, UiPalette palette)
    {
        var list = new Rectangle(panel.X + 22, panel.Y + 82, panel.Width - 44, panel.Height - 176);
        UiTheme.DrawPanel(context, list, palette, 0.68f, raised: false);

        if (_entries.Count == 0)
        {
            context.DebugText.Draw(new Vector2(list.X + 16, list.Y + 24), "NO SAVED WORLDS YET", palette.TextMuted, 2);
            context.DebugText.Draw(new Vector2(list.X + 16, list.Y + 54), "CREATE A WORLD TO BEGIN", palette.Warning, 1);
            return;
        }

        var visible = Math.Min(_entries.Count, 8);
        var scroll = Math.Clamp(_selectedIndex - visible + 1, 0, Math.Max(0, _entries.Count - visible));
        for (var index = scroll; index < Math.Min(_entries.Count, scroll + visible); index++)
        {
            var row = index - scroll;
            var bounds = new Rectangle(list.X + 10, list.Y + 10 + row * 42, list.Width - 20, 36);
            var entry = _entries[index];
            var selected = _selectedIndex == index;
            var hovered = bounds.Contains(_input.MousePosition);
            UiTheme.DrawButton(context, bounds, palette, selected, hovered);
            context.DebugText.Draw(new Vector2(bounds.X + 10, bounds.Y + 6), Trim(entry.Name, 24), palette.Text, 2);
            context.DebugText.Draw(new Vector2(bounds.X + 286, bounds.Y + 7), entry.SizeLabel, palette.TextMuted, 1);
            context.DebugText.Draw(new Vector2(bounds.X + 406, bounds.Y + 7), $"SEED {entry.Seed}", palette.TextMuted, 1);
            context.DebugText.Draw(new Vector2(bounds.Right - 98, bounds.Y + 7), entry.CreatedLabel, palette.TextMuted, 1);
            _hitZones.Add(new HitZone(bounds, index));
        }
    }

    private void DrawFooterOptions(RenderContext context, Rectangle panel, UiPalette palette)
    {
        var createIndex = _entries.Count;
        var backIndex = _entries.Count + 1;
        var y = panel.Bottom - 76;
        var create = new Rectangle(panel.X + 22, y, 220, 36);
        var back = new Rectangle(create.Right + 12, y, 140, 36);
        DrawOption(context, palette, create, "CREATE WORLD", createIndex);
        DrawOption(context, palette, back, "BACK", backIndex);
    }

    private void DrawOption(RenderContext context, UiPalette palette, Rectangle bounds, string label, int index)
    {
        UiTheme.DrawButton(context, bounds, palette, _selectedIndex == index, bounds.Contains(_input.MousePosition));
        context.DebugText.Draw(new Vector2(bounds.X + 14, bounds.Y + 10), label, palette.Text, 1);
        _hitZones.Add(new HitZone(bounds, index));
    }

    private void MoveSelection(int direction)
    {
        _selectedIndex = ((_selectedIndex + direction) % OptionCount + OptionCount) % OptionCount;
    }

    private void UpdateMouseSelection()
    {
        foreach (var zone in _hitZones)
        {
            if (zone.Bounds.Contains(_input.MousePosition))
            {
                _selectedIndex = zone.Index;
                return;
            }
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

        Back();
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

    private sealed record HitZone(Rectangle Bounds, int Index);
}
