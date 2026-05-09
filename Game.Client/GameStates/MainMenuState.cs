using Game.Client.Input;
using Game.Client.Rendering;
using Game.Client.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Input;

namespace Game.Client.GameStates;

public sealed class MainMenuState : IGameState
{
    private readonly GameStateManager _states;
    private readonly Action _exit;
    private readonly InputManager _input = new();
    private readonly List<MenuItem> _items;
    private readonly List<Rectangle> _itemBounds = new();
    private int _selectedIndex;

    public MainMenuState(GameStateManager states, Action exit)
    {
        _states = states;
        _exit = exit;
        _items =
        [
            new MenuItem("SINGLEPLAYER", "CREATE A LOCAL WORLD", true, StartSingleplayer),
            new MenuItem("COOP SPLITSCREEN", "PLANNED FOR CONSOLE SUPPORT", false, Noop),
            new MenuItem("MULTIPLAYER", "PLANNED SERVER CLIENT SUPPORT", false, Noop),
            new MenuItem("SETTINGS", "VIDEO AUDIO INPUT DEBUG", true, OpenSettings),
            new MenuItem("EXIT", "CLOSE GAME", true, _exit)
        ];
    }

    public string Name => "MainMenu";

    public void Initialize()
    {
    }

    public void LoadContent(ContentManager content)
    {
    }

    public void FixedUpdate(float fixedDeltaSeconds)
    {
    }

    public void Update(double deltaSeconds)
    {
        _input.Update();

        if (_input.IsKeyPressed(Keys.Down) || _input.IsKeyPressed(Keys.S))
        {
            MoveSelection(1);
        }
        else if (_input.IsKeyPressed(Keys.Up) || _input.IsKeyPressed(Keys.W))
        {
            MoveSelection(-1);
        }

        UpdateMouseSelection();

        if (_input.IsKeyPressed(Keys.Enter) || _input.IsKeyPressed(Keys.Space) || _input.IsLeftMousePressed)
        {
            ActivateSelected();
        }

        if (_input.IsKeyPressed(Keys.Escape))
        {
            _exit();
        }
    }

    public void Draw(RenderContext context)
    {
        DrawBackground(context);

        var titleX = Math.Max(36, context.ViewportBounds.Width / 2 - 250);
        context.DebugText.Draw(new Vector2(titleX, 64), "TERRARIA LIKE ENGINE", new Color(245, 224, 151), 4);
        context.DebugText.Draw(new Vector2(titleX + 4, 112), "SANDBOX ACTION GAMEBUILDING CORE", new Color(190, 214, 230), 2);

        _itemBounds.Clear();
        var startY = 172;
        var width = Math.Min(520, context.ViewportBounds.Width - 72);
        var x = Math.Max(36, context.ViewportBounds.Width / 2 - width / 2);

        for (var index = 0; index < _items.Count; index++)
        {
            var bounds = new Rectangle(x, startY + index * 62, width, 48);
            _itemBounds.Add(bounds);
            DrawMenuItem(context, _items[index], bounds, index == _selectedIndex);
        }

        var footerY = context.ViewportBounds.Height - 48;
        context.DebugText.Draw(new Vector2(36, footerY), "ENTER SELECT   ESC EXIT   F10 CONSOLE IN GAME", new Color(144, 159, 174), 2);
    }

    public void Dispose()
    {
    }

    private void DrawMenuItem(RenderContext context, MenuItem item, Rectangle bounds, bool selected)
    {
        var fill = selected ? new Color(50, 62, 76, 230) : new Color(18, 24, 32, 210);
        var border = selected ? new Color(245, 214, 126) : new Color(82, 96, 112);
        var text = item.Enabled ? Color.White : new Color(116, 126, 136);
        var hint = item.Enabled ? new Color(154, 176, 190) : new Color(84, 92, 102);

        context.SpriteBatch.Draw(context.Pixel, bounds, fill);
        DrawBorder(context, bounds, border, selected ? 3 : 1);
        context.DebugText.Draw(new Vector2(bounds.X + 16, bounds.Y + 8), item.Label, text, 2);
        context.DebugText.Draw(new Vector2(bounds.X + 16, bounds.Y + 28), item.Hint, hint, 1);

        if (!item.Enabled)
        {
            context.DebugText.Draw(new Vector2(bounds.Right - 118, bounds.Y + 17), "LOCKED", new Color(130, 92, 92), 2);
        }
    }

    private void DrawBackground(RenderContext context)
    {
        context.SpriteBatch.Draw(context.Pixel, context.ViewportBounds, new Color(14, 20, 29));
        var horizon = new Rectangle(0, context.ViewportBounds.Height / 2, context.ViewportBounds.Width, context.ViewportBounds.Height / 2);
        context.SpriteBatch.Draw(context.Pixel, horizon, new Color(36, 64, 72));
        var ground = new Rectangle(0, context.ViewportBounds.Height - 86, context.ViewportBounds.Width, 86);
        context.SpriteBatch.Draw(context.Pixel, ground, new Color(58, 45, 35));
        var grass = new Rectangle(0, ground.Y, context.ViewportBounds.Width, 8);
        context.SpriteBatch.Draw(context.Pixel, grass, new Color(80, 136, 68));
    }

    private void MoveSelection(int direction)
    {
        if (_items.Count == 0)
        {
            return;
        }

        var next = _selectedIndex;
        for (var attempt = 0; attempt < _items.Count; attempt++)
        {
            next = ((next + direction) % _items.Count + _items.Count) % _items.Count;
            if (_items[next].Enabled)
            {
                _selectedIndex = next;
                return;
            }
        }
    }

    private void UpdateMouseSelection()
    {
        for (var index = 0; index < _itemBounds.Count; index++)
        {
            if (_itemBounds[index].Contains(_input.MousePosition))
            {
                _selectedIndex = index;
                return;
            }
        }
    }

    private void ActivateSelected()
    {
        if (_selectedIndex < 0 || _selectedIndex >= _items.Count || !_items[_selectedIndex].Enabled)
        {
            return;
        }

        _items[_selectedIndex].Action();
    }

    private void StartSingleplayer()
    {
        _states.ChangeState(new LoadingWorldState(_states, WorldLoadRequest.Singleplayer(seed: 1337)));
    }

    private void OpenSettings()
    {
        _states.ChangeState(new SettingsState(_states, this));
    }

    private static void Noop()
    {
    }

    private static void DrawBorder(RenderContext context, Rectangle bounds, Color color, int thickness)
    {
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(bounds.X, bounds.Y, bounds.Width, thickness), color);
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(bounds.X, bounds.Bottom - thickness, bounds.Width, thickness), color);
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(bounds.X, bounds.Y, thickness, bounds.Height), color);
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(bounds.Right - thickness, bounds.Y, thickness, bounds.Height), color);
    }

    private sealed record MenuItem(string Label, string Hint, bool Enabled, Action Action);
}
