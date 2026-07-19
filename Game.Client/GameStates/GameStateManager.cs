using Game.Client.Rendering;
using Game.Core.Diagnostics;
using Game.Core.Settings;
using Microsoft.Xna.Framework.Content;

namespace Game.Client.GameStates;

public sealed class GameStateManager : IDisposable
{
    private readonly Action _requestExit;
    private readonly Action<GameSettings> _applySettings;
    private IGameState? _currentState;
    private ContentManager? _content;

    public GameStateManager(
        Action? requestExit = null,
        Action<GameSettings>? applySettings = null,
        PerformanceProfiler? performance = null)
    {
        _requestExit = requestExit ?? (() => { });
        _applySettings = applySettings ?? (_ => { });
        Performance = performance ?? new PerformanceProfiler();
    }

    public PerformanceProfiler Performance { get; }

    public string CurrentStateName => _currentState?.Name ?? "None";

    public IGameState? CurrentState => _currentState;

    public void RequestExit()
    {
        _requestExit();
    }

    public void ApplySettings(GameSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _applySettings(settings);
    }

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

    public void LateUpdate(double deltaSeconds)
    {
        _currentState?.LateUpdate(deltaSeconds);
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
