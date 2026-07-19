using Game.Client.DeveloperTools;
using Game.Client.Audio;
using Game.Client.Diagnostics;
using Game.Client.Input;
using Game.Client.Rendering;
using Game.Client.Rendering.Effects;
using Game.Client.Rendering.Character;
using Game.Client.Rendering.Entities;
using Game.Client.Rendering.Graph;
using Game.Client.Rendering.Lighting;
using Game.Client.Rendering.Performance;
using Game.Core.Characters;
using Game.Client.UI;
using Game.Core;
using Game.Core.Actions;
using Game.Core.Audio;
using Game.Client.Configuration;
using Game.Core.Commands;
using Game.Core.Data;
using Game.Core.Diagnostics;
using Game.Core.Entities;
using Game.Core.Equipment;
using Game.Core.Events;
using Game.Core.Farming;
using Game.Core.Feedback;
using Game.Core.Interaction;
using Game.Core.Inventory;
using Game.Core.Items;
using Game.Core.Lighting;
using Game.Core.Physics;
using Game.Core.Projectiles;
using Game.Core.Runtime;
using Game.Core.Saving;
using Game.Core.Sessions;
using Game.Core.Settings;
using Game.Core.Time;
using Game.Core.World;
using Game.Core.World.Generation;
using Game.Core.World.Streaming;
using Game.Core.World.TileEntities;
using System.Globalization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Graphics;

namespace Game.Client.GameStates;

public sealed class PlayingState : IGameState, ITextInputReceiver, IKeyboardCaptureState, IClientGameplaySmokeTelemetryProvider
{
    private readonly InputManager _input = new();
    private readonly Camera2D _camera = new();
    private readonly TilemapRenderer _tilemapRenderer = new();
    private readonly ParallaxBackgroundRenderer _backgroundRenderer = new();
    private readonly LightingRenderer _lightingRenderer = new();
    private readonly ScreenSpaceEffectsRenderer _screenSpaceEffects = new();
    private readonly PixelAtmosphereRenderer _atmosphereRenderer = new();
    private readonly ScreenSpaceLight[] _visibleLights = new ScreenSpaceLight[32];
    private readonly PresentationWorkScheduler _presentationWork = new(4);
    private readonly PresentationFrameBudget _presentationFrameBudget = new(6);
    private readonly PresentationWorkHandle _lightingWork;
    private readonly PresentationWorkHandle _reflectionWork;
    private readonly PresentationWorkHandle _atmosphereWork;
    private readonly PresentationWorkHandle _sceneCaptureWork;
    private readonly TileCollisionResolver _collisionResolver = new();
    private readonly InteractionTargetingSystem _targeting = new();
    private ChunkStreamingService _streaming = new();
    private readonly GameSaveCoordinator _saves = new();
    private readonly EngineDebugSnapshotBuilder _debugSnapshots = new();
    private readonly DeveloperConsoleAdapter _developerConsole = new();
    private readonly HudOverlay _hud = new();
    private readonly InventoryOverlay _inventoryOverlay = new();
    private readonly CraftingOverlay _craftingOverlay = new();
    private readonly CharacterEditorOverlay _characterEditorOverlay = new();
    private readonly DebugConsoleOverlay _debugConsole = new();
    private readonly PerformanceOverlay _performanceOverlay = new();
    private readonly EventJournalOverlay _eventJournalOverlay = new();
    private readonly GameplayFeedbackOverlay _gameplayFeedback = new();
    private readonly GameplayParticleSystem _particles = new();
    private readonly GameplayFeedbackCue[] _feedbackCueDrainBuffer = new GameplayFeedbackCue[64];
    private readonly GameplayAudioCue[] _audioCueDrainBuffer = new GameplayAudioCue[32];
    private readonly Game.Client.Diagnostics.SimulationPhaseOverlay _simulationPhaseOverlay = new();
    private readonly Wave04PlayerCharacterRenderer _playerCharacterRenderer = new();
    private readonly EntityVisualPipeline _entityVisuals = new();
    private readonly EntityVisualDrawCommandExecutor _entityVisualDrawExecutor = new();
    private readonly RenderPassGraph _renderGraph = new(16, 16, 64);
    private readonly SoundEffectRegistry _soundEffects = new();
    private readonly AudioManager _audio;
    private readonly PauseMenuOverlay _pauseMenu;
    private readonly GameStateManager _states;
    private readonly EntityFactory _entityFactory;
    private EquipmentLoadout _equipmentLoadout = new();
    private readonly LoadedGameSession? _initialSession;
    private readonly bool _openConsoleOnInitialize;
    private readonly bool _openPauseOnInitialize;
    private readonly string? _forcedBiomeId;
    private readonly bool _suppressDebugOverlays;
    private readonly bool _scriptedTraversal;

    private LoadedGameSession? _session;
    private Func<int, int>? _backgroundSurfaceHeightResolver;
    private GameSimulation? _simulation;
    private GameFrameSnapshot? _frameSnapshot;
    private World? _world;
    private PlayerEntity? _player;
    private EntityManager _entities = new();
    private GameEventBus _events = new();
    private GameEventJournal? _eventJournal;
    private GameplayFeedbackRouter? _feedbackRouter;
    private WorldTime _worldTime = new();
    private GameContentDatabase? _content;
    private PlayerInventory? _inventory;
    private TileEntityManager _tileEntities = new();
    private FarmPlotManager _farmPlots = new();
    private TilePos? _hoverTile;
    private TilePos? _interactionTile;
    private Rectangle _lastViewportBounds = new(0, 0, 1280, 720);
    private EngineDebugSnapshot? _debugSnapshot;
    private double _debugSnapshotElapsed;
    private bool _showGrid;
    private int _selectedHotbarSlot;
    private string? _worldSaveDirectory;
    private ClientTextureRegistry? _textures;
    private GraphicsDevice? _graphicsDevice;
    private ChunkStreamingUpdateResult _lastStreaming = ChunkStreamingUpdateResult.Empty;
    private double _streamingUpdateElapsedSeconds;
    private ChunkStreamingViewKey _lastStreamingViewKey;
    private bool _hasStreamingViewKey;
    private GameSaveResult? _lastSave;
    private PlayerCommand _pendingPlayerCommand = PlayerCommand.None;
    private readonly FixedStepButtonLatch _jumpInputLatch = new();
    private PlayerItemUseRequest _pendingItemUse = PlayerItemUseRequest.Inactive;
    private bool _hasPendingItemUse;
    private bool _pendingItemUseContinuous;
    private bool _playerFacingLeft;
    private bool _guardToggleActive;
    private bool _playerWasDamageFlashing;
    private ulong _lastAttackVisualInstanceId;
    private SoundscapeController? _soundscape;
    private PresentationCadenceConfiguration _presentationCadence;
    private bool _hasPresentationCadence;
    private bool _captureSceneThisFrame = true;
    private CompiledRenderGraphPlan _compiledRenderGraph;
    private bool _renderGraphConfigured;
    private bool _renderGraphLightingEnabled;

    public PlayingState(
        GameStateManager states,
        LoadedGameSession? loadedSession = null,
        bool openConsoleOnInitialize = false,
        bool openPauseOnInitialize = false,
        string? forcedBiomeId = null,
        bool suppressDebugOverlays = false,
        bool scriptedTraversal = false)
    {
        _states = states;
        _initialSession = loadedSession;
        _openConsoleOnInitialize = openConsoleOnInitialize;
        _openPauseOnInitialize = openPauseOnInitialize;
        _forcedBiomeId = forcedBiomeId;
        _suppressDebugOverlays = suppressDebugOverlays;
        _scriptedTraversal = scriptedTraversal;
        _entityFactory = new EntityFactory(_collisionResolver);
        _audio = new AudioManager(_soundEffects);
        _pauseMenu = new PauseMenuOverlay(
            ClientPaths.SettingsPath(),
            resume: static () => { },
            mainMenu: ReturnToMainMenu,
            exitGame: _states.RequestExit,
            settingsChanged: _states.ApplySettings);
        _craftingOverlay.CraftingResolved += OnCraftingResolved;
        _lightingWork = _presentationWork.Register(CreatePresentationSchedule(60, 30, 3d));
        _reflectionWork = _presentationWork.Register(CreatePresentationSchedule(30, 45, 6d));
        _atmosphereWork = _presentationWork.Register(CreatePresentationSchedule(30, 45, 8d));
        _sceneCaptureWork = _presentationWork.Register(CreatePresentationSchedule(45, 20, 4d));
    }

    public string Name => "Playing";

    public ClientGameplaySmokeTelemetry CaptureGameplaySmokeTelemetry()
    {
        if (_frameSnapshot is not { } frame)
        {
            return ClientGameplaySmokeTelemetry.NotCaptured;
        }

        var visible = _camera.VisibleWorldRect;
        var visibleBounds = new RectI(visible.X, visible.Y, visible.Width, visible.Height);
        var visibleCount = 0;
        var visibleEnemies = 0;
        var contentIds = new List<string>();
        foreach (var entity in frame.Entities)
        {
            if (!entity.IsActive || !entity.Bounds.Intersects(visibleBounds))
            {
                continue;
            }

            visibleCount++;
            if (entity.Kind == EntityFrameKind.Enemy)
            {
                visibleEnemies++;
            }

            if (!contentIds.Contains(entity.ContentId, StringComparer.OrdinalIgnoreCase))
            {
                contentIds.Add(entity.ContentId);
            }
        }

        contentIds.Sort(StringComparer.OrdinalIgnoreCase);
        return new ClientGameplaySmokeTelemetry(
            true,
            frame.Hud.ActiveEntities,
            frame.Hud.ActiveEnemies,
            visibleCount,
            visibleEnemies,
            contentIds.ToArray());
    }

    public bool CapturesKeyboard => _debugConsole.IsOpen || _pauseMenu.IsOpen || _inventoryOverlay.IsOpen || _craftingOverlay.IsOpen || _characterEditorOverlay.IsOpen;

