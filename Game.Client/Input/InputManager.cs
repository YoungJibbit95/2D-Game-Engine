using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Game.Client.Input;

public sealed class InputManager
{
    private KeyboardState _previousKeyboard;
    private KeyboardState _currentKeyboard;
    private MouseState _previousMouse;
    private MouseState _currentMouse;

    public Point MousePosition => _currentMouse.Position;

    public int ScrollDelta => _currentMouse.ScrollWheelValue - _previousMouse.ScrollWheelValue;

    public bool IsLeftMouseDown => _currentMouse.LeftButton == ButtonState.Pressed;

    public bool IsLeftMousePressed =>
        _currentMouse.LeftButton == ButtonState.Pressed &&
        _previousMouse.LeftButton == ButtonState.Released;

    public bool IsRightMouseDown => _currentMouse.RightButton == ButtonState.Pressed;

    public bool IsRightMousePressed =>
        _currentMouse.RightButton == ButtonState.Pressed &&
        _previousMouse.RightButton == ButtonState.Released;

    public void Update()
    {
        _previousKeyboard = _currentKeyboard;
        _previousMouse = _currentMouse;
        _currentKeyboard = Keyboard.GetState();
        _currentMouse = Mouse.GetState();
    }

    public bool IsKeyDown(Keys key)
    {
        return _currentKeyboard.IsKeyDown(key);
    }

    public bool IsKeyPressed(Keys key)
    {
        return _currentKeyboard.IsKeyDown(key) && _previousKeyboard.IsKeyUp(key);
    }

    public bool IsKeyReleased(Keys key)
    {
        return _currentKeyboard.IsKeyUp(key) && _previousKeyboard.IsKeyDown(key);
    }

    public bool IsBindingDown(string binding)
    {
        return EvaluateBinding(binding, pressedOnly: false);
    }

    public bool IsBindingPressed(string binding)
    {
        return EvaluateBinding(binding, pressedOnly: true);
    }

    public string? FirstPressedKeyName()
    {
        foreach (var key in _currentKeyboard.GetPressedKeys().OrderBy(key => key.ToString(), StringComparer.OrdinalIgnoreCase))
        {
            if (_previousKeyboard.IsKeyUp(key))
            {
                return key.ToString();
            }
        }

        if (IsLeftMousePressed)
        {
            return "MouseLeft";
        }

        if (IsRightMousePressed)
        {
            return "MouseRight";
        }

        return null;
    }

    private bool EvaluateBinding(string binding, bool pressedOnly)
    {
        if (string.IsNullOrWhiteSpace(binding))
        {
            return false;
        }

        var remaining = binding.AsSpan();
        while (!remaining.IsEmpty)
        {
            var separator = remaining.IndexOf(',');
            var token = separator < 0 ? remaining : remaining[..separator];
            token = token.Trim();
            if (!token.IsEmpty && (pressedOnly ? IsTokenPressed(token) : IsTokenDown(token)))
            {
                return true;
            }

            if (separator < 0)
            {
                break;
            }

            remaining = remaining[(separator + 1)..];
        }

        return false;
    }

    private bool IsTokenDown(ReadOnlySpan<char> token)
    {
        if (IsMouseToken(token, out var down, out _))
        {
            return down;
        }

        return Enum.TryParse<Keys>(token, ignoreCase: true, out var key) && IsKeyDown(key);
    }

    private bool IsTokenPressed(ReadOnlySpan<char> token)
    {
        if (IsMouseToken(token, out _, out var pressed))
        {
            return pressed;
        }

        return Enum.TryParse<Keys>(token, ignoreCase: true, out var key) && IsKeyPressed(key);
    }

    private bool IsMouseToken(ReadOnlySpan<char> token, out bool down, out bool pressed)
    {
        if (token.Equals("MouseLeft", StringComparison.OrdinalIgnoreCase))
        {
            down = IsLeftMouseDown;
            pressed = IsLeftMousePressed;
            return true;
        }

        if (token.Equals("MouseRight", StringComparison.OrdinalIgnoreCase))
        {
            down = IsRightMouseDown;
            pressed = IsRightMousePressed;
            return true;
        }

        down = false;
        pressed = false;
        return false;
    }

}
