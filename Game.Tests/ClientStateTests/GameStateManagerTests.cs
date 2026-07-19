using Game.Client.GameStates;
using Game.Client.Rendering;
using Microsoft.Xna.Framework.Content;
using Xunit;

namespace Game.Tests.ClientStateTests;

public sealed class GameStateManagerTests
{
    [Fact]
    public void LateUpdate_IsDispatchedOnceAfterAnyNumberOfFixedSteps()
    {
        using var manager = new GameStateManager();
        var state = new CountingState();
        manager.ChangeState(state);

        manager.Update(0.05);
        manager.FixedUpdate(1f / 60f);
        manager.FixedUpdate(1f / 60f);
        manager.FixedUpdate(1f / 60f);
        manager.LateUpdate(0.05);

        Assert.Equal(1, state.UpdateCount);
        Assert.Equal(3, state.FixedUpdateCount);
        Assert.Equal(1, state.LateUpdateCount);
    }

    private sealed class CountingState : IGameState
    {
        public string Name => "Counting";

        public int UpdateCount { get; private set; }

        public int FixedUpdateCount { get; private set; }

        public int LateUpdateCount { get; private set; }

        public void Initialize()
        {
        }

        public void LoadContent(ContentManager content)
        {
        }

        public void FixedUpdate(float fixedDeltaSeconds)
        {
            FixedUpdateCount++;
        }

        public void Update(double deltaSeconds)
        {
            UpdateCount++;
        }

        public void LateUpdate(double deltaSeconds)
        {
            LateUpdateCount++;
        }

        public void Draw(RenderContext context)
        {
        }

        public void Dispose()
        {
        }
    }
}
