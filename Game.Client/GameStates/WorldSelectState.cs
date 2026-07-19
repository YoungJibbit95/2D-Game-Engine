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
    private string _worldSummaryLabel = "NO LOCAL WORLDS";

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
        RefreshSummaryLabel();
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
        var layout = WorldMenuLayoutPlanner.PlanWorldSelect(context.ViewportBounds, _entries.Count, offsetY);
        WorldMenuPresentation.DrawBackdrop(context, palette);

        UiTheme.DrawPanel(context, layout.Panel, palette, _settings.Ui.PanelOpacity, settings: _settings);
        var titleScale = layout.Compact ? Math.Min(2, _typography.TitleScale) : _typography.TitleScale;
        var titleY = layout.Panel.Y + (layout.Compact ? 12 : 18);
        context.DebugText.Draw(
            new Vector2(layout.Panel.X + layout.ContentInset, titleY),
            "WORLD LIBRARY",
            palette.Accent,
            titleScale);
        if (!layout.Compact || layout.Panel.Width >= 390)
        {
            var summaryX = Math.Max(
                layout.Panel.X + layout.ContentInset,
                layout.Panel.Right - layout.ContentInset - _worldSummaryLabel.Length * 6);
            context.DebugText.Draw(
                new Vector2(summaryX, titleY + 4),
                _worldSummaryLabel,
                palette.TextMuted,
                1);
        }

        context.DebugText.Draw(
            new Vector2(layout.Panel.X + layout.ContentInset + 2, titleY + titleScale * 7 + 9),
            layout.Compact ? "CHOOSE A WORLD OR CREATE ONE" : "LOCAL WORLDS   ENTER TO PLAY   R TO REFRESH",
            palette.TextMuted,
            1);

        _hitRegions.Clear();
        DrawWorldRows(context, layout, palette);
        DrawFooterOptions(context, layout, palette);
        UiTheme.DrawCursorAccent(context, _input.MousePosition, palette, _settings);
    }

    public void Dispose()
    {
    }

    private int OptionCount => _entries.Count + 3;

    private void DrawWorldRows(RenderContext context, WorldSelectLayout layout, UiPalette palette)
    {
        _listBounds = layout.List;
        UiTheme.DrawPanel(context, layout.List, palette, 0.68f, raised: false, settings: _settings);

        if (_entries.Count == 0)
        {
            DrawEmptyWorldLibrary(context, layout, palette);
            return;
        }

        var visible = Math.Min(_entries.Count, layout.VisibleRowCount);
        _scroll.Configure(_entries.Count, visible);
        var scroll = _scroll.Offset;
        var reserveScrollRail = _entries.Count > visible;
        for (var index = scroll; index < Math.Min(_entries.Count, scroll + visible); index++)
        {
            var row = index - scroll;
            var bounds = layout.GetRowBounds(row, reserveScrollRail);
            var entry = _entries[index];
            var selected = _selectedIndex == index;
            var hovered = _pointer.HoveredId == index || bounds.Contains(_input.MousePosition);
            var pressed = _pointer.IsPressed(index);
            UiTheme.DrawButton(context, bounds, palette, selected, hovered, pressed: pressed, focused: _focusVisible && selected, settings: _settings);
            var pressOffset = pressed ? 1 : 0;
            if (layout.StackedMetadata)
            {
                var nameScale = bounds.Width >= 430 ? Math.Min(2, _typography.BodyScale) : 1;
                context.DebugText.Draw(
                    new Vector2(bounds.X + 12, bounds.Y + 6 + pressOffset),
                    entry.CompactDisplayName,
                    palette.Text,
                    nameScale);
                context.DebugText.Draw(
                    new Vector2(bounds.X + 12, bounds.Bottom - 14 + pressOffset),
                    layout.ShowCreatedDate ? entry.MetadataLabel : entry.CompactMetadataLabel,
                    palette.TextMuted,
                    1);
            }
            else
            {
                var metadataX = bounds.Right - 12 - entry.MetadataLabel.Length * 6;
                context.DebugText.Draw(
                    new Vector2(bounds.X + 12, bounds.Y + 14 + pressOffset),
                    entry.DisplayName,
                    palette.Text,
                    Math.Min(2, _typography.BodyScale));
                context.DebugText.Draw(
                    new Vector2(metadataX, bounds.Y + 18 + pressOffset),
                    entry.MetadataLabel,
                    palette.TextMuted,
                    1);
            }

            _hitRegions.Add(new UiHitRegion(index, bounds));
        }

        if (_entries.Count > visible)
        {
            UiTheme.DrawScrollRail(
                context,
                new Rectangle(layout.List.Right - 8, layout.List.Y + 8, 4, Math.Max(1, layout.List.Height - 16)),
                scroll,
                visible,
                _entries.Count,
                palette);
        }
    }

    private void DrawEmptyWorldLibrary(RenderContext context, WorldSelectLayout layout, UiPalette palette)
    {
        var centerX = layout.List.Center.X;
        var iconY = layout.List.Y + Math.Max(12, layout.List.Height / 5);
        var iconWidth = Math.Min(48, Math.Max(24, layout.List.Width / 8));
        context.SpriteBatch.Draw(
            context.Pixel,
            new Rectangle(centerX - iconWidth / 2, iconY, iconWidth, Math.Max(5, iconWidth / 5)),
            UiTheme.WithAlpha(palette.AccentSoft, 0.82f));
        context.SpriteBatch.Draw(
            context.Pixel,
            new Rectangle(centerX - iconWidth / 3, iconY - Math.Max(4, iconWidth / 8), iconWidth * 2 / 3, Math.Max(4, iconWidth / 8)),
            UiTheme.WithAlpha(palette.Accent, 0.72f));

        const string emptyTitle = "NO SAVED WORLDS";
        const string emptyHint = "CREATE YOUR FIRST WORLD BELOW";
        var titleY = iconY + Math.Max(18, iconWidth / 2);
        context.DebugText.Draw(
            new Vector2(centerX - emptyTitle.Length * 3, titleY),
            emptyTitle,
            palette.Text,
            1);
        if (layout.List.Height >= 82)
        {
            context.DebugText.Draw(
                new Vector2(centerX - emptyHint.Length * 3, titleY + 17),
                emptyHint,
                palette.TextMuted,
                1);
        }
    }

    private void DrawFooterOptions(RenderContext context, WorldSelectLayout layout, UiPalette palette)
    {
        var createIndex = _entries.Count;
        var refreshIndex = _entries.Count + 1;
        var backIndex = _entries.Count + 2;
        DrawOption(
            context,
            palette,
            layout.CreateButton,
            layout.CreateButton.Width < 82 ? "NEW" : "CREATE WORLD",
            createIndex);
        DrawOption(
            context,
            palette,
            layout.RefreshButton,
            layout.RefreshButton.Width < 64 ? "SYNC" : "REFRESH",
            refreshIndex);
        DrawOption(context, palette, layout.BackButton, "BACK", backIndex);
    }

    private void DrawOption(RenderContext context, UiPalette palette, Rectangle bounds, string label, int index)
    {
        var selected = _selectedIndex == index;
        var hovered = _pointer.HoveredId == index || bounds.Contains(_input.MousePosition);
        var pressed = _pointer.IsPressed(index);
        UiTheme.DrawButton(context, bounds, palette, selected, hovered, pressed: pressed, focused: _focusVisible && selected, settings: _settings);
        context.DebugText.Draw(
            new Vector2(bounds.X + 14, bounds.Y + 10 + (pressed ? 1 : 0)),
            label,
            palette.Text,
            bounds.Width >= label.Length * 6 * _typography.CaptionScale + 22 ? _typography.CaptionScale : 1);
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
        RefreshSummaryLabel();
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

    private void RefreshSummaryLabel()
    {
        _worldSummaryLabel = _entries.Count switch
        {
            0 => "NO LOCAL WORLDS",
            1 => "1 LOCAL WORLD",
            _ => $"{_entries.Count} LOCAL WORLDS"
        };
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