    public void Initialize()
    {
        var session = _initialSession ?? WorldSessionFactory.CreateSingleplayer(seed: 1337, worldName: "Debug World");
        _session = session;
        _simulation = session.Simulation;
        _frameSnapshot = _simulation.LatestSnapshot;
        _content = session.Content;
        if (_content.RuntimeAnimations?.TryGetCharacter("player.wave06", out var playerAnimation) == true)
        {
            _playerCharacterRenderer.Configure(playerAnimation);
        }
        _entityVisuals.Configure(_content.RuntimeAnimations, TryResolveEntitySpriteId);
        _inventory = session.Inventory;
        _world = session.World;
        _backgroundSurfaceHeightResolver = session.InfiniteChunkGenerator is { } backgroundGenerator &&
            session.WorldGenerationProfile is { } backgroundProfile
                ? backgroundGenerator.CreateSurfaceHeightResolver(backgroundProfile, session.World.Metadata.Seed)
                : null;
        _player = session.Player;
        _entities = session.Entities;
        _events = session.Events;
        _eventJournal?.Dispose();
        _eventJournal = new GameEventJournal(_events, capacity: 256);
        _feedbackRouter?.Dispose();
        _feedbackRouter = new GameplayFeedbackRouter(
            _events,
            ResolveFeedbackEntityPosition,
            capacity: 384,
            rareItemResolver: IsRareFeedbackItem,
            focusPositionResolver: ResolveFeedbackFocusPosition);
        _worldTime = session.WorldTime;
        _worldSaveDirectory = session.WorldSaveDirectory;
        _tileEntities = session.TileEntities ?? new TileEntityManager();
        _farmPlots = session.FarmPlots ?? new FarmPlotManager();
        _equipmentLoadout = session.EquipmentLoadout ?? new EquipmentLoadout();
        ApplyForcedBiomeSpawn();
        _characterEditorOverlay.SetAppearance(_content, session.CharacterAppearance ?? new CharacterAppearance());
        _selectedHotbarSlot = _inventory.SelectedHotbarSlot;
        if (_openConsoleOnInitialize)
        {
            _debugConsole.State.Open();
        }
        if (_openPauseOnInitialize)
        {
            _pauseMenu.Open();
        }
        _streaming.CancelPendingJobs();
        _streaming = new ChunkStreamingService(generator: session.InfiniteChunkGenerator);
        _hasStreamingViewKey = false;
        _streamingUpdateElapsedSeconds = 0d;

        _camera.Position = new Vector2(_player.Body.Center.X, _player.Body.Center.Y);
        _camera.Zoom = _pauseMenu.Settings.Gameplay.CameraZoom;
        _camera.Recalculate(_lastViewportBounds);
    }

