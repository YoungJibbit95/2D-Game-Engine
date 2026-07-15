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

public sealed class CreateWorldState : IGameState, ITextInputReceiver, IKeyboardCaptureState
{
    private readonly GameStateManager _states;
    private readonly IGameState _backState;
    private readonly InputManager _input = new();
    private readonly UiAnimationPlayer _introAnimation = new();
    private readonly List<UiHitRegion> _hitRegions = new();
    private readonly UiPointerRouter _pointer = new();
    private readonly UiGamepadNavigator _gamepad = new();
    private GameSettings _settings = GameSettings.CreateDefault();
    private string _worldName = "New World";
    private string _seedText = Random.Shared.Next(1, int.MaxValue).ToString(System.Globalization.CultureInfo.InvariantCulture);
    private int _selectedIndex;
    private string _status = "CREATE A WORLD";
    private bool _focusVisible = true;
    private UiTypographyTokens _typography = UiTheme.Contract.Typography;

    public CreateWorldState(GameStateManager states, IGameState backState)
    {
        _states = states;
        _backState = backState;
    }

    public string Name => "CreateWorld";

    public bool CapturesKeyboard => true;

    public void Initialize()
    {
        _settings = LoadSettings();
        _pointer.Reset();
        _gamepad.Reset();
        _focusVisible = true;
        _introAnimation.Play(UiAnimationClip.SlideFadeIn(_settings.Ui.ReducedMotion ? 0.001f : 0.2f, -12f));
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

        if (_pointer.HoveredId >= 0 && _pointer.HoveredId < 5 && IsOptionEnabled(_pointer.HoveredId))
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

        if (_input.IsKeyPressed(Keys.Back))
        {
            BackspaceField();
        }

        if (_pointer.ClickedId >= 0 && _pointer.ClickedId < 5)
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
    }

    public void Draw(RenderContext context)
    {
        var palette = UiTheme.Resolve(_settings);
        _typography = UiTheme.ResolveContract(_settings).Typography;
        var offsetY = (int)MathF.Round(_introAnimation.GetValue(UiAnimationProperty.OffsetY, 0f));
        context.SpriteBatch.Draw(context.Pixel, context.ViewportBounds, palette.Backdrop);

        var panelWidth = Math.Min(660, Math.Max(280, context.ViewportBounds.Width - 32));
        var panelHeight = Math.Min(420, Math.Max(304, context.ViewportBounds.Height - 28));
        var panel = new Rectangle(
            context.ViewportBounds.Width / 2 - panelWidth / 2,
            context.ViewportBounds.Height / 2 - panelHeight / 2 + offsetY,
            panelWidth,
            panelHeight);

        UiTheme.DrawPanel(context, panel, palette, _settings.Ui.PanelOpacity, settings: _settings);
        context.DebugText.Draw(new Vector2(panel.X + 22, panel.Y + 28), "CREATE WORLD", palette.Accent, _typography.TitleScale);
        context.DebugText.Draw(new Vector2(panel.X + 24, panel.Y + 60), FitText("NAME YOUR WORLD AND CHOOSE ITS SEED", Math.Max(1, (panel.Width - 48) / (6 * _typography.CaptionScale))), palette.TextMuted, _typography.CaptionScale);

        _hitRegions.Clear();
        DrawField(context, palette, panel, index: 0, label: "WORLD NAME", value: _worldName, row: 0);
        DrawField(context, palette, panel, index: 1, label: "SEED", value: _seedText, row: 1);
        var buttonRow = new Rectangle(panel.X + 24, panel.Y + 210, panel.Width - 48, 36);
        var gap = 8;
        var backWidth = Math.Clamp(buttonRow.Width * 18 / 100, 56, 120);
        var randomWidth = Math.Clamp(buttonRow.Width * 36 / 100, 92, 190);
        var createWidth = Math.Max(1, buttonRow.Width - backWidth - randomWidth - gap * 2);
        DrawButton(context, palette, new Rectangle(buttonRow.X, buttonRow.Y, createWidth, buttonRow.Height), "CREATE", 2, int.TryParse(_seedText, out _));
        DrawButton(context, palette, new Rectangle(buttonRow.X + createWidth + gap, buttonRow.Y, randomWidth, buttonRow.Height), "RANDOM SEED", 3, true);
        DrawButton(context, palette, new Rectangle(buttonRow.Right - backWidth, buttonRow.Y, backWidth, buttonRow.Height), "BACK", 4, true);

        context.DebugText.Draw(new Vector2(panel.X + 24, panel.Bottom - 52), $"PROFILE {_settings.World.WorldProfileId.ToUpperInvariant()}   INFINITE {(_settings.World.InfiniteHorizontalGeneration ? "ON" : "OFF")}", palette.TextMuted, _typography.CaptionScale);
        context.DebugText.Draw(new Vector2(panel.X + 24, panel.Bottom - 30), FitText(_status, Math.Max(1, (panel.Width - 48) / (6 * _typography.CaptionScale))), palette.Warning, _typography.CaptionScale);
        UiTheme.DrawCursorAccent(context, _input.MousePosition, palette, _settings);
    }

