using Game.Client.Rendering;
using Microsoft.Xna.Framework.Content;

namespace Game.Client.GameStates;

public interface IGameState : IDisposable
{
    string Name { get; }

    void Initialize();

    void LoadContent(ContentManager content);

    void FixedUpdate(float fixedDeltaSeconds);

    void Update(double deltaSeconds);

    void Draw(RenderContext context);
}
