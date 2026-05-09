using Game.Client.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;

namespace Game.Client.GameStates;

public sealed class LoadingWorldState : IGameState
{
    private static readonly string[] Stages =
    [
        "PREPARING CONTENT",
        "GENERATING TERRAIN",
        "BUILDING WORLD SESSION",
        "SPAWNING PLAYER",
        "ENTERING WORLD"
    ];

    private readonly GameStateManager _states;
    private readonly WorldLoadRequest _request;
    private double _stageTimer;
    private int _stageIndex;
    private LoadedGameSession? _session;

    public LoadingWorldState(GameStateManager states, WorldLoadRequest request)
    {
        _states = states;
        _request = request;
    }

    public string Name => "LoadingWorld";

    public void Initialize()
    {
        _stageIndex = 0;
        _stageTimer = 0;
    }

    public void LoadContent(ContentManager content)
    {
    }

    public void FixedUpdate(float fixedDeltaSeconds)
    {
    }

    public void Update(double deltaSeconds)
    {
        _stageTimer += deltaSeconds;

        if (_stageIndex == 0 && _stageTimer >= 0.12)
        {
            Advance();
            return;
        }

        if (_stageIndex == 1)
        {
            _session ??= WorldSessionFactory.CreateSingleplayer(_request.Seed, _request.WorldName);
            Advance();
            return;
        }

        if (_stageTimer >= 0.16)
        {
            Advance();
        }
    }

    public void Draw(RenderContext context)
    {
        context.SpriteBatch.Draw(context.Pixel, context.ViewportBounds, new Color(9, 12, 18));

        var centerX = context.ViewportBounds.Width / 2;
        var title = _request.Mode;
        context.DebugText.Draw(new Vector2(centerX - 180, 96), title, new Color(245, 224, 151), 4);
        context.DebugText.Draw(new Vector2(centerX - 168, 148), CurrentStageText(), Color.White, 2);

        var barWidth = Math.Min(560, context.ViewportBounds.Width - 80);
        var barHeight = 18;
        var bar = new Rectangle(centerX - barWidth / 2, 206, barWidth, barHeight);
        var progress = Math.Clamp((_stageIndex + Math.Min(1.0, _stageTimer / 0.16)) / Stages.Length, 0, 1);
        var fill = new Rectangle(bar.X, bar.Y, (int)Math.Round(bar.Width * progress), bar.Height);

        context.SpriteBatch.Draw(context.Pixel, bar, new Color(28, 35, 44));
        context.SpriteBatch.Draw(context.Pixel, fill, new Color(78, 155, 211));
        DrawBorder(context, bar, new Color(118, 142, 164), 1);

        context.DebugText.Draw(new Vector2(centerX - 164, 246), "WORLDGEN  LIGHTING  ENTITIES", new Color(144, 159, 174), 2);
    }

    public void Dispose()
    {
    }

    private string CurrentStageText()
    {
        return Stages[Math.Clamp(_stageIndex, 0, Stages.Length - 1)];
    }

    private void Advance()
    {
        _stageIndex++;
        _stageTimer = 0;

        if (_stageIndex >= Stages.Length && _session is not null)
        {
            _states.ChangeState(new PlayingState(_states, _session));
        }
    }

    private static void DrawBorder(RenderContext context, Rectangle bounds, Color color, int thickness)
    {
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(bounds.X, bounds.Y, bounds.Width, thickness), color);
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(bounds.X, bounds.Bottom - thickness, bounds.Width, thickness), color);
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(bounds.X, bounds.Y, thickness, bounds.Height), color);
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(bounds.Right - thickness, bounds.Y, thickness, bounds.Height), color);
    }
}
