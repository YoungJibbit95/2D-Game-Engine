using Game.Client.Input;
using Game.Client.Rendering;
using Game.Client.UI;
using Game.Core;
using Game.Core.Actions;
using Game.Client.Configuration;
using Game.Core.Commands;
using Game.Core.Data;
using Game.Core.Diagnostics;
using Game.Core.Entities;
using Game.Core.Events;
using Game.Core.Interaction;
using Game.Core.Inventory;
using Game.Core.Items;
using Game.Core.Lighting;
using Game.Core.Physics;
using Game.Core.Projectiles;
using Game.Core.Saving;
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

namespace Game.Client.GameStates;

public sealed class PlayingState : IGameState, ITextInputReceiver, IKeyboardCaptureState
{
    private readonly InputManager _input = new();
    private readonly Camera2D _camera = new();
    private readonly TilemapRenderer _tilemapRenderer = new();
    private readonly LightingRenderer _lightingRenderer = new();
    private readonly TileCollisionResolver _collisionResolver = new();
    private readonly PlayerItemUseSystem _itemUse = new();
    private readonly InteractionTargetingSystem _targeting = new();
    private readonly ChunkStreamingService _streaming = new();
    private readonly GameSaveCoordinator _saves = new();
    private readonly EngineDebugSnapshotBuilder _debugSnapshots = new();
    private readonly CommandDispatcher _commands = new(CommandRegistry.CreateDefault());
    private readonly HudOverlay _hud = new();
    private readonly DebugConsoleOverlay _debugConsole = new();
    private readonly PauseMenuOverlay _pauseMenu;
    private readonly GameStateManager _states;
    private readonly EntityFactory _entityFactory;
    private readonly LoadedGameSession? _loadedSession;

    private World? _world;
    private PlayerEntity? _player;
    private EntityManager _entities = new();
    private GameEventBus _events = new();
    private WorldTime _worldTime = new();
    private GameContentDatabase? _content;
    private PlayerInventory? _inventory;
    private TileEntityManager _tileEntities = new();
    private TilePos? _hoverTile;
    private TilePos? _interactionTile;
    private Rectangle _lastViewportBounds = new(0, 0, 1280, 720);
    private EngineDebugSnapshot? _debugSnapshot;
    private double _nextDebugSnapshotAt;
    private bool _showGrid;
    private int _selectedHotbarSlot;
    private string? _worldSaveDirectory;
    private ClientTextureRegistry? _textures;
    private ChunkStreamingUpdateResult _lastStreaming = ChunkStreamingUpdateResult.Empty;
    private GameSaveResult? _lastSave;

    public PlayingState(GameStateManager states, LoadedGameSession? loadedSession = null)
    {
        _states = states;
        _loadedSession = loadedSession;
        _entityFactory = new EntityFactory(_collisionResolver);
        _pauseMenu = new PauseMenuOverlay(
            ClientPaths.SettingsPath(),
            resume: static () => { },
            mainMenu: ReturnToMainMenu,
            exitGame: _states.RequestExit,
            settingsChanged: _states.ApplySettings);
    }

    public string Name => "Playing";

    public bool CapturesKeyboard => _debugConsole.IsOpen || _pauseMenu.IsOpen;

    public void Initialize()
    {
        var session = _loadedSession ?? WorldSessionFactory.CreateSingleplayer(seed: 1337, worldName: "Debug World");
        _content = session.Content;
        _inventory = session.Inventory;
        _world = session.World;
        _player = session.Player;
        _entities = session.Entities;
        _events = session.Events;
        _worldTime = session.WorldTime;
        _worldSaveDirectory = session.WorldSaveDirectory;
        _tileEntities = session.TileEntities ?? new TileEntityManager();
        _selectedHotbarSlot = _inventory.SelectedHotbarSlot;

        _camera.Position = new Vector2(_player.Body.Center.X, _player.Body.Center.Y);
        _camera.Zoom = _pauseMenu.Settings.Gameplay.CameraZoom;
    }

