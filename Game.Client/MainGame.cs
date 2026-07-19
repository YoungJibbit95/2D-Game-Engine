using Game.Client.GameStates;
using Game.Client.Configuration;
using Game.Client.Diagnostics;
using Game.Client.Rendering;
using Game.Client.Rendering.Diagnostics;
using Game.Client.Rendering.Performance;
using Game.Core;
using Game.Core.Projects;
using Game.Core.Settings;
using Game.Core.Diagnostics;
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
    private readonly PerformanceProfiler _performance = new();
    private readonly FrameTimeTelemetryWindow _frameTimes = new(512);
    private readonly RendererMetricsTelemetryWindow _rendererMetrics = new(512);
    private readonly HighResolutionFrameLimiter _frameLimiter = new();
    private readonly GameThreadSchedulingScope _gameThreadScheduling = new();
    private GameSettings _settings;
    private readonly GameSettingsService _settingsService;
    private readonly ClientSmokeOptions? _smokeOptions;
    private readonly System.Threading.Timer? _smokeWatchdog;
    private static readonly string[] SmokeSpriteIds = ["ui/mana_star", "ui/inventory_tab", "ui/crafting_hammer"];
    private static readonly Color SmokePanelColor = new(18, 22, 31, 255);

    private SpriteBatch? _spriteBatch;
    private Texture2D? _pixel;
    private DebugTextRenderer? _debugText;
    private ClientTextureRegistry? _smokeTextures;
    private readonly HashSet<string> _smokeRenderedSpriteIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _smokeValidatedSourceSpriteIds = new(StringComparer.OrdinalIgnoreCase);
    private ClientSmokeResult? _smokeResult;
    private string? _smokeProjectId;
    private string? _smokeProjectManifestPath;
    private int _drawnFrames;
    private bool _clientDisposed;
    private long _previousDrawStartedAt;

    public MainGame(ClientSmokeOptions? smokeOptions = null)
    {
        _smokeOptions = smokeOptions;
        _settingsService = new GameSettingsService();
        _settings = LoadSettings();
        if (_smokeOptions is { UseConfiguredVideoSettings: false })
        {
            _settings = _settings with
            {
                Video = _settings.Video with
                {
                    Width = _smokeOptions.Width,
                    Height = _smokeOptions.Height,
                    Fullscreen = false,
                    VSync = false,
                    LowLatencyFramePacing = false,
                    FrameRateLimit = _smokeOptions.FrameRateLimit
                }
            };
            IsFixedTimeStep = false;
        }

        ConfigureFrameTiming(_settings.Video);

        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = _settings.Video.Width,
            PreferredBackBufferHeight = _settings.Video.Height,
            SynchronizeWithVerticalRetrace = FramePacingPolicy.Resolve(_settings.Video).SynchronizeWithVerticalRetrace,
            IsFullScreen = _settings.Video.Fullscreen
        };

        _states = new GameStateManager(Exit, ApplySettings, _performance);
        _time = new GameTimeService();
        _fixedUpdateRunner = new FixedUpdateRunner();

        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        Window.Title = "YjsE";
        Window.TextInput += OnTextInput;

        if (_smokeOptions is not null)
        {
            _smokeWatchdog = new System.Threading.Timer(
                OnSmokeTimeout,
                state: null,
                TimeSpan.FromSeconds(_smokeOptions.TimeoutSeconds),
                Timeout.InfiniteTimeSpan);
        }
    }

    public ClientSmokeResult? SmokeResult => Volatile.Read(ref _smokeResult);

    public string CurrentStateName => _states.CurrentStateName;

    protected override void Initialize()
    {
        _graphics.ApplyChanges();
        _states.ChangeState(CreateInitialState());

        base.Initialize();
    }

    private IGameState CreateInitialState()
    {
        var menu = new MainMenuState(_states, _states.RequestExit);
        return _smokeOptions?.StartState switch
        {
            ClientSmokeStartState.WorldSelect => new WorldSelectState(_states, menu),
            ClientSmokeStartState.Playing => new PlayingState(
                _states,
                openConsoleOnInitialize: _smokeOptions.OpenConsole,
                openPauseOnInitialize: _smokeOptions.OpenPause,
                forcedBiomeId: _smokeOptions.ForcedBiomeId,
                suppressDebugOverlays: !_smokeOptions.IncludeDebugOverlays,
                scriptedTraversal: _smokeOptions.ScriptedTraversal),
            _ => menu
        };
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
        _debugText = new DebugTextRenderer(_spriteBatch, _pixel);

        if (_smokeOptions is not null)
        {
            try
            {
                LoadSmokeContent();
            }
            catch (Exception exception)
            {
                TrySetSmokeResult(CreateSmokeFailureResult(exception));
                throw;
            }
        }

        _states.LoadContent(Content);
    }

    protected override void Update(GameTime gameTime)
    {
        _time.BeginFrame(gameTime);
        _performance.BeginFrame();

        using (_performance.Measure("Frame.Update", 8.0))
        {
            _states.Update(_time.FrameDeltaSeconds);
            _fixedUpdateRunner.Run(_time.FrameDeltaSeconds, FixedUpdate);
            using (_performance.Measure("Presentation.LateUpdate", 4.0))
            {
                _states.LateUpdate(_time.FrameDeltaSeconds);
            }
        }

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        if (_spriteBatch is null || _pixel is null || _debugText is null)
        {
            return;
        }

        _frameLimiter.WaitForNextFrame();
        var drawStartedAt = System.Diagnostics.Stopwatch.GetTimestamp();
        if (_previousDrawStartedAt != 0)
        {
            _frameTimes.Record(System.Diagnostics.Stopwatch.GetElapsedTime(_previousDrawStartedAt, drawStartedAt));
        }

        _previousDrawStartedAt = drawStartedAt;
        _rendererMetrics.BeginFrame(RendererMetricCounters.Capture(GraphicsDevice.Metrics));
        GraphicsDevice.Clear(Color.Black);

        _spriteBatch.Begin(samplerState: SamplerState.PointClamp);

        var context = new RenderContext(
            GraphicsDevice,
            _spriteBatch,
            _debugText,
            _pixel,
            _time,
            GraphicsDevice.Viewport.Bounds,
            _performance,
            _frameTimes,
            _rendererMetrics);

        using (_performance.Measure("Frame.Draw", 16.67))
        {
            _states.Draw(context);
            DrawSmokeAssetSample(context);
            DrawDebugOverlay(context);
            using (_performance.Measure("Render.FinalFlush", 2.0))
            {
                _spriteBatch.End();
            }
        }

        _rendererMetrics.EndFrame(RendererMetricCounters.Capture(GraphicsDevice.Metrics));
        _time.RecordDrawFrame();
        CaptureSmokeFrameWhenReady();

        base.Draw(gameTime);
    }

    protected override void UnloadContent()
    {
        DisposeLoadedClientResources();
        base.UnloadContent();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_clientDisposed)
        {
            _clientDisposed = true;
            _smokeWatchdog?.Dispose();
            _frameLimiter.Dispose();
            _gameThreadScheduling.Dispose();
            Window.TextInput -= OnTextInput;
            _states.Dispose();
            DisposeLoadedClientResources();
        }

        base.Dispose(disposing);
    }

    private void DisposeLoadedClientResources()
    {
        _smokeTextures?.Dispose();
        _smokeTextures = null;
        _debugText?.Dispose();
        _debugText = null;
        _spriteBatch?.Dispose();
        _spriteBatch = null;
        _pixel?.Dispose();
        _pixel = null;
    }

    private void DrawSmokeAssetSample(RenderContext context)
    {
        if (_smokeTextures is null)
        {
            return;
        }

        var panel = GetSmokePanelBounds(context.ViewportBounds);
        context.SpriteBatch.Draw(context.Pixel, panel, SmokePanelColor);
        context.DebugText.Draw(new Vector2(panel.X + 8, panel.Y + 7), "UI ASSET SMOKE", Color.LightSteelBlue, 1);

        for (var index = 0; index < SmokeSpriteIds.Length; index++)
        {
            var spriteId = SmokeSpriteIds[index];
            var sprite = _smokeTextures.Get(spriteId);
            if (sprite.IsPlaceholder)
            {
                continue;
            }

            var destination = GetSmokeSpriteDestination(panel, index);
            context.SpriteBatch.Draw(sprite.Texture, destination, sprite.SourceRectangle, Color.White);
            _smokeRenderedSpriteIds.Add(spriteId);
        }
    }

    private void CaptureSmokeFrameWhenReady()
    {
        if (_smokeOptions is null || SmokeResult is not null)
        {
            return;
        }

        _drawnFrames++;
        if (_drawnFrames == _smokeOptions.WarmupFrames)
        {
            _performance.Clear();
            _frameTimes.Clear();
            _rendererMetrics.Reset();
        }

        if (_drawnFrames < _smokeOptions.Frames)
        {
            return;
        }

        try
        {
            var width = GraphicsDevice.PresentationParameters.BackBufferWidth;
            var height = GraphicsDevice.PresentationParameters.BackBufferHeight;
            var pixels = new Color[width * height];
            GraphicsDevice.GetBackBufferData(pixels);

            var smokePanel = GetSmokePanelBounds(GraphicsDevice.Viewport.Bounds);
            var scenePixels = pixels.Length - smokePanel.Width * smokePanel.Height;
            var nonBlack = 0;
            var colors = new HashSet<uint>();
            for (var pixelIndex = 0; pixelIndex < pixels.Length; pixelIndex++)
            {
                var x = pixelIndex % width;
                var y = pixelIndex / width;
                if (smokePanel.Contains(x, y))
                {
                    continue;
                }

                var pixel = pixels[pixelIndex];
                if (pixel.A > 0 && pixel.R + pixel.G + pixel.B > 18)
                {
                    nonBlack++;
                }

                if (colors.Count < 256)
                {
                    colors.Add(pixel.PackedValue);
                }
            }

            string? screenshotPath = null;
            if (!string.IsNullOrWhiteSpace(_smokeOptions.ScreenshotPath))
            {
                screenshotPath = Path.GetFullPath(_smokeOptions.ScreenshotPath);
                Directory.CreateDirectory(Path.GetDirectoryName(screenshotPath)!);
                using var screenshot = new Texture2D(GraphicsDevice, width, height, false, SurfaceFormat.Color);
                screenshot.SetData(pixels);
                using var stream = File.Create(screenshotPath);
                screenshot.SaveAsPng(stream, width, height);
            }

            var minimumNonBlackPixels = Math.Max(256, scenePixels / 20);
            var telemetry = _smokeTextures?.Telemetry ?? default;
            var renderedSprites = SmokeSpriteIds
                .Where(id => _smokeRenderedSpriteIds.Contains(id))
                .ToArray();
            var validatedSourceSprites = SmokeSpriteIds
                .Where(id => _smokeValidatedSourceSpriteIds.Contains(id))
                .ToArray();
            var visibleTargetSprites = SmokeSpriteIds
                .Where((_, index) => HasVisibleSmokeTargetPixels(
                    pixels,
                    width,
                    smokePanel,
                    GetSmokeSpriteDestination(smokePanel, index)))
                .ToArray();
            var placeholderAssetCount = _smokeTextures?.PlaceholderAssetIds.Count ?? int.MaxValue;
            var explicitPlaceholderAssetCount = _smokeTextures?.PlaceholderAssetIds.Count(
                id => !string.Equals(id, _smokeTextures.FallbackAssetId, StringComparison.OrdinalIgnoreCase)) ?? int.MaxValue;
            var expectedFrameCount = _smokeTextures?.ExpectedPreloadedFrameCount ?? 0;
            var textureContractPassed = _smokeTextures?.IsPreloaded == true &&
                expectedFrameCount > 0 &&
                telemetry.FrameCount == expectedFrameCount &&
                telemetry.ResourceCount > 0 &&
                telemetry.FileLoadCount + telemetry.PlaceholderResourceCount == telemetry.ResourceCount &&
                telemetry.PlaceholderResourceCount <= 1 &&
                telemetry.InvalidResourceCount == 0 &&
                explicitPlaceholderAssetCount == 0;
            var passed = nonBlack >= minimumNonBlackPixels &&
                colors.Count >= 4 &&
                renderedSprites.Length == SmokeSpriteIds.Length &&
                validatedSourceSprites.Length == SmokeSpriteIds.Length &&
                visibleTargetSprites.Length == SmokeSpriteIds.Length &&
                textureContractPassed;
            TrySetSmokeResult(new ClientSmokeResult(
                passed,
                _drawnFrames,
                width,
                height,
                nonBlack,
                colors.Count,
                renderedSprites,
                validatedSourceSprites,
                visibleTargetSprites,
                telemetry.ResourceCount,
                telemetry.FileLoadCount,
                telemetry.FrameCount,
                telemetry.PlaceholderResourceCount,
                telemetry.InvalidResourceCount,
                telemetry.TotalResourceLoadMilliseconds,
                telemetry.TotalResourceLoadAllocatedBytes,
                telemetry.EstimatedDecodedTextureBytes,
                expectedFrameCount,
                placeholderAssetCount,
                _smokeProjectId,
                _smokeProjectManifestPath,
                GraphicsAdapter.DefaultAdapter.Description,
                screenshotPath,
                passed
                    ? null
                    : $"Expected a nonblank scene outside the synthetic sample panel, all UI sample sprites, " +
                      $"and {expectedFrameCount} preloaded texture frames without explicit asset placeholders.")
            {
                PerformanceMetrics = _performance.Snapshot(),
                FrameTiming = _frameTimes.Capture(),
                RendererMetrics = _rendererMetrics.Capture(),
                Gameplay = (_states.CurrentState as IClientGameplaySmokeTelemetryProvider)?
                    .CaptureGameplaySmokeTelemetry() ?? ClientGameplaySmokeTelemetry.NotCaptured
            });
        }
        catch (Exception exception)
        {
            TrySetSmokeResult(CreateSmokeFailureResult(exception));
        }
        finally
        {
            Exit();
        }
    }

    private void OnSmokeTimeout(object? state)
    {
        if (_smokeOptions is null)
        {
            return;
        }

        var timedOut = ClientSmokeResult.TimedOut(
            Volatile.Read(ref _drawnFrames),
            _smokeOptions.ScreenshotPath,
            _smokeOptions.TimeoutSeconds,
            _smokeProjectId,
            _smokeProjectManifestPath,
            _smokeTextures?.Telemetry ?? default,
            _smokeTextures?.ExpectedPreloadedFrameCount ?? 0,
            _smokeTextures?.PlaceholderAssetIds.Count ?? 0) with
        {
            RendererMetrics = _rendererMetrics.Capture()
        };
        if (Interlocked.CompareExchange(ref _smokeResult, timedOut, comparand: null) is null)
        {
            Exit();
        }
    }

    private void TrySetSmokeResult(ClientSmokeResult result)
    {
        Interlocked.CompareExchange(ref _smokeResult, result, comparand: null);
    }

    private Rectangle GetSmokePanelBounds(Rectangle viewportBounds)
    {
        var x = string.Equals(_states.CurrentStateName, "Playing", StringComparison.Ordinal)
            ? viewportBounds.Left + 12
            : viewportBounds.Right - 190;
        return new Rectangle(x, viewportBounds.Top + 12, 178, 74);
    }

    private void LoadSmokeContent()
    {
        var activePaths = ClientPaths.FindGameProjectPaths();
        if (!activePaths.HasManifest)
        {
            throw new InvalidDataException(
                $"Client smoke requires an active {GameProjectManifestLoader.ManifestFileName} project contract.");
        }

        var project = new GameProjectContentLoader().Load(activePaths.ProjectRoot);
        _smokeProjectId = project.Manifest.Id;
        _smokeProjectManifestPath = project.Paths.ManifestPath;
        if (project.Content.Report.HasErrors)
        {
            var details = string.Join(
                Environment.NewLine,
                project.Content.Report.Issues.Select(issue => $"{issue.ContentKind}/{issue.ContentId}: {issue.Message}"));
            throw new InvalidDataException("Client smoke content validation failed." + Environment.NewLine + details);
        }

        var missingSamples = SmokeSpriteIds
            .Where(spriteId => !project.Content.Database.SpriteAssets.TryGetById(spriteId, out _))
            .ToArray();
        if (missingSamples.Length > 0)
        {
            throw new InvalidDataException(
                $"Client smoke project is missing required sample sprites: {string.Join(", ", missingSamples)}.");
        }

        _smokeTextures = new ClientTextureRegistry(
            GraphicsDevice,
            project.Paths.ContentRoot,
            project.Content.Database.SpriteAssets);
        _smokeTextures.PreloadAll();
        ValidateSmokeTexturePreload(_smokeTextures);
        ValidateSmokeSpriteSourcePixels(_smokeTextures);
    }

    private ClientSmokeResult CreateSmokeFailureResult(Exception exception)
    {
        return ClientSmokeResult.CaptureFailed(
            Volatile.Read(ref _drawnFrames),
            _smokeOptions?.ScreenshotPath,
            exception,
            _smokeProjectId,
            _smokeProjectManifestPath,
            _smokeTextures?.Telemetry ?? default,
            _smokeTextures?.ExpectedPreloadedFrameCount ?? 0,
            _smokeTextures?.PlaceholderAssetIds.Count ?? 0) with
        {
            PerformanceMetrics = _performance.Snapshot(),
            FrameTiming = _frameTimes.Capture(),
            RendererMetrics = _rendererMetrics.Capture(),
            Gameplay = (_states.CurrentState as IClientGameplaySmokeTelemetryProvider)?
                .CaptureGameplaySmokeTelemetry() ?? ClientGameplaySmokeTelemetry.NotCaptured
        };
    }

    private static void ValidateSmokeTexturePreload(ClientTextureRegistry textures)
    {
        var telemetry = textures.Telemetry;
        if (!textures.IsPreloaded || telemetry.FrameCount != textures.ExpectedPreloadedFrameCount)
        {
            throw new InvalidDataException(
                $"Client smoke preloaded {telemetry.FrameCount} texture frames, " +
                $"expected {textures.ExpectedPreloadedFrameCount}.");
        }

        var explicitPlaceholders = textures.PlaceholderAssetIds
            .Where(id => !string.Equals(id, textures.FallbackAssetId, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (explicitPlaceholders.Length > 0)
        {
            throw new InvalidDataException(
                $"Client smoke resolved explicit placeholder textures for: {string.Join(", ", explicitPlaceholders)}.");
        }

        if (telemetry.InvalidResourceCount > 0 ||
            telemetry.PlaceholderResourceCount > 1 ||
            telemetry.ResourceCount <= 0 ||
            telemetry.FileLoadCount + telemetry.PlaceholderResourceCount != telemetry.ResourceCount)
        {
            throw new InvalidDataException(
                $"Client smoke loaded {telemetry.FileLoadCount} files for {telemetry.ResourceCount} texture resources.");
        }
    }

    private void ValidateSmokeSpriteSourcePixels(ClientTextureRegistry textures)
    {
        foreach (var spriteId in SmokeSpriteIds)
        {
            var sprite = textures.Get(spriteId);
            var source = sprite.SourceRectangle;
            var pixels = new Color[source.Width * source.Height];
            sprite.Texture.GetData(0, source, pixels, 0, pixels.Length);
            if (!pixels.Any(pixel => pixel.A > 0))
            {
                throw new InvalidDataException(
                    $"Client smoke sprite '{spriteId}' has no visible alpha pixels in source rectangle {source}.");
            }

            _smokeValidatedSourceSpriteIds.Add(spriteId);
        }
    }

    private static Rectangle GetSmokeSpriteDestination(Rectangle panel, int index)
    {
        return new Rectangle(panel.X + 10 + index * 54, panel.Y + 28, 38, 38);
    }

    private static bool HasVisibleSmokeTargetPixels(
        IReadOnlyList<Color> pixels,
        int width,
        Rectangle panel,
        Rectangle target)
    {
        for (var y = target.Top; y < target.Bottom; y++)
        {
            var panelBackground = pixels[y * width + panel.Left + 2];
            for (var x = target.Left; x < target.Right; x++)
            {
                var pixel = pixels[y * width + x];
                if (pixel.A > 0 && pixel.PackedValue != panelBackground.PackedValue)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private void FixedUpdate(float fixedDeltaSeconds)
    {
        using (_performance.Measure("Simulation.FixedUpdate", 6.0))
        {
            _states.FixedUpdate(fixedDeltaSeconds);
        }
    }

    private void DrawDebugOverlay(RenderContext context)
    {
        if (_smokeOptions is { IncludeDebugOverlays: false } ||
            !_settings.Rendering.DrawDebugOverlays ||
            !_settings.Debug.ShowDebugOverlay)
        {
            return;
        }

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
        ConfigureFrameTiming(settings.Video);

        var video = settings.Video;
        var pacing = FramePacingPolicy.Resolve(video);
        var needsApply =
            _graphics.PreferredBackBufferWidth != video.Width ||
            _graphics.PreferredBackBufferHeight != video.Height ||
            _graphics.IsFullScreen != video.Fullscreen ||
            _graphics.SynchronizeWithVerticalRetrace != pacing.SynchronizeWithVerticalRetrace;

        _graphics.PreferredBackBufferWidth = video.Width;
        _graphics.PreferredBackBufferHeight = video.Height;
        _graphics.IsFullScreen = video.Fullscreen;
        _graphics.SynchronizeWithVerticalRetrace = pacing.SynchronizeWithVerticalRetrace;

        if (needsApply)
        {
            _graphics.ApplyChanges();
        }
    }

    private void ConfigureFrameTiming(VideoSettings video)
    {
        IsFixedTimeStep = false;
        _frameLimiter.Configure(FramePacingPolicy.Resolve(video).SoftwareFrameRateLimit);
    }
}
