using Game.Client.Input;
using Game.Client.Rendering;
using Game.Core.Animations;
using Game.Core.Characters;
using Game.Client.UI;
using Game.Core;
using Game.Core.Actions;
using Game.Client.Configuration;
using Game.Core.Commands;
using Game.Core.Data;
using Game.Core.Diagnostics;
using Game.Core.Entities;
using Game.Core.Equipment;
using Game.Core.Events;
using Game.Core.Farming;
using Game.Core.Interaction;
using Game.Core.Inventory;
using Game.Core.Items;
using Game.Core.Lighting;
using Game.Core.Physics;
using Game.Core.Projectiles;
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

public sealed class PlayingState : IGameState, ITextInputReceiver, IKeyboardCaptureState
{
    private readonly InputManager _input = new();
    private readonly Camera2D _camera = new();
    private readonly TilemapRenderer _tilemapRenderer = new();
    private readonly ParallaxBackgroundRenderer _backgroundRenderer = new();
    private readonly LightingRenderer _lightingRenderer = new();
    private readonly TileCollisionResolver _collisionResolver = new();
    private readonly PlayerItemUseSystem _itemUse = new();
    private readonly InteractionTargetingSystem _targeting = new();
    private readonly ItemPickupSystem _pickup = new();
    private readonly ChunkStreamingService _streaming = new();
    private readonly GameSaveCoordinator _saves = new();
    private readonly EngineDebugSnapshotBuilder _debugSnapshots = new();
    private readonly CommandDispatcher _commands = new(CommandRegistry.CreateDefault());
    private readonly HudOverlay _hud = new();
    private readonly InventoryOverlay _inventoryOverlay = new();
    private readonly CraftingOverlay _craftingOverlay = new();
    private readonly CharacterEditorOverlay _characterEditorOverlay = new();
    private readonly DebugConsoleOverlay _debugConsole = new();
    private readonly PauseMenuOverlay _pauseMenu;
    private readonly GameStateManager _states;
    private readonly EntityFactory _entityFactory;
    private readonly SpriteAnimator _playerAnimator = new();
    private readonly CharacterAnimationStateResolver _playerAnimationResolver = new();
    private readonly EquipmentLoadout _equipmentLoadout = new();
    private readonly EquipmentStatCalculator _equipmentStats = new();
    private readonly LoadedGameSession? _loadedSession;

    private World? _world;
    private PlayerEntity? _player;
    private EntityManager _entities = new();
    private GameEventBus _events = new();
    private WorldTime _worldTime = new();
    private GameContentDatabase? _content;
    private PlayerInventory? _inventory;
    private TileEntityManager _tileEntities = new();
    private FarmPlotManager _farmPlots = new();
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
    private int _lastFarmDay = 1;
    private int _lastPickedUpItems;
    private CharacterAnimationState _playerAnimationState = CharacterAnimationState.Idle;
    private CharacterAnimationState? _playerActionAnimation;
    private float _playerActionAnimationTimer;
    private bool _playerFacingLeft;

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

    public bool CapturesKeyboard => _debugConsole.IsOpen || _pauseMenu.IsOpen || _inventoryOverlay.IsOpen || _craftingOverlay.IsOpen || _characterEditorOverlay.IsOpen;

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
        _farmPlots = session.FarmPlots ?? new FarmPlotManager();
        _selectedHotbarSlot = _inventory.SelectedHotbarSlot;
        _lastFarmDay = _worldTime.Day;

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

        var previousDay = _worldTime.Day;
        _worldTime.Update(fixedDeltaSeconds);
        if (_content is not null && _worldTime.Day != previousDay && _worldTime.Day != _lastFarmDay)
        {
            new FarmingSystem().AdvanceDay(_content.Crops, _farmPlots, ResolveCurrentFarmSeason());
            _lastFarmDay = _worldTime.Day;
        }