    public void LoadContent(ContentManager content)
    {
    }

    public void FixedUpdate(float fixedDeltaSeconds)
    {
        if (_pauseMenu.IsOpen)
        {
            _player?.SetCommand(PlayerCommand.None);
            return;
        }

        _worldTime.Update(fixedDeltaSeconds);

        if (_world is not null && _player is not null)
        {
            _player.Update(_world, fixedDeltaSeconds);
            _entities.UpdateAll(_world, fixedDeltaSeconds);
        }
    }

    public void Update(double deltaSeconds)
    {
        _input.Update();
        var settings = _pauseMenu.Settings;

        if (_pauseMenu.IsOpen)
        {
            _pauseMenu.Update(_input);
            _player?.SetCommand(PlayerCommand.None);
            return;
        }

        if (_debugConsole.Update(_input, ExecuteDebugCommand, settings.Input.KeyBindings.DebugConsole))
        {
            _player?.SetCommand(PlayerCommand.None);
            return;
        }

        if (_input.IsBindingPressed(settings.Input.KeyBindings.Pause))
        {
            _pauseMenu.Open();
            _player?.SetCommand(PlayerCommand.None);
            return;
        }

        _showGrid = settings.Debug.ShowGrid || _input.IsBindingDown(settings.Input.KeyBindings.DebugToggle);
        UpdateHotbarSelection(settings.Input);
        _player?.SetCommand(PlayerCommandBuilder.Build(_input, settings.Input.KeyBindings));
        UpdateItemUse((float)deltaSeconds, settings);
        UpdateAutosave((float)deltaSeconds, settings);
    }

    public void Draw(RenderContext context)
    {
        if (_world is null)
        {
            return;
        }

        _lastViewportBounds = context.ViewportBounds;
        var settings = _pauseMenu.Settings;
        EnsureTextureRegistry(context);
        _camera.Zoom = settings.Gameplay.CameraZoom;
        var cameraTarget = GetCameraTarget();
        _camera.Follow(cameraTarget, context.ViewportBounds, smoothing: 0.18f);
        EnsureVisibleChunks();

        context.SpriteBatch.Draw(
            context.Pixel,
            context.ViewportBounds,
            new Color(91, 155, 213));

        _tilemapRenderer.ShowGrid = _showGrid;
        _tilemapRenderer.DrawLiquids = settings.Rendering.DrawLiquids;
        _tilemapRenderer.Textures = _textures;
        _tilemapRenderer.TileSpriteResolver = ResolveTileSpriteId;
        _tilemapRenderer.Tiles = _content?.Tiles;
        _tilemapRenderer.Draw(context, _world, _camera);
        DrawPlayer(context);
        DrawEntities(context);
        if (settings.Rendering.DrawLightingOverlay)
        {
            _lightingRenderer.Draw(context, _world, _camera);
        }

        _hud.Draw(context, _selectedHotbarSlot, _player?.Health ?? 0, _player?.MaxHealth ?? 100);

        if (settings.Rendering.DrawDebugOverlays && settings.Debug.ShowDebugOverlay)
        {
            var worldTimeText = _worldTime.NormalizedTimeOfDay.ToString("0.000", CultureInfo.InvariantCulture);
            context.DebugText.Draw(new Vector2(12, 58), $"WORLD TIME: {worldTimeText}", Color.LightGray, 2);
            context.DebugText.Draw(new Vector2(12, 82), $"CHUNKS: {_world.Chunks.Count}", Color.LightGray, 2);
            context.DebugText.Draw(new Vector2(12, 106), $"ENTITIES: {_entities.Entities.Count}", Color.LightGray, 2);
            if (_player is not null)
            {
                var playerTile = CoordinateUtils.WorldToTile(_player.Body.Center.X, _player.Body.Center.Y);
                context.DebugText.Draw(new Vector2(12, 130), $"PLAYER TILE: {playerTile.X}:{playerTile.Y}", Color.LightGray, 2);
            }

            if (_hoverTile is { } hoverTile)
            {
                context.DebugText.Draw(new Vector2(12, 154), $"MOUSE TILE: {hoverTile.X}:{hoverTile.Y}", Color.LightGray, 2);
            }

            DrawEngineDebugSnapshot(context);
            DrawRenderDebugMetrics(context);
            DrawStreamingDebugMetrics(context);
            DrawSaveDebugMetrics(context);
        }

        if (settings.Gameplay.ShowInteractionTarget)
        {
            DrawInteractionTarget(context);
        }

        _debugConsole.Draw(context);
        _pauseMenu.Draw(context);
    }

