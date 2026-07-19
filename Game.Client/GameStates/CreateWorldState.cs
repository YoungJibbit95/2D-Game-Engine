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
    private readonly WorldCreationForm _form;
    private GameSettings _settings = GameSettings.CreateDefault();
    private string _status = "READY TO CREATE";
    private string _profileLabel = "DEFAULT PROFILE";
    private bool _focusVisible = true;
    private UiTypographyTokens _typography = UiTheme.Contract.Typography;

    public CreateWorldState(GameStateManager states, IGameState backState)
    {
        _states = states;
        _backState = backState;
        _form = new WorldCreationForm(CreateRandomSeed());
    }

    public string Name => "CreateWorld";

    public bool CapturesKeyboard => true;

    public void Initialize()
    {
        _settings = LoadSettings();
        _pointer.Reset();
        _gamepad.Reset();
        _focusVisible = true;
        _profileLabel = $"PROFILE {WorldMenuText.Normalize(_settings.World.WorldProfileId)}   INFINITE WORLD {(_settings.World.InfiniteHorizontalGeneration ? "ON" : "OFF")}";
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

        if (_pointer.HoveredId is >= WorldCreationForm.CreateIndex and < WorldCreationForm.OptionCount &&
            _form.IsOptionEnabled(_pointer.HoveredId))
        {
            _form.Focus(_pointer.HoveredId);
            _focusVisible = false;
        }

        var letterNavigation = _form.AcceptsLetterNavigation;
        if (_input.IsKeyPressed(Keys.Down) || (letterNavigation && _input.IsKeyPressed(Keys.S)) || _gamepad.DownPressed)
        {
            _form.MoveSelection(1);
            _focusVisible = true;
        }
        else if (_input.IsKeyPressed(Keys.Up) || (letterNavigation && _input.IsKeyPressed(Keys.W)) || _gamepad.UpPressed)
        {
            _form.MoveSelection(-1);
            _focusVisible = true;
        }
        else if (!_form.IsEditingField && (_gamepad.LeftPressed || _gamepad.RightPressed))
        {
            _form.MoveSelection(_gamepad.RightPressed ? 1 : -1);
            _focusVisible = true;
        }

        if (_input.IsKeyPressed(Keys.Back) && _form.Backspace())
        {
            _status = _form.SelectedIndex == WorldCreationForm.SeedIndex && !_form.IsSeedValid
                ? "ENTER A WHOLE NUMBER"
                : "EDITING";
        }

        if (_pointer.ClickedId is >= 0 and < WorldCreationForm.OptionCount)
        {
            _form.Focus(_pointer.ClickedId, selectContents: _pointer.ClickedId < WorldCreationForm.CreateIndex);
            _focusVisible = false;
            if (!_form.IsEditingField)
            {
                ActivateSelected();
            }
        }
        else if (_input.IsKeyPressed(Keys.Enter) || _gamepad.ConfirmPressed)
        {
            _focusVisible = true;
            AdvanceOrActivate();
        }
        else if (!_form.IsEditingField && _input.IsKeyPressed(Keys.Space))
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
        var layout = WorldMenuLayoutPlanner.PlanCreateWorld(context.ViewportBounds, offsetY);
        WorldMenuPresentation.DrawBackdrop(context, palette);

        UiTheme.DrawPanel(context, layout.Panel, palette, _settings.Ui.PanelOpacity, settings: _settings);
        var titleScale = layout.Compact ? Math.Min(2, _typography.TitleScale) : _typography.TitleScale;
        var titleY = layout.Panel.Y + (layout.Compact ? 12 : 22);
        context.DebugText.Draw(
            new Vector2(layout.Panel.X + layout.ContentInset, titleY),
            "CREATE WORLD",
            palette.Accent,
            titleScale);
        context.DebugText.Draw(
            new Vector2(layout.Panel.X + layout.ContentInset + 2, titleY + titleScale * 7 + 9),
            layout.Compact ? "NAME AND SEED" : "CHOOSE A NAME AND A REPLAYABLE WORLD SEED",
            palette.TextMuted,
            1);

        _hitRegions.Clear();
        DrawField(
            context,
            palette,
            layout.NameField,
            WorldCreationForm.WorldNameIndex,
            "WORLD NAME",
            _form.WorldNameDisplay,
            layout.StackedFieldLabels);
        DrawField(
            context,
            palette,
            layout.SeedField,
            WorldCreationForm.SeedIndex,
            "SEED",
            _form.SeedDisplay,
            layout.StackedFieldLabels);
        DrawButton(
            context,
            palette,
            layout.CreateButton,
            "CREATE",
            WorldCreationForm.CreateIndex,
            _form.IsSeedValid);
        DrawButton(
            context,
            palette,
            layout.RandomButton,
            layout.RandomButton.Width < 92 ? "RANDOM" : "RANDOM SEED",
            WorldCreationForm.RandomSeedIndex,
            true);
        DrawButton(context, palette, layout.BackButton, "BACK", WorldCreationForm.BackIndex, true);

        DrawProfileCard(context, palette, layout);
        UiTheme.DrawCursorAccent(context, _input.MousePosition, palette, _settings);
    }

    public void Dispose()
    {
    }

    public void OnTextInput(char character)
    {
        if (_form.Append(character))
        {
            _status = _form.SelectedIndex == WorldCreationForm.SeedIndex && !_form.IsSeedValid
                ? "ENTER A WHOLE NUMBER"
                : "READY TO CREATE";
        }
    }

    private void DrawField(
        RenderContext context,
        UiPalette palette,
        Rectangle bounds,
        int index,
        string label,
        string value,
        bool stackedLabel)
    {
        var selected = _form.SelectedIndex == index;
        var hovered = _pointer.HoveredId == index || bounds.Contains(_input.MousePosition);
        var pressed = _pointer.IsPressed(index);
        UiTheme.DrawButton(context, bounds, palette, selected, hovered, pressed: pressed, focused: _focusVisible && selected, settings: _settings);
        var pressOffset = pressed ? 1 : 0;
        var valueScale = stackedLabel ? 1 : Math.Min(2, _typography.BodyScale);
        int valueX;
        int valueY;
        if (stackedLabel)
        {
            context.DebugText.Draw(
                new Vector2(bounds.X + 10, bounds.Y + 5 + pressOffset),
                label,
                palette.TextMuted,
                1);
            valueX = bounds.X + 10;
            valueY = bounds.Bottom - 14 + pressOffset;
        }
        else
        {
            context.DebugText.Draw(
                new Vector2(bounds.X + 12, bounds.Y + 7 + pressOffset),
                label,
                palette.TextMuted,
                _typography.CaptionScale);
            valueX = bounds.X + Math.Min(164, bounds.Width / 3);
            var availableWidth = Math.Max(6, bounds.Right - valueX - 12);
            while (valueScale > 1 && value.Length * 6 * valueScale > availableWidth)
            {
                valueScale--;
            }

            valueY = bounds.Y + (valueScale == 2 ? 15 : 18) + pressOffset;
        }

        context.DebugText.Draw(new Vector2(valueX, valueY), value, palette.Text, valueScale);
        if (selected)
        {
            var caretX = Math.Min(bounds.Right - 6, valueX + value.Length * 6 * valueScale + 2);
            context.SpriteBatch.Draw(
                context.Pixel,
                new Rectangle(caretX, valueY, Math.Max(1, valueScale), 7 * valueScale),
                palette.Accent);
        }

        _hitRegions.Add(new UiHitRegion(index, bounds));
    }

    private void DrawButton(RenderContext context, UiPalette palette, Rectangle bounds, string label, int index, bool enabled)
    {
        var selected = _form.SelectedIndex == index;
        var hovered = _pointer.HoveredId == index || bounds.Contains(_input.MousePosition);
        var pressed = _pointer.IsPressed(index);
        UiTheme.DrawButton(context, bounds, palette, selected, hovered, enabled, pressed, _focusVisible && selected, _settings);
        context.DebugText.Draw(
            new Vector2(bounds.X + 8, bounds.Y + 10 + (pressed ? 1 : 0)),
            label,
            enabled ? palette.Text : palette.TextMuted,
            bounds.Width >= label.Length * 6 * _typography.CaptionScale + 16 ? _typography.CaptionScale : 1);
        _hitRegions.Add(new UiHitRegion(index, bounds, enabled));
    }

    private void DrawProfileCard(RenderContext context, UiPalette palette, CreateWorldLayout layout)
    {
        if (layout.ProfileCard.Height < 12)
        {
            return;
        }

        UiTheme.DrawPanel(context, layout.ProfileCard, palette, 0.46f, raised: false, settings: _settings);
        var textX = layout.ProfileCard.X + 10;
        if (layout.ProfileCard.Height >= 36)
        {
            context.DebugText.Draw(
                new Vector2(textX, layout.ProfileCard.Y + 8),
                _profileLabel,
                palette.TextMuted,
                1);
        }

        context.DebugText.Draw(
            new Vector2(textX, layout.ProfileCard.Bottom - 15),
            _status,
            _form.IsSeedValid ? palette.Accent : palette.Warning,
            1);
    }

    private void AdvanceOrActivate()
    {
        if (_form.SelectedIndex == WorldCreationForm.WorldNameIndex)
        {
            _form.Focus(WorldCreationForm.SeedIndex, selectContents: true);
            _status = "SEED READY OR TYPE TO REPLACE";
            return;
        }

        if (_form.SelectedIndex == WorldCreationForm.SeedIndex)
        {
            if (_form.IsSeedValid)
            {
                _form.Focus(WorldCreationForm.CreateIndex);
                _status = "READY TO CREATE";
            }
            else
            {
                _status = "ENTER A WHOLE NUMBER";
            }

            return;
        }

        ActivateSelected();
    }

    private void ActivateSelected()
    {
        if (!_form.IsOptionEnabled(_form.SelectedIndex))
        {
            _status = "ENTER A WHOLE NUMBER";
            return;
        }

        switch (_form.SelectedIndex)
        {
            case WorldCreationForm.CreateIndex:
                CreateWorld();
                break;
            case WorldCreationForm.RandomSeedIndex:
                _form.SetRandomSeed(CreateRandomSeed());
                _status = "RANDOM SEED READY";
                break;
            case WorldCreationForm.BackIndex:
                Back();
                break;
        }
    }

    private void CreateWorld()
    {
        if (!_form.TryGetWorld(out var name, out var seed))
        {
            _status = "INVALID SEED";
            _form.Focus(WorldCreationForm.SeedIndex);
            return;
        }

        _states.ChangeState(new LoadingWorldState(_states, WorldLoadRequest.Singleplayer(seed, name)));
    }

    private void Back()
    {
        _states.ChangeState(_backState);
    }

    private static int CreateRandomSeed()
    {
        return Random.Shared.Next(1, int.MaxValue);
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