        if (_world is not null && _player is not null)
        {
            ApplyPlayerStats();
            _player.Update(_world, fixedDeltaSeconds);
            _entities.UpdateAll(_world, fixedDeltaSeconds);
            UpdateItemPickup();
            UpdatePlayerAnimation(fixedDeltaSeconds);
        }
    }

    public void Update(double deltaSeconds)
    {
        _input.Update();
        var settings = _pauseMenu.Settings;

        if (_pauseMenu.IsOpen)
        {
            _pauseMenu.Update(_input, deltaSeconds);
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

        if (_content is not null && _characterEditorOverlay.Update(_input, _content, settings, deltaSeconds))
        {
            _player?.SetCommand(PlayerCommand.None);
            UpdateAutosave((float)deltaSeconds, settings);
            return;
        }

        if (_inventory is not null &&
            _content is not null &&
            _world is not null &&
            _player is not null &&
            _craftingOverlay.Update(_input, _inventory, _content, _world, _player, settings))
        {
            _player.SetCommand(PlayerCommand.None);
            UpdateAutosave((float)deltaSeconds, settings);
            return;
        }

        if (_inventory is not null &&
            _content is not null &&
            _inventoryOverlay.Update(_input, _inventory, _content.Items, _equipmentLoadout, settings))
        {
            _player?.SetCommand(PlayerCommand.None);
            UpdateAutosave((float)deltaSeconds, settings);
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
        var cameraTarget = GetCameraTarget(settings);
        _camera.Follow(cameraTarget, context.ViewportBounds, smoothing: 0.18f);
        EnsureVisibleChunks();

        _backgroundRenderer.Draw(context, _textures, _camera, _world, _worldTime);

        _tilemapRenderer.ShowGrid = _showGrid;
        _tilemapRenderer.DrawLiquids = settings.Rendering.DrawLiquids;
        _tilemapRenderer.LiquidOpacity = settings.Rendering.LiquidOpacity;
        _tilemapRenderer.MaxCachedChunks = settings.Rendering.MaxChunkRenderCacheEntries;
        _tilemapRenderer.Textures = _textures;
        _tilemapRenderer.TileSpriteResolver = ResolveTileSpriteId;
        _tilemapRenderer.Tiles = _content?.Tiles;
        _tilemapRenderer.Draw(context, _world, _camera);
        DrawFarmPlots(context);
        DrawPlayer(context);
        DrawEntities(context);
        if (settings.Rendering.DrawLightingOverlay)
        {
            _lightingRenderer.Draw(context, _world, _camera);
        }

        var playerStats = ResolvePlayerStats();
        _hud.Draw(
            context,
            _inventory,
            _content?.Items,
            _textures,
            _player?.Health ?? 0,
            _player?.MaxHealth ?? 100,
            _player?.Mana ?? 0,
            _player?.MaxMana ?? playerStats.MaxMana,
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

            context.DebugText.Draw(new Vector2(12, 322), $"FARM PLOTS: {_farmPlots.Plots.Count}", Color.LightGray, 2);
            context.DebugText.Draw(new Vector2(12, 346), $"PICKUP LAST: {_lastPickedUpItems}", Color.LightGray, 2);
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

    private Vector2 GetCameraTarget(GameSettings settings)
    {
        if (_player is null)
        {
            return Vector2.Zero;
        }

        var lookAhead = settings.Gameplay.CameraLookAheadPixels;
        var velocity = _player.Body.Velocity;
        var offsetX = Math.Abs(velocity.X) < 1f ? 0f : MathF.Sign(velocity.X) * lookAhead;
        return new Vector2(_player.Body.Center.X + offsetX, _player.Body.Center.Y);
    }

    private void DrawPlayer(RenderContext context)
    {
        if (_player is null)
        {
            return;
        }

        if (TryDrawPlayerSprite(context))
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

    private bool TryDrawPlayerSprite(RenderContext context)
    {
        if (_player is null || _textures is null || _playerAnimator.CurrentFrame is not { } frame)
        {
            return false;
        }

        var sprite = _textures.Get(frame.SpriteId, frame.FrameIndex);
        if (sprite.SourceRectangle.Width <= 0 || sprite.SourceRectangle.Height <= 0)
        {
            return false;
        }

        var source = sprite.SourceRectangle;
        var worldPosition = new Vector2(
            _player.Body.Center.X - source.Width * 0.5f + frame.OffsetX,
            _player.Body.Position.Y + _player.Body.Size.Y - source.Height + frame.OffsetY);
        worldPosition += ResolveEntityVisualOffset(_player, context.Time.TotalSeconds);
        var screenPosition = _camera.WorldToScreen(worldPosition, context.ViewportBounds);
        var destination = new Rectangle(
            (int)MathF.Floor(screenPosition.X),
            (int)MathF.Floor(screenPosition.Y),
            Math.Max(1, (int)MathF.Ceiling(source.Width * _camera.Zoom)),
            Math.Max(1, (int)MathF.Ceiling(source.Height * _camera.Zoom)));
        var effects = _playerFacingLeft ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

        context.SpriteBatch.Draw(sprite.Texture, destination, source, Color.White, 0f, Vector2.Zero, effects, 0f);
        DrawPlayerAppearanceParts(context, destination, effects);
        return true;
    }

    private void DrawPlayerAppearanceParts(RenderContext context, Rectangle destination, SpriteEffects effects)
    {
        if (_textures is null)
        {
            return;
        }

        var appearance = _characterEditorOverlay.Appearance;
        DrawPlayerPart(context, destination, effects, "entities/player/body_variants", ResolveSkinFrame(appearance.SkinTone), ParseHexColor(appearance.SkinTone));
        DrawPlayerPart(context, destination, effects, "entities/player/clothes_variants_v2", ResolveClothesFrame(appearance.ClothesStyleId), ParseHexColor(appearance.ShirtColor));
        DrawPlayerPart(context, destination, effects, "entities/player/hair_variants_v2", ResolveHairFrame(appearance.HairStyleId), ParseHexColor(appearance.HairColor));
        if (!string.Equals(appearance.AccessoryId, "none", StringComparison.OrdinalIgnoreCase))
        {
            DrawPlayerPart(context, destination, effects, "entities/player/accessories_hats", Math.Max(0, ResolveAccessoryFrame(appearance.AccessoryId) - 1), Color.White);
        }
    }

    private void DrawPlayerPart(RenderContext context, Rectangle destination, SpriteEffects effects, string spriteId, int frameIndex, Color color)
    {
        if (_textures is null)
        {
            return;
        }

        var sprite = _textures.Get(spriteId, frameIndex);
        if (sprite.IsPlaceholder || sprite.SourceRectangle.Width <= 0 || sprite.SourceRectangle.Height <= 0)
        {
            return;
        }

        context.SpriteBatch.Draw(sprite.Texture, destination, sprite.SourceRectangle, color, 0f, Vector2.Zero, effects, 0f);
    }

    private bool TryDrawEntitySprite(RenderContext context, Entity entity)
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

    private static SpriteEffects ResolveEntitySpriteEffects(Entity entity)
    {
        return entity switch
        {
            EnemyEntity enemy when enemy.Body.Velocity.X < -1f => SpriteEffects.FlipHorizontally,
            EnemyEntity => SpriteEffects.None,
            DroppedItemEntity => SpriteEffects.None,
            ProjectileEntity projectile when projectile.Velocity.X < 0 => SpriteEffects.FlipHorizontally,
            _ => SpriteEffects.None
        };
    }

    private static Color ResolveEntityTint(Entity entity, double totalSeconds)
    {
        if (entity is EnemyEntity { Health.InvulnerabilityTimeRemaining: > 0 })
        {
            return Math.Floor(totalSeconds * 18) % 2 == 0
                ? new Color(255, 210, 210)
                : Color.White;
        }

        if (entity is ProjectileEntity projectile && projectile.DamageType == Game.Core.Combat.DamageType.Magic)
        {
            return new Color(170, 210, 255);
        }

        return Color.White;
    }

    private static Vector2 ResolveEntityVisualOffset(Entity entity, double totalSeconds)
    {
        return entity switch
        {
            DroppedItemEntity => new Vector2(0, MathF.Sin((float)(totalSeconds * 5.5 + entity.Id)) * 2f),
            EnemyEntity enemy when enemy.DefinitionId.Contains("bat", StringComparison.OrdinalIgnoreCase) => new Vector2(0, MathF.Sin((float)(totalSeconds * 6 + entity.Id)) * 3f),
            _ => Vector2.Zero
        };
    }

    private int ResolveEntityFrameIndex(Entity entity, string spriteId, double totalSeconds)
    {
        if (_content is null ||
            !_content.SpriteAssets.TryGetById(spriteId, out var asset) ||
            asset.Frames.Count <= 1 ||
            !asset.HasTag("animated") ||
            entity is DroppedItemEntity or ProjectileEntity)
        {
            return 0;
        }

        var frameRate = entity is EnemyEntity enemy && enemy.DefinitionId.Contains("bat", StringComparison.OrdinalIgnoreCase)
            ? 10.0
            : 6.0;
        return (int)Math.Floor(totalSeconds * frameRate + entity.Id * 0.37) % asset.Frames.Count;
    }

    private bool TryResolveEntitySpriteId(Entity entity, out string spriteId)
    {
        spriteId = string.Empty;
        if (_content is null)
        {
            return false;
        }

        switch (entity)
        {
            case EnemyEntity enemy when _content.Entities.TryGetById(enemy.DefinitionId, out var enemyDefinition):
                spriteId = enemyDefinition.TexturePath;
                return true;
            case DroppedItemEntity dropped when _content.Items.TryGetById(dropped.Stack.ItemId, out var itemDefinition):
                spriteId = itemDefinition.TexturePath;
                return true;
            case ProjectileEntity projectile when _content.Projectiles.TryGetById(projectile.ProjectileId, out var projectileDefinition):
                spriteId = projectileDefinition.TexturePath;
                return true;
            default:
                return false;
        }
    }

    private void DrawEntities(RenderContext context)
    {
        foreach (var entity in _entities.Entities)
        {
            if (!entity.IsActive)
            {
                continue;
            }

            if (TryDrawEntitySprite(context, entity))
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

    private void UpdatePlayerAnimation(float deltaSeconds)
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

        if (_playerActionAnimationTimer > 0)
        {
            _playerActionAnimationTimer = Math.Max(0, _playerActionAnimationTimer - deltaSeconds);
        }
        else
        {
            _playerActionAnimation = null;
        }

        var resolvedState = _playerActionAnimation ?? _playerAnimationResolver.Resolve(
            _player.Body,
            isDead: _player.HealthComponent.IsDead);
        _playerAnimationState = resolvedState;

        if (TryResolvePlayerAnimationClip(resolvedState, out var clip) &&
            !string.Equals(_playerAnimator.Clip?.Id, clip.Id, StringComparison.OrdinalIgnoreCase))
        {
            _playerAnimator.Play(clip, restartIfSame: true);
        }

        _playerAnimator.Update(deltaSeconds);
    }

    private bool TryResolvePlayerAnimationClip(CharacterAnimationState state, out SpriteAnimationClip clip)
    {
        clip = null!;
        if (_content is null)
        {
            return false;
        }

        var clipId = _content.Characters.TryGetById("player", out var character)
            ? character.AnimationSet.ResolveClipId(state)
            : ResolveFallbackPlayerClipId(state);

        return !string.IsNullOrWhiteSpace(clipId) && _content.Animations.TryGetById(clipId, out clip!);
    }

    private static string ResolveFallbackPlayerClipId(CharacterAnimationState state)
    {
        return state switch
        {
            CharacterAnimationState.Walk => "player.walk",
            CharacterAnimationState.Jump => "player.jump",
            CharacterAnimationState.Fall => "player.fall",
            CharacterAnimationState.Attack => "player.attack",
            CharacterAnimationState.Mine => "player.mine",
            CharacterAnimationState.UseItem => "player.attack",
            _ => "player.idle"
        };
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

        _playerActionAnimation = state.Value;
        _playerActionAnimationTimer = state.Value == CharacterAnimationState.Attack ? 0.22f : 0.18f;
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

        TriggerPlayerActionAnimation(ResolvePlayerActionState(_inventory.SelectedStack));

        _itemUse.UseSelectedItem(
            _world,
            _content,
            _player,
            _inventory,
            _entities,
            targetTile,
            new System.Numerics.Vector2(mouseWorld.X, mouseWorld.Y),
            deltaSeconds,
            _events,
            _farmPlots,
            ResolveCurrentFarmSeason(),
            _worldTime.Day);
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

    private void DrawFarmPlots(RenderContext context)
    {
        if (_content is null)
        {
            return;
        }

        foreach (var plot in _farmPlots.Plots)
        {
            var worldRect = new RectI(
                plot.Position.X * GameConstants.TileSize,
                plot.Position.Y * GameConstants.TileSize,
                GameConstants.TileSize,
                GameConstants.TileSize);

            DrawWorldRect(context, worldRect, plot.IsWatered ? new Color(81, 116, 130, 150) : new Color(116, 79, 48, 150));

            if (plot.Crop is null || !_content.Crops.TryGetById(plot.Crop.CropId, out var crop))
            {
                continue;
            }

            var stage = plot.Crop.GetGrowthStageIndex(crop);
            var cropColor = stage >= crop.GrowthStageDays.Count - 1
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
            $"LAST SAVE: {_lastSave.Reason} chunks:{_lastSave.WorldChunksConsidered} entities:{_lastSave.RuntimeEntitiesSaved} farm:{_lastSave.FarmPlotCount}",
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
                TileEntities = _tileEntities,
                FarmPlots = _farmPlots
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

    private void UpdateItemPickup()
    {
        if (_player is null ||
            _inventory is null ||
            !_pauseMenu.Settings.Gameplay.AutoPickupItems ||
            _player.HealthComponent.IsDead)
        {
            _lastPickedUpItems = 0;
            return;
        }

        AttractNearbyDroppedItems();
        _lastPickedUpItems = _pickup.PickupItems(
            _entities,
            _inventory,
            _player.Bounds.Inflate(28),
            _events);
    }

    private void AttractNearbyDroppedItems()
    {
        if (_player is null)
        {
            return;
        }

        const float magnetRadius = 92f;
        const float magnetStrength = 360f;
        var playerCenter = _player.Body.Center;
        foreach (var dropped in _entities.Query(_player.Bounds.Inflate((int)magnetRadius)).OfType<DroppedItemEntity>())
        {
            var itemCenter = dropped.Body.Center;
            var offset = playerCenter - itemCenter;
            var distance = offset.Length();
            if (distance <= 0.001f || distance > magnetRadius)
            {
                continue;
            }

            var direction = offset / distance;
            var strength = (1f - distance / magnetRadius) * magnetStrength;
            dropped.Body.Velocity += direction * strength * (1f / 60f);
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
        var streamingSaveDirectory = worldSettings.SaveChunksBeforeUnload
            ? _worldSaveDirectory
            : null;
            _lastStreaming = _streaming.Update(_world, profile, visibleTiles, streamingSaveDirectory, new ChunkStreamingOptions
        {
            LoadMarginChunks = worldSettings.ChunkLoadMargin,
            UnloadMarginChunks = worldSettings.ChunkUnloadMargin,
            KeepDirtyChunksLoaded = worldSettings.KeepDirtyChunksLoaded
        }, _events);
    }

    private FarmSeason ResolveCurrentFarmSeason()
    {
        return (((_worldTime.Day - 1) / 28) % 4) switch
        {
            0 => FarmSeason.Spring,
            1 => FarmSeason.Summer,
            2 => FarmSeason.Fall,
            _ => FarmSeason.Winter
        };
    }

    private PlayerStatBlock ResolvePlayerStats()
    {
        if (_content is null)
        {
            return PlayerStatBlock.Base;
        }

        var equipmentStats = _equipmentStats.Calculate(PlayerStatBlock.Base, _equipmentLoadout, _content.Items);
        return _player?.StatusEffects.ApplyStatModifiers(equipmentStats) ?? equipmentStats;
    }

    private void ApplyPlayerStats()
    {
        if (_player is null)
        {
            return;
        }

        _player.ApplyStats(ResolvePlayerStats());
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

    private static int ResolveHairFrame(string hairStyle)
    {
        return hairStyle.ToUpperInvariant() switch
        {
            "MESSY" => 1,
            "BOB" => 2,
            "PONYTAIL" => 3,
            "BRAIDS" => 4,
            "MOHAWK" => 5,
            "LONG" => 6,
            "CAP" => 7,
            _ => 0
        };
    }

    private static int ResolveClothesFrame(string clothesStyle)
    {
        return clothesStyle.ToUpperInvariant() switch
        {
            "GREEN_WORK" => 1,
            "RED_TRAIL" => 2,
            "VIOLET_MAGE" => 3,
            "TAN_WORKER" => 4,
            "GREY_MINER" => 5,
            "TEAL_SCOUT" => 6,
            "OCHRE_FARMER" => 7,
            _ => 0
        };
    }

    private static int ResolveAccessoryFrame(string accessoryId)
    {
        return accessoryId.ToUpperInvariant() switch
        {
            "STRAW_HAT" => 1,
            "MINER_HELMET" => 2,
            "RED_BANDANA" => 3,
            "GLASSES" => 4,
            "LEAF_CROWN" => 5,
            "HOOD" => 6,
            _ => 0
        };
    }

    private static int ResolveSkinFrame(string skinTone)
    {
        return skinTone.ToUpperInvariant() switch
        {
            "#F0CFA3" => 0,
            "#E0B687" => 1,
            "#C98F62" => 2,
            "#8F563B" => 3,
            _ => 1
        };
    }

    private static Color ParseHexColor(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length != 7 || value[0] != '#')
        {
            return Color.White;
        }

        return byte.TryParse(value.AsSpan(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var r) &&
               byte.TryParse(value.AsSpan(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var g) &&
               byte.TryParse(value.AsSpan(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b)
            ? new Color(r, g, b)
            : Color.White;
    }

}
