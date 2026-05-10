using Game.Client.Rendering;
using Game.Client.UI;
using Game.Core.UI.Animation;
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
    private readonly UiAnimationPlayer _introAnimation = new();

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
        _introAnimation.Play(UiAnimationClip.SlideFadeIn(0.22f, -12f));
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
        _introAnimation.Update((float)deltaSeconds);

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
        var palette = UiTheme.Resolve();
        var offsetY = (int)MathF.Round(_introAnimation.GetValue(UiAnimationProperty.OffsetY, 0f));
        context.SpriteBatch.Draw(context.Pixel, context.ViewportBounds, palette.Backdrop);

        var centerX = context.ViewportBounds.Width / 2;
        var title = _request.Mode;
        context.DebugText.Draw(new Vector2(centerX - 180, 96 + offsetY), title, palette.Accent, 4);
        context.DebugText.Draw(new Vector2(centerX - 168, 148 + offsetY), CurrentStageText(), palette.Text, 2);

        var barWidth = Math.Min(560, context.ViewportBounds.Width - 80);
        var barHeight = 18;
        var bar = new Rectangle(centerX - barWidth / 2, 206 + offsetY, barWidth, barHeight);
        var progress = Math.Clamp((_stageIndex + Math.Min(1.0, _stageTimer / 0.16)) / Stages.Length, 0, 1);

        UiTheme.DrawProgressBar(context, bar, (float)progress, palette);

        context.DebugText.Draw(new Vector2(centerX - 164, 246 + offsetY), "WORLDGEN  LIGHTING  ENTITIES", palette.TextMuted, 2);
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

}
