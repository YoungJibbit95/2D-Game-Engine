using Game.Client.Input;
using Game.Client.Configuration;
using Game.Client.Rendering;
using Game.Client.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;

namespace Game.Client.GameStates;

public sealed class SettingsState : IGameState
{
    private readonly GameStateManager _states;
    private readonly IGameState _backState;
    private readonly InputManager _input = new();
    private readonly PauseMenuOverlay _settingsMenu;

    public SettingsState(GameStateManager states, IGameState backState)
    {
        _states = states;
        _backState = backState;
        _settingsMenu = new PauseMenuOverlay(
            ClientPaths.SettingsPath(),
            Back,
            Back,
            _states.RequestExit,
            title: "SETTINGS",
            resumeLabel: "BACK",
            settingsChanged: _states.ApplySettings);
    }

    public string Name => "Settings";

    public void Initialize()
    {
        _settingsMenu.Open();
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
        _settingsMenu.Update(_input, deltaSeconds);
    }

    public void Draw(RenderContext context)
    {
        context.SpriteBatch.Draw(context.Pixel, context.ViewportBounds, new Color(12, 16, 23));
        _settingsMenu.Draw(context);
    }

    public void Dispose()
    {
    }

    private void Back()
    {
        _states.ChangeState(_backState);
    }
}
