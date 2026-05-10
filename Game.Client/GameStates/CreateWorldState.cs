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
    private readonly List<HitZone> _hitZones = new();
    private GameSettings _settings = GameSettings.CreateDefault();
    private string _worldName = "New World";
    private string _seedText = Random.Shared.Next(1, int.MaxValue).ToString(System.Globalization.CultureInfo.InvariantCulture);
    private int _selectedIndex;
    private string _status = "CREATE A WORLD";

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

        if (_input.IsKeyPressed(Keys.Back))
        {
            BackspaceField();
        }

        UpdateMouseSelection();

        if (_input.IsKeyPressed(Keys.Enter) || _input.IsKeyPressed(Keys.Space) || _input.IsLeftMousePressed)
        {
            ActivateSelected();
        }
    }

    public void Draw(RenderContext context)
    {
        var palette = UiTheme.Resolve(_settings);
        var offsetY = (int)MathF.Round(_introAnimation.GetValue(UiAnimationProperty.OffsetY, 0f));
        context.SpriteBatch.Draw(context.Pixel, context.ViewportBounds, palette.Backdrop);

        var panelWidth = Math.Min(660, context.ViewportBounds.Width - 56);
        var panelHeight = Math.Min(420, context.ViewportBounds.Height - 56);
        var panel = new Rectangle(
            context.ViewportBounds.Width / 2 - panelWidth / 2,
            context.ViewportBounds.Height / 2 - panelHeight / 2 + offsetY,
            panelWidth,
            panelHeight);

        UiTheme.DrawPanel(context, panel, palette, _settings.Ui.PanelOpacity);
        context.DebugText.Draw(new Vector2(panel.X + 22, panel.Y + 18), "CREATE WORLD", palette.Accent, 3);
        context.DebugText.Draw(new Vector2(panel.X + 24, panel.Y + 52), "TYPE NAME OR SEED   ENTER ACTIVATE   ESC BACK", palette.TextMuted, 1);

        _hitZones.Clear();
        DrawField(context, palette, panel, index: 0, label: "WORLD NAME", value: _worldName, row: 0);
        DrawField(context, palette, panel, index: 1, label: "SEED", value: _seedText, row: 1);
        DrawButton(context, palette, new Rectangle(panel.X + 24, panel.Y + 210, 190, 36), "CREATE", 2);
        DrawButton(context, palette, new Rectangle(panel.X + 230, panel.Y + 210, 190, 36), "RANDOM SEED", 3);
        DrawButton(context, palette, new Rectangle(panel.X + 436, panel.Y + 210, 120, 36), "BACK", 4);

        context.DebugText.Draw(new Vector2(panel.X + 24, panel.Bottom - 52), $"PROFILE {_settings.World.WorldProfileId.ToUpperInvariant()}   INFINITE {(_settings.World.InfiniteHorizontalGeneration ? "ON" : "OFF")}", palette.TextMuted, 1);
        context.DebugText.Draw(new Vector2(panel.X + 24, panel.Bottom - 30), _status, palette.Warning, 1);
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
        UiTheme.DrawButton(context, bounds, palette, _selectedIndex == index, bounds.Contains(_input.MousePosition));
        context.DebugText.Draw(new Vector2(bounds.X + 12, bounds.Y + 6), label, palette.TextMuted, 1);
        context.DebugText.Draw(new Vector2(bounds.X + 156, bounds.Y + 14), string.IsNullOrWhiteSpace(value) ? "_" : value.ToUpperInvariant(), palette.Text, 2);
        _hitZones.Add(new HitZone(bounds, index));
    }

    private void DrawButton(RenderContext context, UiPalette palette, Rectangle bounds, string label, int index)
    {
        UiTheme.DrawButton(context, bounds, palette, _selectedIndex == index, bounds.Contains(_input.MousePosition));
        context.DebugText.Draw(new Vector2(bounds.X + 12, bounds.Y + 10), label, palette.Text, 1);
        _hitZones.Add(new HitZone(bounds, index));
    }

    private void MoveSelection(int direction)
    {
        _selectedIndex = ((_selectedIndex + direction) % 5 + 5) % 5;
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
