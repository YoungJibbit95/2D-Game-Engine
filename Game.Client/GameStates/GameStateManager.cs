using Game.Client.Rendering;
using Microsoft.Xna.Framework.Content;

namespace Game.Client.GameStates;

public sealed class GameStateManager : IDisposable
{
    private IGameState? _currentState;
    private ContentManager? _content;

    public string CurrentStateName => _currentState?.Name ?? "None";

    public IGameState? CurrentState => _currentState;

    public void ChangeState(IGameState nextState)
    {
        ArgumentNullException.ThrowIfNull(nextState);

        _currentState?.Dispose();
        _currentState = nextState;
        _currentState.Initialize();

        if (_content is not null)
        {
            _currentState.LoadContent(_content);
        }
    }

    public void LoadContent(ContentManager content)
    {
        _content = content;
        _currentState?.LoadContent(content);
    }

    public void FixedUpdate(float fixedDeltaSeconds)
    {
        _currentState?.FixedUpdate(fixedDeltaSeconds);
    }

    public void Update(double deltaSeconds)
    {
        _currentState?.Update(deltaSeconds);
    }

    public void Draw(RenderContext context)
    {
        _currentState?.Draw(context);
    }

    public void Dispose()
    {
        _currentState?.Dispose();
    }
}