    public void Dispose()
    {
        _textures?.Dispose();
    }

    public void OnTextInput(char character)
    {
        _debugConsole.OnTextInput(character);
    }

    private Vector2 GetCameraTarget()
    {
        if (_player is null)
        {
            return Vector2.Zero;
        }

        return new Vector2(_player.Body.Center.X, _player.Body.Center.Y);
    }

    private void DrawPlayer(RenderContext context)
    {
        if (_player is null)
        {
            return;
        }

        var screenPosition = _camera.WorldToScreen(
            new Vector2(_player.Body.Position.X, _player.Body.Position.Y),
            context.ViewportBounds);

        var destination = new Rectangle(
            (int)MathF.Floor(screenPosition.X),
            (int)MathF.Floor(screenPosition.Y),
            (int)MathF.Ceiling(_player.Body.Size.X * _camera.Zoom),
            (int)MathF.Ceiling(_player.Body.Size.Y * _camera.Zoom));

        context.SpriteBatch.Draw(context.Pixel, destination, new Color(230, 204, 138));
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(destination.X, destination.Y, destination.Width, 2), new Color(80, 52, 48));
    }

    private void DrawEntities(RenderContext context)
    {
        foreach (var entity in _entities.Entities)
        {
            if (!entity.IsActive)
            {
                continue;
            }

            var color = entity switch
            {
                EnemyEntity => new Color(98, 181, 94),
                DroppedItemEntity => new Color(239, 203, 105),
                ProjectileEntity => new Color(224, 86, 72),
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

    private void UpdateItemUse(float deltaSeconds, GameSettings settings)
    {
        if (_world is null || _content is null || _player is null || _inventory is null)
        {
            return;
        }

        _itemUse.Update(deltaSeconds);
        var mouseWorld = _camera.ScreenToWorld(_input.MousePosition.ToVector2(), _lastViewportBounds);
        _hoverTile = CoordinateUtils.WorldToTile(mouseWorld.X, mouseWorld.Y);
        _interactionTile = ResolveInteractionTarget(mouseWorld, settings.Gameplay.InteractionReachPixels);

        var useInputActive = settings.Gameplay.HoldToMine
            ? _input.IsBindingDown(settings.Input.KeyBindings.AttackPrimary)
            : _input.IsBindingPressed(settings.Input.KeyBindings.AttackPrimary);

        if (!useInputActive || _interactionTile is not { } targetTile)
        {
            return;
        }

        _itemUse.UseSelectedItem(
            _world,
            _content,
            _player,
            _inventory,
            _entities,
            targetTile,
            new System.Numerics.Vector2(mouseWorld.X, mouseWorld.Y),
            deltaSeconds,
            _events);
    }

    private TilePos? ResolveInteractionTarget(Vector2 mouseWorld, float reachPixels)
    {
        if (_world is null || _content is null || _player is null || _inventory is null)
        {
            return null;
        }

        var selected = _inventory.SelectedStack;
        if (selected.IsEmpty || ! _content.Items.TryGetById(selected.ItemId, out var item))
        {
            return _hoverTile;
        }

        var aim = new System.Numerics.Vector2(mouseWorld.X, mouseWorld.Y);
        var target = item.Type switch
        {
            ItemType.PlaceableTile => _targeting.FindPlacementTarget(_world, _player.Body.Center, aim, reachPixels),
            ItemType.ToolPickaxe => _targeting.FindMiningTarget(_world, _player.Body.Center, aim, reachPixels),
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

    private void DrawEngineDebugSnapshot(RenderContext context)
    {
        if (_world is null)
        {
            return;
        }

        if (_debugSnapshot is null || context.Time.TotalSeconds >= _nextDebugSnapshotAt)
        {
            _debugSnapshot = _debugSnapshots.Build(_world, _entities, _worldTime);
            _nextDebugSnapshotAt = context.Time.TotalSeconds + 0.5;
        }

        var snapshot = _debugSnapshot;
        context.DebugText.Draw(new Vector2(12, 178), $"DIRTY: {snapshot.DirtyChunkCount} LIQ: {snapshot.LiquidTileCount}", Color.LightGray, 2);
        context.DebugText.Draw(new Vector2(12, 202), $"SURF: {snapshot.MinSurfaceY}-{snapshot.MaxSurfaceY}", Color.LightGray, 2);
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
            $"TILE CMDS: {metrics.RenderedTileCommands} LIQ CMDS: {metrics.RenderedLiquidCommands} EVICT: {metrics.EvictedChunks}",
            Color.LightGray,
            2);
    }

    private void DrawStreamingDebugMetrics(RenderContext context)
    {
        context.DebugText.Draw(
            new Vector2(12, 274),
            $"STREAM LOAD:{_lastStreaming.LoadedChunks} GEN:{_lastStreaming.GeneratedChunks} SAVE:{_lastStreaming.SavedChunksBeforeUnload} UNLOAD:{_lastStreaming.UnloadedChunks}",
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
            new Vector2(12, 298),
            $"LAST SAVE: {_lastSave.Reason} chunks:{_lastSave.WorldChunksConsidered} entities:{_lastSave.RuntimeEntitiesSaved}",
            Color.LightGray,
            2);
    }

    private void EnsureTextureRegistry(RenderContext context)
    {
        if (_textures is not null || _content is null)
        {
            return;
        }

        _textures = new ClientTextureRegistry(context.GraphicsDevice, ClientPaths.FindGameDataRoot(), _content.SpriteAssets);
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

        return _commands.Execute(command, new CommandContext
        {
            Content = _content,
            World = _world,
            PlayerLoadoutInventory = _inventory,
            WorldTime = _worldTime,
            EntityManager = _entities,
            EntityFactory = _entityFactory,
            PlayerPosition = _player?.Body.Position,
            Events = _events
        });
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
                TileEntities = _tileEntities
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

    private void EnsureVisibleChunks()
    {
        if (_world is null ||
            !_world.IsHorizontallyInfinite ||
            _loadedSession?.WorldGenerationProfile is not { } profile)
        {
            return;
        }

        var minTile = CoordinateUtils.WorldToTile(_camera.VisibleWorldRect.Left, _camera.VisibleWorldRect.Top);
        var maxTile = CoordinateUtils.WorldToTile(_camera.VisibleWorldRect.Right, _camera.VisibleWorldRect.Bottom);
        var visibleTiles = RectI.FromInclusiveTileBounds(minTile.X, minTile.Y, maxTile.X, maxTile.Y);
        var worldSettings = _pauseMenu.Settings.World;
        _lastStreaming = _streaming.Update(_world, profile, visibleTiles, _worldSaveDirectory, new ChunkStreamingOptions
        {
            LoadMarginChunks = worldSettings.ChunkLoadMargin,
            UnloadMarginChunks = worldSettings.ChunkUnloadMargin,
            KeepDirtyChunksLoaded = worldSettings.KeepDirtyChunksLoaded
        }, _events);
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
