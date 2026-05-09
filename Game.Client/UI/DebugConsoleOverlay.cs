using Game.Client.Input;
using Game.Client.Rendering;
using Game.Core.Commands;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Game.Client.UI;

public sealed class DebugConsoleOverlay
{
    private const int MaxHistoryLines = 9;
    private readonly List<string> _history = new();
    private string _input = string.Empty;

    public bool IsOpen { get; private set; }

    public bool Update(InputManager input, Func<string, CommandResult> executeCommand, string toggleBinding = "F10")
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(executeCommand);

        if (input.IsBindingPressed(toggleBinding))
        {
            IsOpen = !IsOpen;
            return true;
        }

        if (!IsOpen)
        {
            return false;
        }

        if (input.IsKeyPressed(Keys.Escape))
        {
            IsOpen = false;
            return true;
        }

        if (input.IsKeyPressed(Keys.Back) && _input.Length > 0)
        {
            _input = _input[..^1];
        }

        if (input.IsKeyPressed(Keys.Enter))
        {
            Submit(executeCommand);
        }

        return true;
    }

    public void OnTextInput(char character)
    {
        if (!IsOpen || char.IsControl(character))
        {
            return;
        }

        _input += character;
    }

    public void Draw(RenderContext context)
    {
        if (!IsOpen)
        {
            return;
        }

        var bounds = new Rectangle(18, 52, Math.Min(760, context.ViewportBounds.Width - 36), 224);
        DrawRectangle(context, bounds, new Color(14, 18, 24, 235));
        DrawBorder(context, bounds, new Color(94, 122, 148), 1);

        context.DebugText.Draw(new Vector2(bounds.X + 10, bounds.Y + 8), "DEBUG CONSOLE  F10", Color.LightSteelBlue, 2);

        var y = bounds.Y + 34;
        foreach (var line in _history.TakeLast(MaxHistoryLines))
        {
            context.DebugText.Draw(new Vector2(bounds.X + 10, y), line, Color.LightGray, 2);
            y += 18;
        }

        var inputBounds = new Rectangle(bounds.X + 8, bounds.Bottom - 30, bounds.Width - 16, 22);
        DrawRectangle(context, inputBounds, new Color(5, 8, 12, 245));
        DrawBorder(context, inputBounds, new Color(64, 82, 102), 1);
        context.DebugText.Draw(new Vector2(inputBounds.X + 6, inputBounds.Y + 4), $"> {_input}", Color.White, 2);
    }

    private void Submit(Func<string, CommandResult> executeCommand)
    {
        var commandText = _input.Trim();
        if (commandText.Length == 0)
        {
            return;
        }

        AddHistory($"> {commandText}");
        var result = executeCommand(commandText);
        AddHistory($"{(result.IsSuccess ? "OK" : "ERR")}: {result.Message}");
        _input = string.Empty;
    }

    private void AddHistory(string line)
    {
        _history.Add(line);
        if (_history.Count > 64)
        {
            _history.RemoveAt(0);
        }
    }

    private static void DrawRectangle(RenderContext context, Rectangle bounds, Color color)
    {
        context.SpriteBatch.Draw(context.Pixel, bounds, color);
    }

    private static void DrawBorder(RenderContext context, Rectangle bounds, Color color, int thickness)
    {
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(bounds.X, bounds.Y, bounds.Width, thickness), color);
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(bounds.X, bounds.Bottom - thickness, bounds.Width, thickness), color);
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(bounds.X, bounds.Y, thickness, bounds.Height), color);
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(bounds.Right - thickness, bounds.Y, thickness, bounds.Height), color);
    }
}