    public void Dispose()
    {
    }

    public void OnTextInput(char character)
    {
        if (_selectedIndex == 0)
        {
            if (!char.IsControl(character) && _worldName.Length < 28 && IsAllowedNameCharacter(character))
            {
                _worldName += character;
            }
        }
        else if (_selectedIndex == 1)
        {
            if (char.IsDigit(character) && _seedText.Length < 10)
            {
                _seedText += character;
            }
        }
    }

    private void DrawField(RenderContext context, UiPalette palette, Rectangle panel, int index, string label, string value, int row)
    {
        var bounds = new Rectangle(panel.X + 24, panel.Y + 86 + row * 58, panel.Width - 48, 40);
        var selected = _selectedIndex == index;
        var hovered = _pointer.HoveredId == index || bounds.Contains(_input.MousePosition);
        var pressed = _pointer.IsPressed(index);
        UiTheme.DrawButton(context, bounds, palette, selected, hovered, pressed: pressed, focused: _focusVisible && selected, settings: _settings);
        context.DebugText.Draw(new Vector2(bounds.X + 12, bounds.Y + 6 + (pressed ? 1 : 0)), label, palette.TextMuted, _typography.CaptionScale);
        var shownValue = string.IsNullOrWhiteSpace(value) ? "_" : value.ToUpperInvariant();
        if (selected && _focusVisible)
        {
            shownValue += "|";
        }

        var valueX = bounds.X + Math.Min(156, bounds.Width / 3);
        var availableWidth = Math.Max(6, bounds.Right - valueX - 10);
        var valueScale = _typography.BodyScale;
        while (valueScale > 1 && shownValue.Length * 6 * valueScale > availableWidth)
        {
            valueScale--;
        }

        shownValue = FitText(shownValue, Math.Max(1, availableWidth / (6 * valueScale)));
        context.DebugText.Draw(new Vector2(valueX, bounds.Y + (valueScale == 2 ? 14 : 17) + (pressed ? 1 : 0)), shownValue, palette.Text, valueScale);
        _hitRegions.Add(new UiHitRegion(index, bounds));
    }

    private void DrawButton(RenderContext context, UiPalette palette, Rectangle bounds, string label, int index, bool enabled)
    {
        var selected = _selectedIndex == index;
        var hovered = _pointer.HoveredId == index || bounds.Contains(_input.MousePosition);
        var pressed = _pointer.IsPressed(index);
        UiTheme.DrawButton(context, bounds, palette, selected, hovered, enabled, pressed, _focusVisible && selected, _settings);
        context.DebugText.Draw(
            new Vector2(bounds.X + 8, bounds.Y + 10 + (pressed ? 1 : 0)),
            FitText(label, Math.Max(1, (bounds.Width - 16) / (6 * _typography.CaptionScale))),
            enabled ? palette.Text : palette.TextMuted,
            _typography.CaptionScale);
        _hitRegions.Add(new UiHitRegion(index, bounds, enabled));
    }

    private void MoveSelection(int direction)
    {
        var next = _selectedIndex;
        for (var attempt = 0; attempt < 5; attempt++)
        {
            next = ((next + direction) % 5 + 5) % 5;
            if (IsOptionEnabled(next))
            {
                _selectedIndex = next;
                return;
            }
        }
    }

    private void ActivateSelected()
    {
        if (!IsOptionEnabled(_selectedIndex))
        {
            return;
        }

        switch (_selectedIndex)
        {
            case 2:
                CreateWorld();
                break;
            case 3:
                _seedText = Random.Shared.Next(1, int.MaxValue).ToString(System.Globalization.CultureInfo.InvariantCulture);
                _status = "RANDOM SEED READY";
                break;
            case 4:
                Back();
                break;
        }
    }

    private bool IsOptionEnabled(int index)
    {
        return index != 2 || int.TryParse(_seedText, out _);
    }

    private void CreateWorld()
    {
        var name = string.IsNullOrWhiteSpace(_worldName) ? "New World" : _worldName.Trim();
        if (!int.TryParse(_seedText, out var seed))
        {
            _status = "INVALID SEED";
            _selectedIndex = 1;
            return;
        }

        _states.ChangeState(new LoadingWorldState(_states, WorldLoadRequest.Singleplayer(seed, name)));
    }

    private void BackspaceField()
    {
        if (_selectedIndex == 0 && _worldName.Length > 0)
        {
            _worldName = _worldName[..^1];
        }
        else if (_selectedIndex == 1 && _seedText.Length > 0)
        {
            _seedText = _seedText[..^1];
        }
    }

    private void Back()
    {
        _states.ChangeState(_backState);
    }

    private static bool IsAllowedNameCharacter(char character)
    {
        return char.IsLetterOrDigit(character) || character == ' ' || character == '_' || character == '-';
    }

    private static string FitText(string value, int maxLength)
    {
        if (value.Length <= maxLength)
        {
            return value;
        }

        return maxLength <= 1 ? value[..1] : value[..(maxLength - 1)] + ".";
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