    private void ApplyForcedBiomeSpawn()
    {
        if (string.IsNullOrWhiteSpace(_forcedBiomeId) || _simulation is null || _player is null)
        {
            return;
        }

        var livingWorld = _simulation.LivingWorld;
        var profile = livingWorld.Profile;
        _worldTime.SetDay();
        for (var regionIndex = -256; regionIndex <= 256; regionIndex++)
        {
            var regionStart = (long)regionIndex * profile.RegionWidthTiles;
            var centerTileX = (int)Math.Clamp(
                regionStart + profile.RegionWidthTiles / 2L,
                int.MinValue,
                int.MaxValue);
            var region = livingWorld.ResolveRegion(centerTileX);
            if (string.Equals(region.Biome.Id, _forcedBiomeId, StringComparison.OrdinalIgnoreCase))
            {
                var surfaceY = _session?.InfiniteChunkGenerator is { } generator &&
                    _session.WorldGenerationProfile is { } worldProfile
                        ? generator.GetSurfaceHeightAt(
                            worldProfile,
                            _world?.Metadata.Seed ?? 0,
                            centerTileX)
                        : profile.SurfaceBaseY;
                EnsureForcedSpawnChunks(centerTileX, surfaceY);
                SetForcedPlayerTile(centerTileX, Math.Max(2, surfaceY - 3));
                return;
            }

            for (var caveIndex = 0; caveIndex < region.Caves.Count; caveIndex++)
            {
                var cave = region.Caves[caveIndex];
                if (!string.Equals(cave.ProfileId, _forcedBiomeId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var caveTileX = (int)Math.Clamp(cave.CenterTileX, int.MinValue, int.MaxValue);
                EnsureForcedSpawnChunks(caveTileX, cave.CenterTileY);
                SetForcedPlayerTile(caveTileX, cave.CenterTileY);
                return;
            }
        }

        throw new InvalidDataException($"Could not find forced smoke biome '{_forcedBiomeId}' in the search window.");
    }

    private void EnsureForcedSpawnChunks(int tileX, int tileY)
    {
        if (_world is null ||
            _session?.InfiniteChunkGenerator is not { } generator ||
            _session.WorldGenerationProfile is not { } profile)
        {
            return;
        }

        var center = CoordinateUtils.TileToChunk(new TilePos(tileX, tileY));
        var maximumChunkY = Math.Max(0, (_world.HeightTiles - 1) / GameConstants.ChunkSize);
        for (var chunkY = Math.Max(0, center.Y - 2); chunkY <= Math.Min(maximumChunkY, center.Y + 2); chunkY++)
        {
            for (var chunkX = center.X - 2; chunkX <= center.X + 2; chunkX++)
            {
                generator.EnsureChunk(_world, profile, new ChunkPos(chunkX, chunkY));
            }
        }
    }

    private void SetForcedPlayerTile(int tileX, int tileY)
    {
        if (_player is null)
        {
            return;
        }

        _player.Body.Position = new System.Numerics.Vector2(
            tileX * (float)GameConstants.TileSize,
            tileY * (float)GameConstants.TileSize);
        _player.Body.Velocity = System.Numerics.Vector2.Zero;
    }

    public void LoadContent(ContentManager content)
    {
        if (_content is null)
        {
            return;
        }

        var graphicsService = content.ServiceProvider.GetService(typeof(IGraphicsDeviceService)) as IGraphicsDeviceService
            ?? throw new InvalidOperationException("MonoGame graphics device service is unavailable.");
        _graphicsDevice = graphicsService.GraphicsDevice;
        _tilemapRenderer.Dispose();
        _textures?.Dispose();
        var textureContentRoot = _session?.ProjectPaths?.ContentRoot ?? ClientPaths.FindGameDataRoot();
        _textures = new ClientTextureRegistry(
            graphicsService.GraphicsDevice,
            textureContentRoot,
            _content.SpriteAssets);
        _textures.PreloadAll();
        _tilemapRenderer.ConfigureContent(_textures, _content.Tiles);
        _entityVisualDrawExecutor.PrepareResources(graphicsService.GraphicsDevice);
        var soundscapeDirectory = Path.Combine(textureContentRoot, "soundscapes");
        var soundscapes = new SoundscapeDefinitionJsonLoader().LoadCatalogFromDirectory(soundscapeDirectory);
        _soundscape = new SoundscapeController(
            new SoundscapeCommandPlanner(new SoundscapeResolver(soundscapes)),
            _audio);
        AudioSettingsAdapter.Apply(_audio, _pauseMenu.Settings.Audio);
        EnsurePresentationResources(_pauseMenu.Settings);
    }

    public void FixedUpdate(float fixedDeltaSeconds)
    {
        if (_pauseMenu.IsOpen || _simulation is null)
        {
            SuppressGameplayInput();
            return;
        }

        var settings = _pauseMenu.Settings;
        var visibleHalfWidth = Math.Max(
            1,
            (int)MathF.Ceiling(_camera.VisibleWorldRect.Width / (GameConstants.TileSize * 2f)));
        var visibleHalfHeight = Math.Max(
            1,
            (int)MathF.Ceiling(_camera.VisibleWorldRect.Height / (GameConstants.TileSize * 2f)));
        var spawnMinimumTiles = Math.Max(
            1,
            (int)MathF.Ceiling(settings.Gameplay.SpawnMinimumDistancePixels / GameConstants.TileSize));
        var spawnMaximumTiles = Math.Max(
            spawnMinimumTiles,
            (int)MathF.Ceiling(settings.Gameplay.SpawnMaximumDistancePixels / GameConstants.TileSize));
        if (_simulation.Options.AutoPickupItems != settings.Gameplay.AutoPickupItems ||
            _simulation.Options.EnablePhaseTelemetry != settings.Debug.ShowPerformanceProfiler ||
            _simulation.Options.MaxActiveEnemies != settings.Gameplay.MaxActiveEnemies ||
            _simulation.Options.EnemySpawnRateMultiplier != settings.Gameplay.EnemySpawnRateMultiplier ||
            _simulation.Options.SpawnMinimumDistanceTiles != spawnMinimumTiles ||
            _simulation.Options.SpawnMaximumDistanceTiles != spawnMaximumTiles ||
            _simulation.Options.SpawnVisibleHalfWidthTiles != visibleHalfWidth ||
            _simulation.Options.SpawnVisibleHalfHeightTiles != visibleHalfHeight ||
            _simulation.Options.SpawnOutsideViewportOnly != settings.Gameplay.SpawnOutsideViewportOnly)
        {
            _simulation.ConfigureOptions(_simulation.Options with
            {
                AutoPickupItems = settings.Gameplay.AutoPickupItems,
                EnablePhaseTelemetry = settings.Debug.ShowPerformanceProfiler,
                MaxActiveEnemies = settings.Gameplay.MaxActiveEnemies,
                EnemySpawnRateMultiplier = settings.Gameplay.EnemySpawnRateMultiplier,
                SpawnMinimumDistanceTiles = spawnMinimumTiles,
                SpawnMaximumDistanceTiles = spawnMaximumTiles,
                SpawnVisibleHalfWidthTiles = visibleHalfWidth,
                SpawnVisibleHalfHeightTiles = visibleHalfHeight,
                SpawnOutsideViewportOnly = settings.Gameplay.SpawnOutsideViewportOnly
            });
        }

        var itemUse = _hasPendingItemUse ? _pendingItemUse : PlayerItemUseRequest.Inactive;
        var result = _simulation.Tick(_pendingPlayerCommand, fixedDeltaSeconds, itemUse);
        if (!_scriptedTraversal)
        {
            _jumpInputLatch.ConsumePress();
            _pendingPlayerCommand = _pendingPlayerCommand with
            {
                WantsJump = _jumpInputLatch.IsActiveForFixedStep
            };
        }

        _frameSnapshot = result.Snapshot;
        _entityVisuals.Prepare(result.Snapshot, _camera.VisibleWorldRect);
        AudioSettingsAdapter.Apply(_audio, settings.Audio);
        _soundscape?.Update(
            result.Snapshot.LivingWorld,
            result.Snapshot.WorldTime,
            fixedDeltaSeconds,
            result.Snapshot.Player.Position);
        _particles.EmitAmbient(new AmbientParticleFrame(
            result.Snapshot.LivingWorld,
            _camera.VisibleWorldRect,
            result.TickNumber,
            settings.Rendering.ParticleQuality,
            _world));
        if (itemUse.IsActive)
        {
            _gameplayFeedback.Observe(result.ItemUse);
        }

        if (!_pendingItemUseContinuous)
        {
            _pendingItemUse = PlayerItemUseRequest.Inactive;
            _hasPendingItemUse = false;
        }

        UpdatePlayerAnimation();
        DrainFeedbackCues();
        _particles.Update(fixedDeltaSeconds);
    }

    public void LateUpdate(double deltaSeconds)
    {
        if (_frameSnapshot is { } frame)
        {
            var settings = _pauseMenu.Settings;
            if (_world is not null)
            {
                using (_states.Performance.Measure("Presentation.ChunkPrepare", 1.0))
                {
                    _tilemapRenderer.PrepareVisible(_world, _camera);
                }

                using (_states.Performance.Measure("Presentation.LiquidPrepare", 0.65))
                {
                    var waterPalette = WaterPresentationPaletteCatalog.Resolve(frame.LivingWorld.BiomeId);
                    _tilemapRenderer.PrepareLiquidPresentation(
                        _world,
                        _camera,
                        _lastViewportBounds,
                        waterPalette,
                        settings.Rendering.DrawLiquids ? settings.Rendering.LiquidOpacity : 0f,
                        frame.TickNumber);
                }
            }

            if (!settings.Rendering.AdaptivePresentationCadence)
            {
                _captureSceneThisFrame = true;
                PreparePresentationFrames(frame, settings, true, true, true);
                return;
            }

            EnsurePresentationCadence(settings.Rendering);
            _presentationWork.AdvanceFrame(Math.Max(0d, deltaSeconds));
            _presentationFrameBudget.Reset(ResolvePresentationFrameBudget(settings.Rendering));
            var state = new PresentationWorkState(
                frame.TickNumber,
                _camera.Position.X,
                _camera.Position.Y,
                _camera.Zoom);
            var prepareLighting = _presentationWork.TrySchedule(
                _lightingWork,
                state with { IsEnabled = settings.Rendering.DrawLightingOverlay },
                EstimateLightingWork(settings.Rendering),
                _presentationFrameBudget,
                out _);
            var prepareReflections = _presentationWork.TrySchedule(
                _reflectionWork,
                state with
                {
                    IsEnabled = settings.Rendering.ScreenSpaceReflections &&
                        settings.Rendering.ReflectionQuality > 0 &&
                        settings.Rendering.ReflectionStrength > 0.001f
                },
                EstimateReflectionWork(settings.Rendering),
                _presentationFrameBudget,
                out _);
            var prepareAtmosphere = _presentationWork.TrySchedule(
                _atmosphereWork,
                state,
                2,
                _presentationFrameBudget,
                out _);
            PreparePresentationFrames(
                frame,
                settings,
                prepareLighting,
                prepareReflections,
                prepareAtmosphere);

            var requiresBackdrop = RequiresBackdropBlur(settings);
            _captureSceneThisFrame = _presentationWork.TrySchedule(
                _sceneCaptureWork,
                state with
                {
                    IsDirty = requiresBackdrop,
                    IsEnabled = _screenSpaceEffects.ShouldCaptureScene
                },
                EstimateSceneCaptureWork(settings.Rendering),
                _presentationFrameBudget,
                out _) ||
                (_screenSpaceEffects.ShouldCaptureScene && !_screenSpaceEffects.HasCapturedScene);
        }
    }

    public void Update(double deltaSeconds)
    {
        _input.Update();
        _gameplayFeedback.Update((float)deltaSeconds);
        var settings = _pauseMenu.Settings;
        EnsurePresentationResources(settings);
        _screenSpaceEffects.SetSceneCaptureRequired(RequiresBackdropBlur(settings));
        using (_states.Performance.Measure("Client.DebugSnapshot", 0.5))
        {
            UpdateEngineDebugSnapshot(deltaSeconds, settings);
        }
        _camera.Zoom = settings.Gameplay.CameraZoom;
        _camera.Follow(GetCameraTarget(settings), _lastViewportBounds, smoothing: 0.18f);
        using (_states.Performance.Measure("Client.StreamingUpdate", 1.5))
        {
            EnsureVisibleChunks(deltaSeconds);
        }

        if (_pauseMenu.IsOpen)
        {
            _pauseMenu.Update(_input, deltaSeconds);
            SuppressGameplayInput();
            return;
        }

        var consoleHandled = _debugConsole.IsOpen
            ? _debugConsole.Update(_input, _developerConsole, CreateCommandContext(), settings.Input.KeyBindings.DebugConsole)
            : _debugConsole.Update(_input, ExecuteDebugCommand, settings.Input.KeyBindings.DebugConsole);
        if (consoleHandled)
        {
            SuppressGameplayInput();
            return;
        }

        if (_input.IsBindingPressed(settings.Input.KeyBindings.Pause))
        {
            _pauseMenu.Open();
            SuppressGameplayInput();
            return;
        }

        if (_content is not null && _characterEditorOverlay.Update(_input, _content, settings, deltaSeconds))
        {
            SuppressGameplayInput();
            UpdateAutosave((float)deltaSeconds, settings);
            return;
        }

        if (_input.IsBindingPressed(settings.Input.KeyBindings.OpenInventory) && _craftingOverlay.IsOpen)
        {
            _craftingOverlay.Close();
        }

        if (_input.IsBindingPressed(settings.Input.KeyBindings.OpenCrafting) && _inventoryOverlay.IsOpen)
        {
            _inventoryOverlay.Close();
            if (_inventoryOverlay.IsOpen)
            {
                SuppressGameplayInput();
                UpdateAutosave((float)deltaSeconds, settings);
                return;
            }
        }

        if (_inventory is not null &&
            _content is not null &&
            _world is not null &&
            _player is not null &&
            _craftingOverlay.Update(_input, _inventory, _content, _world, _player, settings))
        {
            SuppressGameplayInput();
            UpdateAutosave((float)deltaSeconds, settings);
            return;
        }

        if (_inventory is not null &&
            _content is not null &&
            _inventoryOverlay.Update(_input, _inventory, _content.Items, _equipmentLoadout, settings))
        {
            SuppressGameplayInput();
            UpdateAutosave((float)deltaSeconds, settings);
            return;
        }

        _showGrid = !_suppressDebugOverlays &&
            settings.Rendering.DrawDebugOverlays &&
            (settings.Debug.ShowGrid || _input.IsBindingDown(settings.Input.KeyBindings.DebugToggle));
        UpdateHotbarSelection(settings.Input);
        _pendingPlayerCommand = _scriptedTraversal
            ? BuildScriptedTraversalCommand()
            : BuildPlayerCommand(settings);
        PrepareItemUseRequest(settings);
        UpdateAutosave((float)deltaSeconds, settings);
        if (_simulation is not null)
        {
            _simulationPhaseOverlay.Update(deltaSeconds, _simulation, settings);
        }
    }

    public void Draw(RenderContext context)
    {
        if (_world is null || _frameSnapshot is null)
        {
            return;
        }

        var frame = _frameSnapshot;
        _lastViewportBounds = context.ViewportBounds;
        var settings = _pauseMenu.Settings;
        _camera.Zoom = settings.Gameplay.CameraZoom;
        _camera.Recalculate(context.ViewportBounds);
        var capturedScene = _screenSpaceEffects.BeginSceneCapture(
            context,
            Color.Transparent,
            _captureSceneThisFrame);

        EnsureRenderGraph(settings.Rendering.DrawLightingOverlay);
        var executor = new PlayingRenderGraphExecutor(this, context, frame, settings, capturedScene);
        _compiledRenderGraph.Execute(ref executor);
    }

    private void DrawBackgroundPass(RenderContext context, GameFrameSnapshot frame)
    {

        using (context.Performance.Measure("Render.Background", 1.25))
        {
            _backgroundRenderer.Draw(
                context,
                _textures,
                _camera,
                _world!,
                frame.WorldTime.IsNight,
                frame.LivingWorld,
                _backgroundSurfaceHeightResolver);
        }
    }

    private void DrawTilemapPass(RenderContext context, GameSettings settings)
    {
        _tilemapRenderer.ShowGrid = _showGrid;
        _tilemapRenderer.DrawLiquids = settings.Rendering.DrawLiquids;
        _tilemapRenderer.LiquidOpacity = settings.Rendering.LiquidOpacity;
        _tilemapRenderer.MaxCachedChunks = settings.Rendering.MaxChunkRenderCacheEntries;
        using (context.Performance.Measure("Render.Tilemap", 5.5))
        {
            _tilemapRenderer.Draw(context, _world!, _camera);
            DrawFarmPlots(context);
        }
    }

    private void DrawEntityPass(RenderContext context)
    {
        using (context.Performance.Measure("Render.Entities", 2.0))
        {
            DrawPlayer(context);
            DrawEntities(context);
        }
    }

    private void DrawParticlePass(RenderContext context, GameSettings settings)
    {
        using (context.Performance.Measure("Render.Particles", 1.0))
        {
            _particles.Draw(context, _camera, settings);
        }
    }

    private void DrawLightingPass(RenderContext context, GameSettings settings)
    {
        using (context.Performance.Measure("Render.Lighting", 2.5))
        {
            _lightingRenderer.Draw(context, _world!, _camera, settings.Rendering.LightingBlendStrength);
        }
    }

    private void DrawAtmospherePass(RenderContext context)
    {
        _atmosphereRenderer.DrawPrepared(context, _camera);
    }

    private void DrawScreenEffectsPass(RenderContext context, GameSettings settings, bool capturedScene)
    {
        if (capturedScene)
        {
            _screenSpaceEffects.EndSceneCaptureAndComposite(
                context,
                settings.Rendering.ReflectionStrength,
                RequiresBackdropBlur(settings) ? settings.Rendering.BlurRadiusPixels : 0);
            if (RequiresBackdropBlur(settings))
            {
                _screenSpaceEffects.DrawPreparedBackdropBlur(
                    context,
                    settings.Ui.BackdropBlurStrength,
                    settings.Rendering.BlurRadiusPixels);
            }
        }
        else
        {
            _screenSpaceEffects.DrawReusedSceneEffects(context, settings.Rendering.ReflectionStrength);
            if (RequiresBackdropBlur(settings) && _screenSpaceEffects.HasCapturedScene)
            {
                _screenSpaceEffects.DrawPreparedBackdropBlur(
                    context,
                    settings.Ui.BackdropBlurStrength,
                    settings.Rendering.BlurRadiusPixels);
            }
        }
    }

    private void DrawUiPass(RenderContext context, GameFrameSnapshot frame, GameSettings settings)
    {
        using (context.Performance.Measure("Render.UI", 2.5))
        {
            var playerStats = ResolvePlayerStats();
            _hud.Draw(
                context,
                frame,
                _content?.Items,
                _textures,
                settings);
            if (_inventory is not null && _content is not null)
            {
                _inventoryOverlay.Draw(
                    context,
                    _inventory,
                    _content.Items,
                    _textures,
                    settings,
                    _equipmentLoadout,
                    playerStats,
                    _player?.Mana ?? 0,
                    _player?.MaxMana ?? playerStats.MaxMana);
            }

            if (_content is not null)
            {
                _craftingOverlay.Draw(context, _content, _textures, settings);
                _characterEditorOverlay.Draw(context, _content, _textures, settings);
            }

            _gameplayFeedback.Draw(context, _camera, _player, settings);
        }
    }

    private void DrawDebugPass(RenderContext context, GameFrameSnapshot frame, GameSettings settings)
    {
        using (context.Performance.Measure("Render.Debug", 1.5))
        {
            if (!_suppressDebugOverlays && settings.Rendering.DrawDebugOverlays && settings.Debug.ShowDebugOverlay)
            {
                var worldTimeText = frame.WorldTime.NormalizedTimeOfDay.ToString("0.000", CultureInfo.InvariantCulture);
                context.DebugText.Draw(new Vector2(12, 58), $"WORLD TIME: {worldTimeText}", Color.LightGray, 2);
                context.DebugText.Draw(new Vector2(12, 82), $"CHUNKS: {_world!.Chunks.Count}", Color.LightGray, 2);
                context.DebugText.Draw(new Vector2(12, 106), $"ENTITIES: {frame.Hud.ActiveEntities}", Color.LightGray, 2);
                var playerTile = CoordinateUtils.WorldToTile(
                    frame.Player.Bounds.X + frame.Player.Bounds.Width * 0.5f,
                    frame.Player.Bounds.Y + frame.Player.Bounds.Height * 0.5f);
                context.DebugText.Draw(new Vector2(12, 130), $"PLAYER TILE: {playerTile.X}:{playerTile.Y}", Color.LightGray, 2);
                var living = frame.LivingWorld;
                var eventText = living.IsWorldEventActive
                    ? $" EVENT:{living.WorldEventId}/{living.WorldEventPhaseId ?? "active"}"
                    : string.Empty;
                context.DebugText.Draw(
                    new Vector2(12, 394),
                    $"BIOME: {living.BiomeId}/{living.SubBiomeId ?? "-"} WEATHER:{living.Weather} {living.WeatherIntensity:0.00}{eventText}",
                    Color.LightGray,
                    2);

                if (settings.Debug.ShowMouseTile && _hoverTile is { } hoverTile)
                {
                    context.DebugText.Draw(new Vector2(12, 154), $"MOUSE TILE: {hoverTile.X}:{hoverTile.Y}", Color.LightGray, 2);
                }

                DrawEngineDebugSnapshot(context);
                if (settings.Debug.ShowRenderMetrics)
                {
                    DrawRenderDebugMetrics(context);
                }

                if (settings.Debug.ShowStreamingMetrics)
                {
                    DrawStreamingDebugMetrics(context);
                }

                if (settings.Debug.ShowSaveMetrics)
                {
                    DrawSaveDebugMetrics(context);
                }

                context.DebugText.Draw(new Vector2(12, 346), $"FARM PLOTS: {frame.Hud.FarmPlots}", Color.LightGray, 2);
                context.DebugText.Draw(new Vector2(12, 370), $"PICKUP LAST: {frame.Hud.PickedUpItemsThisTick}", Color.LightGray, 2);
            }

            if (settings.Gameplay.ShowInteractionTarget)
            {
                DrawInteractionTarget(context);
            }

            if (!_suppressDebugOverlays)
            {
                _performanceOverlay.Draw(context, settings);
                _simulationPhaseOverlay.Draw(context);
                _eventJournalOverlay.Draw(context, _eventJournal, settings);
            }

            _debugConsole.Draw(context);
            _pauseMenu.Draw(context);
        }
    }

    private void EnsureRenderGraph(bool lightingEnabled)
    {
        if (_renderGraphConfigured && _renderGraphLightingEnabled == lightingEnabled)
        {
            return;
        }

        _renderGraph.Clear();
        var lastResource = RenderGraphIds.Background;
        DeclareTransient(RenderGraphIds.Background);
        _renderGraph.DeclarePass(
            RenderGraphIds.BackgroundPass,
            RenderPassPhase.Prepass,
            ReadOnlySpan<RenderResourceId>.Empty,
            [RenderGraphIds.Background]);

        DeclareChainedPass(RenderGraphIds.TilemapPass, RenderPassPhase.Opaque, lastResource, RenderGraphIds.Tilemap);
        lastResource = RenderGraphIds.Tilemap;
        DeclareChainedPass(RenderGraphIds.EntityPass, RenderPassPhase.Opaque, lastResource, RenderGraphIds.Entities);
        lastResource = RenderGraphIds.Entities;
        DeclareChainedPass(RenderGraphIds.ParticlePass, RenderPassPhase.Transparent, lastResource, RenderGraphIds.Particles);
        lastResource = RenderGraphIds.Particles;
        if (lightingEnabled)
        {
            DeclareChainedPass(RenderGraphIds.LightingPass, RenderPassPhase.PostProcess, lastResource, RenderGraphIds.Lighting);
            lastResource = RenderGraphIds.Lighting;
        }

        DeclareChainedPass(RenderGraphIds.AtmospherePass, RenderPassPhase.Composite, lastResource, RenderGraphIds.Atmosphere);
        lastResource = RenderGraphIds.Atmosphere;
        DeclareChainedPass(RenderGraphIds.ScreenEffectsPass, RenderPassPhase.Composite, lastResource, RenderGraphIds.ScreenEffects);
        lastResource = RenderGraphIds.ScreenEffects;
        DeclareChainedPass(RenderGraphIds.UiPass, RenderPassPhase.Overlay, lastResource, RenderGraphIds.Ui);
        lastResource = RenderGraphIds.Ui;
        DeclareChainedPass(RenderGraphIds.DebugPass, RenderPassPhase.Overlay, lastResource, RenderGraphIds.Presented);

        Span<RenderResourceId> requested = stackalloc RenderResourceId[1];
        requested[0] = RenderGraphIds.Presented;
        _compiledRenderGraph = _renderGraph.Compile(requested);
        _renderGraphConfigured = true;
        _renderGraphLightingEnabled = lightingEnabled;
    }

    private void DeclareChainedPass(
        RenderPassId pass,
        RenderPassPhase phase,
        RenderResourceId input,
        RenderResourceId output)
    {
        DeclareTransient(output);
        Span<RenderResourceId> reads = stackalloc RenderResourceId[1];
        Span<RenderResourceId> writes = stackalloc RenderResourceId[1];
        reads[0] = input;
        writes[0] = output;
        _renderGraph.DeclarePass(pass, phase, reads, writes);
    }

    private void DeclareTransient(RenderResourceId resource)
    {
        _renderGraph.DeclareResource(resource, RenderResourceFlags.Transient);
    }

    private void ExecuteRenderPass(
        RenderPassId pass,
        RenderContext context,
        GameFrameSnapshot frame,
        GameSettings settings,
        bool capturedScene)
    {
        switch (pass.Value)
        {
            case 0:
                DrawBackgroundPass(context, frame);
                break;
            case 1:
                DrawTilemapPass(context, settings);
                break;
            case 2:
                DrawEntityPass(context);
                break;
            case 3:
                DrawParticlePass(context, settings);
                break;
            case 4:
                DrawLightingPass(context, settings);
                break;
            case 5:
                DrawAtmospherePass(context);
                break;
            case 6:
                DrawScreenEffectsPass(context, settings, capturedScene);
                break;
            case 7:
                DrawUiPass(context, frame, settings);
                break;
            case 8:
                DrawDebugPass(context, frame, settings);
                break;
            default:
                throw new InvalidOperationException($"Unknown playing render pass {pass}.");
        }
    }

    private readonly struct PlayingRenderGraphExecutor : IRenderPassExecutor
    {
        private readonly PlayingState _owner;
        private readonly RenderContext _context;
        private readonly GameFrameSnapshot _frame;
        private readonly GameSettings _settings;
        private readonly bool _capturedScene;

        public PlayingRenderGraphExecutor(
            PlayingState owner,
            RenderContext context,
            GameFrameSnapshot frame,
            GameSettings settings,
            bool capturedScene)
        {
            _owner = owner;
            _context = context;
            _frame = frame;
            _settings = settings;
            _capturedScene = capturedScene;
        }

        public void Execute(in RenderPassDescriptor pass)
        {
            _owner.ExecuteRenderPass(pass.Id, _context, _frame, _settings, _capturedScene);
        }
    }

    private static class RenderGraphIds
    {
        public static readonly RenderPassId BackgroundPass = new(0);
        public static readonly RenderPassId TilemapPass = new(1);
        public static readonly RenderPassId EntityPass = new(2);
        public static readonly RenderPassId ParticlePass = new(3);
        public static readonly RenderPassId LightingPass = new(4);
        public static readonly RenderPassId AtmospherePass = new(5);
        public static readonly RenderPassId ScreenEffectsPass = new(6);
        public static readonly RenderPassId UiPass = new(7);
        public static readonly RenderPassId DebugPass = new(8);

        public static readonly RenderResourceId Background = new(0);
        public static readonly RenderResourceId Tilemap = new(1);
        public static readonly RenderResourceId Entities = new(2);
        public static readonly RenderResourceId Particles = new(3);
        public static readonly RenderResourceId Lighting = new(4);
        public static readonly RenderResourceId Atmosphere = new(5);
        public static readonly RenderResourceId ScreenEffects = new(6);
        public static readonly RenderResourceId Ui = new(7);
        public static readonly RenderResourceId Presented = new(8);
    }

    public void Dispose()
    {
        _craftingOverlay.CraftingResolved -= OnCraftingResolved;
        _streaming.CancelPendingJobs();
        _eventJournal?.Dispose();
        _feedbackRouter?.Dispose();
        _session?.Dispose();
        _session = null;
        _simulation = null;
        _particles.Clear();
        _tilemapRenderer.Dispose();
        _textures?.Dispose();
        _lightingRenderer.Dispose();
        _screenSpaceEffects.Dispose();
        _entityVisualDrawExecutor.Dispose();
        _soundscape?.Reset(0);
        _audio.Dispose();
    }

    public void OnTextInput(char character)
    {
        if (_craftingOverlay.IsOpen)
        {
            _craftingOverlay.OnTextInput(character);
            return;
        }

        _debugConsole.OnTextInput(character);
    }

    private void OnCraftingResolved(Game.Core.Crafting.CraftingBatchResult result)
    {
        if (result.CompletedEvent is { } completed)
        {
            _events.Publish(completed);
            return;
        }

        if (result.FailedEvent is { } failed)
        {
            _events.Publish(failed);
        }
    }

    private Vector2 GetCameraTarget(GameSettings settings)
    {
        if (_frameSnapshot is not { } frame)
        {
            return Vector2.Zero;
        }

        var lookAhead = settings.Gameplay.CameraLookAheadPixels;
        var velocity = frame.Player.Velocity;
        var offsetX = Math.Abs(velocity.X) < 1f ? 0f : MathF.Sign(velocity.X) * lookAhead;
        return new Vector2(
            frame.Player.Bounds.X + frame.Player.Bounds.Width * 0.5f + offsetX,
            frame.Player.Bounds.Y + frame.Player.Bounds.Height * 0.5f);
    }

    private void DrawPlayer(RenderContext context)
    {
        if (_frameSnapshot is not { } frame)
        {
            return;
        }

        if (TryDrawPlayerSprite(context, frame.Player))
        {
            return;
        }

        var screenPosition = _camera.WorldToScreen(
            new Vector2(frame.Player.Position.X, frame.Player.Position.Y),
            context.ViewportBounds);

        var destination = new Rectangle(
            (int)MathF.Floor(screenPosition.X),
            (int)MathF.Floor(screenPosition.Y),
            (int)MathF.Ceiling(frame.Player.Bounds.Width * _camera.Zoom),
            (int)MathF.Ceiling(frame.Player.Bounds.Height * _camera.Zoom));

        context.SpriteBatch.Draw(context.Pixel, destination, new Color(230, 204, 138));
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(destination.X, destination.Y, destination.Width, 2), new Color(80, 52, 48));
    }

    private bool TryDrawPlayerSprite(RenderContext context, PlayerFrameSnapshot player)
    {
        _ = player;
        return _playerCharacterRenderer.Draw(context, _textures, _camera);
    }

    private bool TryDrawEntitySprite(RenderContext context, EntityFrameSnapshot entity)
    {
        if (_textures is null || _content is null || !TryResolveEntitySpriteId(entity, out var spriteId))
        {
            return false;
        }

        var frameIndex = ResolveEntityFrameIndex(entity, spriteId, context.Time.TotalSeconds);
        var sprite = _textures.Get(spriteId, frameIndex);
        if (sprite.SourceRectangle.Width <= 0 || sprite.SourceRectangle.Height <= 0)
        {
            return false;
        }

        var bounds = entity.Bounds;
        var source = sprite.SourceRectangle;
        var worldPosition = new Vector2(
            bounds.X + bounds.Width * 0.5f - source.Width * 0.5f,
            bounds.Y + bounds.Height - source.Height);
        worldPosition += ResolveEntityVisualOffset(entity, context.Time.TotalSeconds);
        var screenPosition = _camera.WorldToScreen(worldPosition, context.ViewportBounds);
        var destination = new Rectangle(
            (int)MathF.Floor(screenPosition.X),
            (int)MathF.Floor(screenPosition.Y),
            Math.Max(1, (int)MathF.Ceiling(source.Width * _camera.Zoom)),
            Math.Max(1, (int)MathF.Ceiling(source.Height * _camera.Zoom)));

        var effects = ResolveEntitySpriteEffects(entity);
        var tint = ResolveEntityTint(entity, context.Time.TotalSeconds);
        context.SpriteBatch.Draw(sprite.Texture, destination, source, tint, 0f, Vector2.Zero, effects, 0f);
        return true;
    }

    private static SpriteEffects ResolveEntitySpriteEffects(EntityFrameSnapshot entity)
    {
        return entity.Kind switch
        {
            EntityFrameKind.Enemy when entity.Velocity.X < -1f => SpriteEffects.FlipHorizontally,
            EntityFrameKind.Projectile when entity.Velocity.X < 0 => SpriteEffects.FlipHorizontally,
            _ => SpriteEffects.None
        };
    }

    private static Color ResolveEntityTint(EntityFrameSnapshot entity, double totalSeconds)
    {
        if (entity.IsDamageFlashing)
        {
            return Math.Floor(totalSeconds * 18) % 2 == 0
                ? new Color(255, 210, 210)
                : Color.White;
        }

        if (entity.Kind == EntityFrameKind.Projectile && entity.DamageType == Game.Core.Combat.DamageType.Magic)
        {
            return new Color(170, 210, 255);
        }

        return Color.White;
    }

    private static Vector2 ResolveEntityVisualOffset(EntityFrameSnapshot entity, double totalSeconds)
    {
        return entity.Kind switch
        {
            EntityFrameKind.DroppedItem => new Vector2(0, MathF.Sin((float)(totalSeconds * 5.5 + entity.Id)) * 2f),
            EntityFrameKind.Enemy when entity.ContentId.Contains("bat", StringComparison.OrdinalIgnoreCase) => new Vector2(0, MathF.Sin((float)(totalSeconds * 6 + entity.Id)) * 3f),
            _ => Vector2.Zero
        };
    }

    private int ResolveEntityFrameIndex(EntityFrameSnapshot entity, string spriteId, double totalSeconds)
    {
        if (_content is null ||
            !_content.SpriteAssets.TryGetById(spriteId, out var asset) ||
            asset.Frames.Count <= 1 ||
            !asset.HasTag("animated") ||
            entity.Kind is EntityFrameKind.DroppedItem or EntityFrameKind.Projectile)
        {
            return 0;
        }

        var frameRate = entity.Kind == EntityFrameKind.Enemy && entity.ContentId.Contains("bat", StringComparison.OrdinalIgnoreCase)
            ? 10.0
            : 6.0;
        return (int)Math.Floor(totalSeconds * frameRate + entity.Id * 0.37) % asset.Frames.Count;
    }

    private bool TryResolveEntitySpriteId(in EntityFrameSnapshot entity, out string spriteId)
    {
        spriteId = string.Empty;
        if (_content is null)
        {
            return false;
        }

        switch (entity.Kind)
        {
            case EntityFrameKind.Enemy when _content.Entities.TryGetById(entity.ContentId, out var enemyDefinition):
                spriteId = enemyDefinition.TexturePath;
                return true;
            case EntityFrameKind.DroppedItem when _content.Items.TryGetById(entity.ContentId, out var itemDefinition):
                spriteId = itemDefinition.TexturePath;
                return true;
            case EntityFrameKind.Projectile when _content.Projectiles.TryGetById(entity.ContentId, out var projectileDefinition):
                spriteId = projectileDefinition.TexturePath;
                return true;
            default:
                return false;
        }
    }

    private void DrawEntities(RenderContext context)
    {
        if (_frameSnapshot is not { } frame)
        {
            return;
        }

        if (_textures is not null && _entityVisuals.CommandBuffer.Count > 0)
        {
            _entityVisualDrawExecutor.Draw(context, _textures, _entityVisuals.CommandBuffer, _camera);
            return;
        }

        foreach (var entity in frame.Entities)
        {
            if (!entity.IsActive)
            {
                continue;
            }

            if (TryDrawEntitySprite(context, entity))
            {
                continue;
            }

            var color = entity.Kind switch
            {
                EntityFrameKind.Enemy => new Color(98, 181, 94),
                EntityFrameKind.DroppedItem => new Color(239, 203, 105),
                EntityFrameKind.Projectile => new Color(224, 86, 72),
                _ => new Color(180, 180, 180)
            };

            DrawWorldRect(context, entity.Bounds, color);
        }
    }

    private void DrawWorldRect(RenderContext context, RectI bounds, Color color)
    {
        var screenPosition = _camera.WorldToScreen(new Vector2(bounds.X, bounds.Y), context.ViewportBounds);
        var destination = new Rectangle(
            (int)MathF.Floor(screenPosition.X),
            (int)MathF.Floor(screenPosition.Y),
            Math.Max(1, (int)MathF.Ceiling(bounds.Width * _camera.Zoom)),
            Math.Max(1, (int)MathF.Ceiling(bounds.Height * _camera.Zoom)));

        context.SpriteBatch.Draw(context.Pixel, destination, color);
    }

    private void UpdatePlayerAnimation()
    {
        if (_player is null || _content is null)
        {
            return;
        }

        if (_player.Body.Velocity.X < -2f)
        {
            _playerFacingLeft = true;
        }
        else if (_player.Body.Velocity.X > 2f)
        {
            _playerFacingLeft = false;
        }

        if (_frameSnapshot is { } frame)
        {
            if (frame.Attack.AttackInstanceId != 0 &&
                frame.Attack.AttackInstanceId != _lastAttackVisualInstanceId)
            {
                _lastAttackVisualInstanceId = frame.Attack.AttackInstanceId;
                _playerCharacterRenderer.RequestAction(CharacterAnimationState.Attack);
            }

            var damageFlashing = _player.HealthComponent.InvulnerabilityTimeRemaining > 0;
            if (damageFlashing && !_playerWasDamageFlashing)
            {
                _playerCharacterRenderer.RequestAction(CharacterAnimationState.Hurt);
            }

            _playerWasDamageFlashing = damageFlashing;

            _playerCharacterRenderer.Advance(
                frame.Player,
                _simulation?.PlayerGuard,
                _characterEditorOverlay.Appearance,
                _equipmentLoadout);
        }
    }

    private PlayerCommand BuildPlayerCommand(GameSettings settings)
    {
        var command = PlayerCommandBuilder.Build(_input, settings.Input.KeyBindings);
        _jumpInputLatch.Observe(
            command.WantsJump,
            _input.IsBindingPressed(settings.Input.KeyBindings.Jump));
        command = command with { WantsJump = _jumpInputLatch.IsActiveForFixedStep };
        if (_player is null)
        {
            return command;
        }

        var wantsGuard = settings.Gameplay.HoldToBlock
            ? _input.IsBindingDown(settings.Input.KeyBindings.AttackSecondary)
            : ResolveToggleGuard(settings.Input.KeyBindings.AttackSecondary);
        var mouseWorld = _camera.ScreenToWorld(_input.MousePosition.ToVector2(), _lastViewportBounds);
        var facing = new System.Numerics.Vector2(
            mouseWorld.X - _player.Body.Center.X,
            mouseWorld.Y - _player.Body.Center.Y);
        if (facing.LengthSquared() <= float.Epsilon)
        {
            facing = _playerFacingLeft
                ? -System.Numerics.Vector2.UnitX
                : System.Numerics.Vector2.UnitX;
        }

        return command with { WantsGuard = wantsGuard, GuardFacing = facing };
    }

    private PlayerCommand BuildScriptedTraversalCommand()
    {
        var tick = _frameSnapshot?.TickNumber ?? 0;
        return new PlayerCommand(
            MoveAxis: 1f,
            WantsJump: tick % 90 is 0 or 1,
            WantsGuard: false,
            GuardFacing: System.Numerics.Vector2.UnitX);
    }

    private bool ResolveToggleGuard(string binding)
    {
        if (_input.IsBindingPressed(binding))
        {
            _guardToggleActive = !_guardToggleActive;
        }

        return _guardToggleActive;
    }

    private CharacterAnimationState? ResolvePlayerActionState(ItemStack stack)
    {
        if (_content is null || stack.IsEmpty || !_content.Items.TryGetById(stack.ItemId, out var item))
        {
            return null;
        }

        return item.Type switch
        {
            ItemType.ToolPickaxe or ItemType.ToolAxe or ItemType.ToolHoe or ItemType.ToolWateringCan => CharacterAnimationState.Mine,
            ItemType.WeaponMelee or ItemType.WeaponRanged or ItemType.WeaponMagic => CharacterAnimationState.Attack,
            ItemType.PlaceableTile or ItemType.Consumable or ItemType.Seed => CharacterAnimationState.UseItem,
            _ => null
        };
    }

    private void TriggerPlayerActionAnimation(CharacterAnimationState? state)
    {
        if (!state.HasValue)
        {
            return;
        }

        _playerCharacterRenderer.RequestAction(state.Value);
    }

    private void UpdateHotbarSelection(InputSettings inputSettings)
    {
        for (var slot = 0; slot < 10; slot++)
        {
            if (_input.IsBindingPressed(GetHotbarBinding(inputSettings.KeyBindings, slot)))
            {
                _selectedHotbarSlot = slot;
                _inventory?.SelectHotbarSlot(slot);
            }
        }

        var scrollDelta = inputSettings.InvertHotbarScroll ? -_input.ScrollDelta : _input.ScrollDelta;
        if (scrollDelta > 0)
        {
            _selectedHotbarSlot = (_selectedHotbarSlot + 9) % 10;
            _inventory?.SelectHotbarSlot(_selectedHotbarSlot);
        }
        else if (scrollDelta < 0)
        {
            _selectedHotbarSlot = (_selectedHotbarSlot + 1) % 10;
            _inventory?.SelectHotbarSlot(_selectedHotbarSlot);
        }
    }

    private void PrepareItemUseRequest(GameSettings settings)
    {
        if (_world is null || _content is null || _player is null || _inventory is null)
        {
            _pendingItemUse = PlayerItemUseRequest.Inactive;
            _hasPendingItemUse = false;
            return;
        }

        var mouseWorld = _camera.ScreenToWorld(_input.MousePosition.ToVector2(), _lastViewportBounds);
        _hoverTile = CoordinateUtils.WorldToTile(mouseWorld.X, mouseWorld.Y);
        _interactionTile = ResolveInteractionTarget(mouseWorld, settings.Gameplay.InteractionReachPixels);

        var selectedStack = _inventory.SelectedStack;
        var selectedType = !selectedStack.IsEmpty &&
            _content.Items.TryGetById(selectedStack.ItemId, out var selectedItem)
                ? selectedItem.Type
                : ItemType.Material;
        _pendingItemUseContinuous = settings.Gameplay.HoldToMine && selectedType is
            ItemType.ToolPickaxe or
            ItemType.ToolAxe or
            ItemType.ToolHoe or
            ItemType.ToolWateringCan or
            ItemType.PlaceableTile;
        var useInputActive = _pendingItemUseContinuous
            ? _input.IsBindingDown(settings.Input.KeyBindings.AttackPrimary)
            : _input.IsBindingPressed(settings.Input.KeyBindings.AttackPrimary);

        if (!useInputActive || _interactionTile is not { } targetTile)
        {
            if (_pendingItemUseContinuous)
            {
                _pendingItemUse = PlayerItemUseRequest.Inactive;
                _hasPendingItemUse = false;
            }

            _gameplayFeedback.CancelMining();
            return;
        }

        var actionState = ResolvePlayerActionState(_inventory.SelectedStack);
        if (actionState != CharacterAnimationState.Attack)
        {
            TriggerPlayerActionAnimation(actionState);
        }
        _pendingItemUse = new PlayerItemUseRequest(
            true,
            targetTile,
            new System.Numerics.Vector2(mouseWorld.X, mouseWorld.Y));
        _hasPendingItemUse = true;
    }

    private void SuppressGameplayInput()
    {
        _pendingPlayerCommand = PlayerCommand.None;
        _jumpInputLatch.Reset();
        _pendingItemUse = PlayerItemUseRequest.Inactive;
        _hasPendingItemUse = false;
        _gameplayFeedback.CancelMining();
    }

    private TilePos? ResolveInteractionTarget(Vector2 mouseWorld, float reachPixels)
    {
        if (_world is null || _content is null || _player is null || _inventory is null)
        {
            return null;
        }

        var selected = _inventory.SelectedStack;
        if (selected.IsEmpty || !_content.Items.TryGetById(selected.ItemId, out var item))
        {
            return _hoverTile;
        }

        var aim = new System.Numerics.Vector2(mouseWorld.X, mouseWorld.Y);
        var target = item.Type switch
        {
            ItemType.PlaceableTile => _targeting.FindPlacementTarget(_world, _player.Body.Center, aim, reachPixels),
            ItemType.ToolPickaxe => _targeting.FindMiningTarget(_world, _player.Body.Center, aim, reachPixels),
            ItemType.ToolHoe or ItemType.ToolWateringCan or ItemType.Seed => _hoverTile is { } hover ? new InteractionTarget(true, hover) : InteractionTarget.None,
            _ => _hoverTile is { } hover ? new InteractionTarget(true, hover) : InteractionTarget.None
        };

        return target.Found ? target.TilePosition : null;
    }

    private void DrawInteractionTarget(RenderContext context)
    {
        if (_interactionTile is not { } target)
        {
            return;
        }

        var screenPosition = _camera.WorldToScreen(
            new Vector2(target.X * GameConstants.TileSize, target.Y * GameConstants.TileSize),
            context.ViewportBounds);
        var size = Math.Max(1, (int)MathF.Ceiling(GameConstants.TileSize * _camera.Zoom));
        var bounds = new Rectangle((int)MathF.Floor(screenPosition.X), (int)MathF.Floor(screenPosition.Y), size, size);
        var color = new Color(245, 214, 126, 210);

        context.SpriteBatch.Draw(context.Pixel, new Rectangle(bounds.X, bounds.Y, bounds.Width, 2), color);
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(bounds.X, bounds.Bottom - 2, bounds.Width, 2), color);
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(bounds.X, bounds.Y, 2, bounds.Height), color);
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(bounds.Right - 2, bounds.Y, 2, bounds.Height), color);
    }

    private void UpdateEngineDebugSnapshot(double deltaSeconds, GameSettings settings)
    {
        if (_world is null ||
            !settings.Rendering.DrawDebugOverlays ||
            !settings.Debug.ShowDebugOverlay)
        {
            return;
        }

        _debugSnapshotElapsed += Math.Max(0, deltaSeconds);
        if (_debugSnapshot is not null && _debugSnapshotElapsed < 0.5)
        {
            return;
        }

        _debugSnapshot = _debugSnapshots.Build(_world, _entities, _worldTime);
        _debugSnapshotElapsed = 0;
    }

    private void DrawEngineDebugSnapshot(RenderContext context)
    {
        if (_debugSnapshot is not { } snapshot)
        {
            return;
        }

        context.DebugText.Draw(new Vector2(12, 178), $"DIRTY: {snapshot.DirtyChunkCount} LIQ: {snapshot.LiquidTileCount}", Color.LightGray, 2);
        context.DebugText.Draw(new Vector2(12, 202), $"SURF: {snapshot.MinSurfaceY}-{snapshot.MaxSurfaceY}", Color.LightGray, 2);
    }

    private void DrawFarmPlots(RenderContext context)
    {
        if (_content is null || _frameSnapshot is not { } frame)
        {
            return;
        }

        foreach (var plot in frame.FarmPlots)
        {
            var worldRect = new RectI(
                plot.Position.X * GameConstants.TileSize,
                plot.Position.Y * GameConstants.TileSize,
                GameConstants.TileSize,
                GameConstants.TileSize);

            DrawWorldRect(context, worldRect, plot.IsWatered ? new Color(81, 116, 130, 150) : new Color(116, 79, 48, 150));

            if (plot.CropId is null || !_content.Crops.TryGetById(plot.CropId, out var crop))
            {
                continue;
            }

            var totalGrowthDays = 0;
            for (var index = 0; index < crop.GrowthStageDays.Count; index++)
            {
                totalGrowthDays += crop.GrowthStageDays[index];
            }

            totalGrowthDays = Math.Max(1, totalGrowthDays);
            var elapsedDays = Math.Max(0, totalGrowthDays - plot.DaysUntilHarvest);
            var stage = 0;
            var stageBoundary = 0;
            while (stage < crop.GrowthStageDays.Count - 1 && elapsedDays >= stageBoundary + crop.GrowthStageDays[stage])
            {
                stageBoundary += crop.GrowthStageDays[stage++];
            }

            var cropColor = plot.IsMature
                ? new Color(142, 190, 83)
                : new Color(83, 153, 72);
            var cropRect = new RectI(
                worldRect.X + 4,
                worldRect.Y + Math.Max(2, 10 - stage * 2),
                8,
                Math.Min(12, 4 + stage * 3));
            DrawWorldRect(context, cropRect, cropColor);
        }
    }

    private void DrawRenderDebugMetrics(RenderContext context)
    {
        var metrics = _tilemapRenderer.LastMetrics;
        context.DebugText.Draw(
            new Vector2(12, 226),
            $"RENDER CHUNKS: {metrics.VisibleChunks} CACHE: {metrics.CachedChunks} REBUILT: {metrics.RebuiltChunks}",
            Color.LightGray,
            2);
        context.DebugText.Draw(
            new Vector2(12, 250),
            $"TILE CMDS: {metrics.RenderedTileCommands} LIQ CMDS: {metrics.RenderedLiquidCommands} EVICT: {metrics.EvictedChunks} TEX:{_textures?.Telemetry.ResourceCount ?? 0}/{_textures?.Telemetry.FrameCount ?? 0}",
            Color.LightGray,
            2);
        var lighting = _lightingRenderer.LastTelemetry;
        var reflections = _screenSpaceEffects.LastTelemetry;
        var atlas = _tilemapRenderer.AtlasTelemetry;
        var blur = _screenSpaceEffects.LastBackdropBlurPlan;
        var graph = _compiledRenderGraph.Telemetry;
        var presentationBudget = _presentationFrameBudget.CaptureTelemetry();
        context.DebugText.Draw(
            new Vector2(12, 418),
            $"LIGHT {lighting.Quality} {lighting.MaskSize.X}x{lighting.MaskSize.Y} RAYS:{lighting.RaysCast} OCC:{lighting.OccluderSamples} PTS:{lighting.PointLightsUsed} UP:{_lightingRenderer.LastTextureUploadCount} REFL:{reflections.SurfaceCount} ATLAS:{atlas.PageCount}/{atlas.TextureBucketsSaved} BLUR:{blur.TargetSize.X}x{blur.TargetSize.Y} RG:{graph.ExecutedPasses}/{graph.TransientAliasSlots} PB:{presentationBudget.ConsumedUnits}/{presentationBudget.MaximumUnits} D:{presentationBudget.DeferredWorkCount}",
            Color.LightGray,
            2);
        if (_soundscape is not null)
        {
            var audio = _soundscape.CaptureTelemetry();
            context.DebugText.Draw(
                new Vector2(12, 442),
                $"AUDIO V:{audio.Audio.ActiveVoices} MISS:{audio.Audio.MissingAssets} TRANS:{audio.Audio.LoopTransitions} SCAPE:{audio.DesiredAmbientLoopId ?? "-"}",
                Color.LightGray,
                2);
        }

        var entityVisuals = _entityVisuals.CommandBuffer.Telemetry;
        context.DebugText.Draw(
            new Vector2(12, 466),
            $"ENTITY VIS {entityVisuals.VisibleEntities}/{entityVisuals.InputEntities} CMD:{entityVisuals.PreparedCommands} CULL:{entityVisuals.CulledEntities} OVR:{entityVisuals.CommandOverflowEntities + entityVisuals.TrackOverflowEntities}",
            entityVisuals.WasBudgetClamped ? Color.OrangeRed : Color.LightGray,
            2);
        var feedback = _feedbackRouter?.Telemetry ?? default;
        context.DebugText.Draw(
            new Vector2(12, 490),
            $"FX Q:{feedback.PendingVisualCommands}/{feedback.PendingAudioCommands} DROP:{feedback.VisualCommandsDropped}/{feedback.AudioCommandsDropped} DRAIN:{feedback.VisualCommandsDrained}/{feedback.AudioCommandsDrained}",
            feedback.VisualCommandsDropped + feedback.AudioCommandsDropped > 0 ? Color.OrangeRed : Color.LightGray,
            2);
    }

    private void EnsurePresentationResources(GameSettings settings)
    {
        if (_graphicsDevice is null || _lastViewportBounds.IsEmpty)
        {
            return;
        }

        _lightingRenderer.PrepareResources(_graphicsDevice, _lastViewportBounds, settings.Rendering);
        _screenSpaceEffects.PrepareResources(
            _graphicsDevice,
            _lastViewportBounds,
            settings.Rendering,
            settings.Ui);
    }

    private bool RequiresBackdropBlur(GameSettings settings)
    {
        return settings.Rendering.UiEffectQuality > 0 &&
            settings.Ui.BackdropBlurStrength > 0.001f &&
            (_pauseMenu.IsOpen ||
             _inventoryOverlay.IsOpen ||
             _craftingOverlay.IsOpen ||
             _characterEditorOverlay.IsOpen);
    }

    private void PreparePresentationFrames(
        GameFrameSnapshot frame,
        GameSettings settings,
        bool prepareLighting,
        bool prepareReflections,
        bool prepareAtmosphere)
    {
        if (_world is null || _content is null || _lastViewportBounds.IsEmpty)
        {
            return;
        }

        var lightingQuality = _lightingRenderer.Quality;
        if (prepareLighting)
        {
            using (_states.Performance.Measure("Presentation.LightingPrepare", 2.0))
            {
                var lightTelemetry = VisibleLightCollector.CollectTileLights(
                    _world,
                    _content.Tiles,
                    _camera.VisibleWorldRect,
                    lightingQuality,
                    _visibleLights);
                _lightingRenderer.PrepareFrame(
                    _world,
                    _camera,
                    _lastViewportBounds,
                    frame.WorldTime,
                    frame.LivingWorld,
                    settings.Rendering,
                    frame.TickNumber,
                    _visibleLights.AsSpan(0, lightTelemetry.LightsCollected),
                    _states.Performance);
            }
        }

        if (prepareReflections)
        {
            using (_states.Performance.Measure("Presentation.ReflectionPrepare", 1.0))
            {
                var waterPalette = WaterPresentationPaletteCatalog.Resolve(frame.LivingWorld.BiomeId);
                _screenSpaceEffects.PrepareFrame(
                    _world,
                    _camera,
                    _lastViewportBounds,
                    frame.TickNumber,
                    settings.Rendering,
                    waterPalette);
            }
        }

        if (prepareAtmosphere)
        {
            using (_states.Performance.Measure("Presentation.AtmospherePrepare", 0.75))
            {
                _atmosphereRenderer.PrepareFrame(
                    _world,
                    _camera,
                    frame.WorldTime,
                    frame.LivingWorld,
                    settings.Rendering,
                    _lastViewportBounds,
                    lightingQuality.Tier,
                    frame.TickNumber);
            }
        }
    }

    private void EnsurePresentationCadence(RenderingSettings settings)
    {
        var cadence = new PresentationCadenceConfiguration(
            settings.LightingUpdateRateHz,
            settings.ReflectionUpdateRateHz,
            settings.AtmosphereUpdateRateHz,
            settings.SceneCaptureUpdateRateHz);
        if (_hasPresentationCadence && cadence == _presentationCadence)
        {
            return;
        }

        _presentationWork.Configure(
            _lightingWork,
            CreatePresentationSchedule(cadence.LightingHz, 30, 3d),
            requestImmediate: true);
        _presentationWork.Configure(
            _reflectionWork,
            CreatePresentationSchedule(cadence.ReflectionHz, 45, 6d),
            requestImmediate: true);
        _presentationWork.Configure(
            _atmosphereWork,
            CreatePresentationSchedule(cadence.AtmosphereHz, 45, 8d),
            requestImmediate: true);
        _presentationWork.Configure(
            _sceneCaptureWork,
            CreatePresentationSchedule(cadence.SceneCaptureHz, 20, 4d),
            requestImmediate: true);
        _presentationCadence = cadence;
        _hasPresentationCadence = true;
    }

    private static PresentationWorkSchedule CreatePresentationSchedule(
        int targetHz,
        int maximumDeferredFrames,
        double cameraTranslationThreshold)
    {
        var rate = Math.Clamp(targetHz, 10, 240);
        return new PresentationWorkSchedule(
            rate,
            MaximumStalenessSeconds: Math.Max(0.125d, 3d / rate),
            maximumDeferredFrames,
            PresentationWorkTrigger.Revision |
                PresentationWorkTrigger.CameraTranslation |
                PresentationWorkTrigger.CameraZoom,
            MinimumRevisionDelta: 1,
            CameraTranslationThreshold: cameraTranslationThreshold,
            CameraZoomThreshold: 0.01d);
    }

    private static int ResolvePresentationFrameBudget(RenderingSettings settings)
    {
        var maximumQuality = Math.Max(
            Math.Max(settings.LightingQuality, settings.ShadowQuality),
            Math.Max(settings.ReflectionQuality, settings.UiEffectQuality));
        return maximumQuality switch
        {
            <= 1 => 4,
            2 => 6,
            _ => 8
        };
    }

    private static int EstimateLightingWork(RenderingSettings settings)
    {
        return 2 + Math.Max(settings.LightingQuality, settings.ShadowQuality);
    }

    private static int EstimateReflectionWork(RenderingSettings settings)
    {
        return 2 + Math.Max(0, settings.ReflectionQuality);
    }

    private static int EstimateSceneCaptureWork(RenderingSettings settings)
    {
        return 2 + Math.Max(settings.ReflectionQuality, settings.UiEffectQuality);
    }

    private readonly record struct PresentationCadenceConfiguration(
        int LightingHz,
        int ReflectionHz,
        int AtmosphereHz,
        int SceneCaptureHz);

    private void DrawStreamingDebugMetrics(RenderContext context)
    {
        var telemetry = _lastStreaming.Telemetry;
        context.DebugText.Draw(
            new Vector2(12, 274),
            $"STREAM L/G/S/U:{_lastStreaming.LoadedChunks}/{_lastStreaming.GeneratedChunks}/{_lastStreaming.SavedChunksBeforeUnload}/{_lastStreaming.UnloadedChunks} JOBS:{telemetry.PendingLoadJobs}/{telemetry.PendingSaveJobs} APPLY:{telemetry.ApplyQueueLength} ({telemetry.QueuedDecodedBytes / 1024}KB)",
            Color.LightGray,
            2);
        context.DebugText.Draw(
            new Vector2(12, 298),
            $"STREAM DEF:{telemetry.DeferredLoadRequests}/{telemetry.DeferredUnloadRequests} CANCEL:{telemetry.CancelledJobs}/{telemetry.CancellationRequests} STALE:{telemetry.StaleResultsRejected} FAIL:{telemetry.FailedJobs} MS:{telemetry.LoadTime.TotalMilliseconds:0}/{telemetry.GenerateTime.TotalMilliseconds:0}/{telemetry.ApplyTime.TotalMilliseconds:0}/{telemetry.SaveTime.TotalMilliseconds:0}",
            Color.LightGray,
            2);
    }

    private void DrawSaveDebugMetrics(RenderContext context)
    {
        if (_lastSave is null)
        {
            return;
        }

        context.DebugText.Draw(
            new Vector2(12, 322),
            $"LAST SAVE: {_lastSave.Reason} chunks:{_lastSave.WorldChunksConsidered} entities:{_lastSave.RuntimeEntitiesSaved} farm:{_lastSave.FarmPlotCount}",
            Color.LightGray,
            2);
    }

    private string? ResolveTileSpriteId(ushort tileId)
    {
        if (_content is null)
        {
            return null;
        }

        return _content.Tiles.GetByNumericId(tileId).TexturePath;
    }

    private CommandResult ExecuteDebugCommand(string command)
    {
        if (_content is null || _inventory is null)
        {
            return CommandResult.Failure("Playing state is not initialized.");
        }

        return _developerConsole.Execute(command, CreateCommandContext());
    }

    private CommandContext CreateCommandContext()
    {
        return new CommandContext
        {
            Content = _content,
            World = _world,
            PlayerLoadoutInventory = _inventory,
            WorldTime = _worldTime,
            EntityManager = _entities,
            EntityFactory = _entityFactory,
            PlayerPosition = _player?.Body.Position,
            Events = _events
        };
    }
    private void ReturnToMainMenu()
    {
        _states.ChangeState(new MainMenuState(_states, _states.RequestExit));
    }

    private void UpdateAutosave(float deltaSeconds, GameSettings settings)
    {
        if (_world is null ||
            _player is null ||
            _inventory is null ||
            string.IsNullOrWhiteSpace(_worldSaveDirectory))
        {
            return;
        }

        var intervalSeconds = settings.Gameplay.AutosaveMinutes * 60f;
        var result = _saves.TickAutosave(
            deltaSeconds,
            intervalSeconds,
            new GameSaveRequest(_world, _player, _inventory, _entities)
            {
                TileEntities = _tileEntities,
                FarmPlots = _farmPlots,
                EquipmentLoadout = _equipmentLoadout,
                CharacterAppearance = _characterEditorOverlay.Appearance,
                WorldTime = _worldTime,
                RandomStreams = _simulation?.RandomStreams,
                WorldEventState = _simulation?.LivingWorld.CaptureWorldEventState()
            },
            _worldSaveDirectory,
            new GameSaveCoordinatorOptions
            {
                ChunkStorageMode = WorldChunkStorageMode.RegionFiles,
                WorldSaveMode = WorldSaveMode.DirtyChunksOnly,
                PlayerDisplayName = _world.Metadata.Name
            },
            _events);

        if (result is not null)
        {
            _lastSave = result;
        }
    }

    private void EnsureVisibleChunks(double deltaSeconds)
    {
        if (_world is null ||
            !_world.IsHorizontallyInfinite ||
            _session?.WorldGenerationProfile is not { } profile)
        {
            return;
        }

        var minTile = CoordinateUtils.WorldToTile(_camera.VisibleWorldRect.Left, _camera.VisibleWorldRect.Top);
        var maxTile = CoordinateUtils.WorldToTile(_camera.VisibleWorldRect.Right, _camera.VisibleWorldRect.Bottom);
        var visibleTiles = RectI.FromInclusiveTileBounds(minTile.X, minTile.Y, maxTile.X, maxTile.Y);
        var worldSettings = _pauseMenu.Settings.World;
        var streamingSaveDirectory = worldSettings.SaveChunksBeforeUnload
            ? _worldSaveDirectory
            : null;
        var viewKey = new ChunkStreamingViewKey(
            CoordinateUtils.TileToChunk(minTile),
            CoordinateUtils.TileToChunk(maxTile),
            worldSettings,
            streamingSaveDirectory);
        var viewChanged = !_hasStreamingViewKey || !_lastStreamingViewKey.Equals(viewKey);
        var telemetry = _streaming.Telemetry;
        var hasPendingWork = telemetry.PendingLoadJobs > 0 ||
            telemetry.PendingSaveJobs > 0 ||
            telemetry.ApplyQueueLength > 0 ||
            telemetry.PendingRetryJobs > 0 ||
            _lastStreaming.DeferredLoadChunks > 0 ||
            _lastStreaming.DeferredUnloadChunks > 0;

        _streamingUpdateElapsedSeconds += Math.Max(0d, deltaSeconds);
        if (!viewChanged && !hasPendingWork)
        {
            return;
        }

        const double maximumStreamingUpdateRate = 30d;
        if (!viewChanged && _streamingUpdateElapsedSeconds < 1d / maximumStreamingUpdateRate)
        {
            return;
        }

        _lastStreamingViewKey = viewKey;
        _hasStreamingViewKey = true;
        _streamingUpdateElapsedSeconds = 0d;
        _lastStreaming = _streaming.Update(_world, profile, visibleTiles, streamingSaveDirectory, new ChunkStreamingOptions
        {
            LoadMarginChunks = worldSettings.ChunkLoadMargin,
            UnloadMarginChunks = worldSettings.ChunkUnloadMargin,
            KeepDirtyChunksLoaded = worldSettings.KeepDirtyChunksLoaded,
            MaxChunkOperationsPerUpdate = worldSettings.StreamingBudgetChunksPerFrame,
            MaxConcurrentLoadJobs = worldSettings.StreamingConcurrentLoads,
            MaxConcurrentSaveJobs = worldSettings.StreamingConcurrentSaves,
            MaxApplyQueueLength = worldSettings.StreamingApplyQueueLimit,
            MaxApplyTimePerUpdate = TimeSpan.FromMilliseconds(worldSettings.StreamingApplyBudgetMilliseconds),
            MaxApplyDecodedBytesPerUpdate = worldSettings.StreamingApplyBudgetKilobytes * 1024L,
            RetryPolicy = new ChunkStreamingRetryPolicy
            {
                MaxAttempts = worldSettings.StreamingRetryAttempts,
                InitialBackoffUpdates = worldSettings.StreamingRetryInitialBackoffUpdates,
                MaxBackoffUpdates = worldSettings.StreamingRetryMaximumBackoffUpdates
            }
        }, _events);
    }

    private readonly record struct ChunkStreamingViewKey(
        ChunkPos MinimumVisibleChunk,
        ChunkPos MaximumVisibleChunk,
        WorldSettings Settings,
        string? SaveDirectory);

    private PlayerStatBlock ResolvePlayerStats()
    {
        return _frameSnapshot?.Player.Stats ?? PlayerStatBlock.Base;
    }

    private void DrainFeedbackCues()
    {
        if (_feedbackRouter is null)
        {
            return;
        }

        var quality = _pauseMenu.Settings.Rendering.ParticleQuality;
        while (true)
        {
            var count = _feedbackRouter.DrainTo(_feedbackCueDrainBuffer);
            for (var index = 0; index < count; index++)
            {
                _particles.Emit(_feedbackCueDrainBuffer[index], quality);
            }

            if (count < _feedbackCueDrainBuffer.Length)
            {
                break;
            }
        }

        while (true)
        {
            var count = _feedbackRouter.DrainAudioTo(_audioCueDrainBuffer);
            for (var index = 0; index < count; index++)
            {
                var cue = _audioCueDrainBuffer[index];
                _audio.PlayOneShot(new AudioPlayRequest(
                    cue.AudioId,
                    cue.Bus,
                    cue.Volume,
                    cue.Pitch,
                    cue.Priority,
                    cue.CooldownSeconds,
                    cue.IsSpatial,
                    cue.WorldPosition,
                    cue.MaximumDistance));
            }

            if (count < _audioCueDrainBuffer.Length)
            {
                break;
            }
        }
    }

    private System.Numerics.Vector2? ResolveFeedbackEntityPosition(int entityId)
    {
        if (_player is not null && _player.Id == entityId)
        {
            return _player.Body.Center;
        }

        var entity = _entities.Entities.FirstOrDefault(candidate => candidate.Id == entityId);
        return entity?.Position;
    }

    private System.Numerics.Vector2 ResolveFeedbackFocusPosition()
    {
        return _player?.Body.Center ?? default;
    }

    private bool IsRareFeedbackItem(string itemId)
    {
        return _content?.Items.TryGetById(itemId, out var item) == true &&
            item.Rarity >= ItemRarity.Rare &&
            item.Rarity != ItemRarity.Quest;
    }

    private static string GetHotbarBinding(KeyBindingSettings bindings, int slot)
    {
        return slot switch
        {
            0 => bindings.Hotbar1,
            1 => bindings.Hotbar2,
            2 => bindings.Hotbar3,
            3 => bindings.Hotbar4,
            4 => bindings.Hotbar5,
            5 => bindings.Hotbar6,
            6 => bindings.Hotbar7,
            7 => bindings.Hotbar8,
            8 => bindings.Hotbar9,
            _ => bindings.Hotbar10
        };
    }

}
