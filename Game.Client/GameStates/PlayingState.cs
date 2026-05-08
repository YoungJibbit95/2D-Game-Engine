using Game.Client.Input;
using Game.Client.Rendering;
using Game.Client.UI;
using Game.Client.Configuration;
using Game.Core.Commands;
using Game.Core.Data;
using Game.Core.Entities;
using Game.Core.Events;
using Game.Core.Inventory;
using Game.Core.Lighting;
using Game.Core.Physics;
using Game.Core.Projectiles;
using Game.Core.Time;
using Game.Core;
using Game.Core.World;
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
    private readonly EntityManager _entities = new();
    private readonly GameEventBus _events = new();
    private readonly CommandDispatcher _commands = new(CommandRegistry.CreateDefault());
    private readonly HudOverlay _hud = new();
    private readonly DebugConsoleOverlay _debugConsole = new();
    private readonly EntityFactory _entityFactory;

    private World? _world;
    private PlayerEntity? _player;
    private WorldTime _worldTime = new();
    private GameContentDatabase? _content;
    private Inventory? _inventory;
    private bool _showGrid;
    private int _selectedHotbarSlot;

    public PlayingState()
    {
        _entityFactory = new EntityFactory(_collisionResolver);
    }

    public string Name => "Playing";

    public bool CapturesKeyboard => _debugConsole.IsOpen;

    public void Initialize()
    {
        _content = new GameContentLoader().LoadWithMods(ClientPaths.FindGameDataRoot(), ClientPaths.ModsRoot()).Database;
        _inventory = new Inventory(50, _content.Items);
        GiveStarterItems(_inventory);
        _worldTime = new WorldTime();
        _world = new SimpleWorldGenerator().Generate(widthTiles: 256, heightTiles: 128, seed: 1337);
        var spawn = new Vector2(
            _world.Metadata.SpawnTile.X * GameConstants.TileSize + 2,
            _world.Metadata.SpawnTile.Y * GameConstants.TileSize);
        _player = new PlayerEntity(new System.Numerics.Vector2(spawn.X, spawn.Y), _collisionResolver);
        new LightingSystem().Recalculate(_world, new[]
        {
            new LightSource(CoordinateUtils.WorldToTile(spawn.X, spawn.Y), 235, Radius: 9)
        });
        _world.ClearAllDirtyFlags();
        _camera.Position = spawn;
        _camera.Zoom = 2f;
    }

    public void LoadContent(ContentManager content)
    {
    }

    public void FixedUpdate(float fixedDeltaSeconds)
    {
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
        if (_debugConsole.Update(_input, ExecuteDebugCommand))
        {
            _player?.SetCommand(PlayerCommand.None);
            return;
        }

        _showGrid = _input.IsKeyDown(Keys.F3);
        UpdateHotbarSelection();
        _player?.SetCommand(PlayerCommandBuilder.Build(_input));
    }

    public void Draw(RenderContext context)
    {
        if (_world is null)
        {
            return;
        }

        var cameraTarget = GetCameraTarget();
        _camera.Follow(cameraTarget, context.ViewportBounds, smoothing: 0.18f);

        context.SpriteBatch.Draw(
            context.Pixel,
            context.ViewportBounds,
            new Color(91, 155, 213));

        _tilemapRenderer.ShowGrid = _showGrid;
        _tilemapRenderer.Draw(context, _world, _camera);
        DrawPlayer(context);
        DrawEntities(context);
        _lightingRenderer.Draw(context, _world, _camera);
        _hud.Draw(context, _selectedHotbarSlot, _player?.Health ?? 0, _player?.MaxHealth ?? 100);

        var worldTimeText = _worldTime.NormalizedTimeOfDay.ToString("0.000", CultureInfo.InvariantCulture);
        context.DebugText.Draw(new Vector2(12, 58), $"WORLD TIME: {worldTimeText}", Color.LightGray, 2);
        context.DebugText.Draw(new Vector2(12, 82), $"CHUNKS: {_world.Chunks.Count}", Color.LightGray, 2);
        context.DebugText.Draw(new Vector2(12, 106), $"ENTITIES: {_entities.Entities.Count}", Color.LightGray, 2);
        if (_player is not null)
        {
            var playerTile = CoordinateUtils.WorldToTile(_player.Body.Center.X, _player.Body.Center.Y);
            context.DebugText.Draw(new Vector2(12, 130), $"PLAYER TILE: {playerTile.X}:{playerTile.Y}", Color.LightGray, 2);
        }

        _debugConsole.Draw(context);
    }

    public void Dispose()
    {
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

    private void UpdateHotbarSelection()
    {
        for (var slot = 0; slot < 10; slot++)
        {
            var key = slot == 9 ? Keys.D0 : (Keys)((int)Keys.D1 + slot);
            if (_input.IsKeyPressed(key))
            {
                _selectedHotbarSlot = slot;
            }
        }

        if (_input.ScrollDelta > 0)
        {
            _selectedHotbarSlot = (_selectedHotbarSlot + 9) % 10;
        }
        else if (_input.ScrollDelta < 0)
        {
            _selectedHotbarSlot = (_selectedHotbarSlot + 1) % 10;
        }
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
            PlayerInventory = _inventory,
            WorldTime = _worldTime,
            EntityManager = _entities,
            EntityFactory = _entityFactory,
            PlayerPosition = _player?.Body.Position,
            Events = _events
        });
    }

    private static void GiveStarterItems(Inventory inventory)
    {
        inventory.AddItem(new ItemStack("copper_pickaxe", 1));
        inventory.AddItem(new ItemStack("dirt_block", 50));
        inventory.AddItem(new ItemStack("stone_block", 25));
    }
}
