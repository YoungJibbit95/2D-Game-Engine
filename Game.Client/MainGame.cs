using Game.Client.GameStates;
using Game.Client.Configuration;
using Game.Client.Rendering;
using Game.Core;
using Game.Core.Settings;
using System.Globalization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Game.Client;

public sealed class MainGame : Microsoft.Xna.Framework.Game
{
    private readonly GraphicsDeviceManager _graphics;
    private readonly GameStateManager _states;
    private readonly GameTimeService _time;
    private readonly FixedUpdateRunner _fixedUpdateRunner;
    private GameSettings _settings;
    private readonly GameSettingsService _settingsService;

    private SpriteBatch? _spriteBatch;
    private Texture2D? _pixel;
    private DebugTextRenderer? _debugText;

    public MainGame()
    {
        _settingsService = new GameSettingsService();
        _settings = LoadSettings();
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = _settings.Video.Width,
            PreferredBackBufferHeight = _settings.Video.Height,
            SynchronizeWithVerticalRetrace = _settings.Video.VSync,
            IsFullScreen = _settings.Video.Fullscreen
        };

        _states = new GameStateManager(Exit, ApplySettings);
        _time = new GameTimeService();
        _fixedUpdateRunner = new FixedUpdateRunner();

        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        Window.Title = "YjsE";
        Window.TextInput += OnTextInput;
    }

    protected override void Initialize()
    {
        _graphics.ApplyChanges();
        _states.ChangeState(new MainMenuState(_states, _states.RequestExit));

        base.Initialize();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
        _debugText = new DebugTextRenderer(_spriteBatch, _pixel);

        _states.LoadContent(Content);
    }

    protected override void Update(GameTime gameTime)
    {
        _time.BeginFrame(gameTime);

        _states.Update(_time.FrameDeltaSeconds);
        _fixedUpdateRunner.Run(_time.FrameDeltaSeconds, FixedUpdate);

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        if (_spriteBatch is null || _pixel is null || _debugText is null)
        {
            return;
        }

        GraphicsDevice.Clear(Color.Black);

        _spriteBatch.Begin(samplerState: SamplerState.PointClamp);

        var context = new RenderContext(
            GraphicsDevice,
            _spriteBatch,
            _debugText,
            _pixel,
            _time,
            GraphicsDevice.Viewport.Bounds);

        _states.Draw(context);
        DrawDebugOverlay(context);

        _spriteBatch.End();
        _time.RecordDrawFrame();

        base.Draw(gameTime);
    }

    private void FixedUpdate(float fixedDeltaSeconds)
    {
        _states.FixedUpdate(fixedDeltaSeconds);
    }

    private void DrawDebugOverlay(RenderContext context)
    {
        var debug = context.DebugText;
        var fpsText = _time.FramesPerSecond.ToString("0.0", CultureInfo.InvariantCulture);
        debug.Draw(new Vector2(12, 12), $"FPS: {fpsText}", Color.LimeGreen, 2);
        debug.Draw(new Vector2(12, 34), $"STATE: {_states.CurrentStateName}", Color.White, 2);
    }

    private void OnTextInput(object? sender, TextInputEventArgs e)
    {
        if (_states.CurrentState is ITextInputReceiver receiver)
        {
            receiver.OnTextInput(e.Character);
        }
    }

    private bool CurrentStateCapturesKeyboard()
    {
        return _states.CurrentState is IKeyboardCaptureState { CapturesKeyboard: true };
    }

    private GameSettings LoadSettings()
    {
        try
        {
            return _settingsService.LoadOrCreate(ClientPaths.SettingsPath());
        }
        catch (InvalidDataException)
        {
            return GameSettings.CreateDefault();
        }
    }

    private void ApplySettings(GameSettings settings)
    {
        _settings = settings;

        var video = settings.Video;
        var needsApply =
            _graphics.PreferredBackBufferWidth != video.Width ||
            _graphics.PreferredBackBufferHeight != video.Height ||
            _graphics.IsFullScreen != video.Fullscreen ||
            _graphics.SynchronizeWithVerticalRetrace != video.VSync;

        _graphics.PreferredBackBufferWidth = video.Width;
        _graphics.PreferredBackBufferHeight = video.Height;
        _graphics.IsFullScreen = video.Fullscreen;
        _graphics.SynchronizeWithVerticalRetrace = video.VSync;

        if (needsApply)
        {
            _graphics.ApplyChanges();
        }
    }
}
