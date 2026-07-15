using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Game.Client.UI;

public readonly record struct UiHitRegion(int Id, Rectangle Bounds, bool IsEnabled = true);

public sealed class UiPointerRouter
{
    private bool _wasDown;
    private int _capturedId = -1;

    public Point Position { get; private set; }

    public int HoveredId { get; private set; } = -1;

    public int PressedId { get; private set; } = -1;

    public int ClickedId { get; private set; } = -1;

    public bool IsDown { get; private set; }

    public void Update(Point position, bool isDown, IReadOnlyList<UiHitRegion> regions)
    {
        ArgumentNullException.ThrowIfNull(regions);

        Position = position;
        IsDown = isDown;
        ClickedId = -1;
        HoveredId = HitTest(position, regions);

        if (isDown && !_wasDown)
        {
            _capturedId = IsEnabled(HoveredId, regions) ? HoveredId : -1;
        }
        else if (!isDown && _wasDown)
        {
            if (_capturedId >= 0 && HoveredId == _capturedId && IsEnabled(_capturedId, regions))
            {
                ClickedId = _capturedId;
            }

            _capturedId = -1;
        }

        PressedId = isDown ? _capturedId : -1;
        _wasDown = isDown;
    }

    public bool IsPressed(int id)
    {
        return id >= 0 && PressedId == id && HoveredId == id;
    }

    public void Reset()
    {
        _wasDown = false;
        _capturedId = -1;
        HoveredId = -1;
        PressedId = -1;
        ClickedId = -1;
        IsDown = false;
    }

    private static int HitTest(Point point, IReadOnlyList<UiHitRegion> regions)
    {
        for (var index = regions.Count - 1; index >= 0; index--)
        {
            if (regions[index].Bounds.Contains(point))
            {
                return regions[index].Id;
            }
        }

        return -1;
    }

    private static bool IsEnabled(int id, IReadOnlyList<UiHitRegion> regions)
    {
        if (id < 0)
        {
            return false;
        }

        for (var index = regions.Count - 1; index >= 0; index--)
        {
            if (regions[index].Id == id)
            {
                return regions[index].IsEnabled;
            }
        }

        return false;
    }
}

public readonly record struct UiGamepadSnapshot(
    bool Up,
    bool Down,
    bool Left,
    bool Right,
    bool Confirm,
    bool Cancel,
    bool PreviousTab,
    bool NextTab)
{
    public static UiGamepadSnapshot FromState(GamePadState state)
    {
        const float stickThreshold = 0.55f;
        return new UiGamepadSnapshot(
            state.DPad.Up == ButtonState.Pressed || state.ThumbSticks.Left.Y >= stickThreshold,
            state.DPad.Down == ButtonState.Pressed || state.ThumbSticks.Left.Y <= -stickThreshold,
            state.DPad.Left == ButtonState.Pressed || state.ThumbSticks.Left.X <= -stickThreshold,
            state.DPad.Right == ButtonState.Pressed || state.ThumbSticks.Left.X >= stickThreshold,
            state.IsButtonDown(Buttons.A),
            state.IsButtonDown(Buttons.B),
            state.IsButtonDown(Buttons.LeftShoulder),
            state.IsButtonDown(Buttons.RightShoulder));
    }
}

public sealed class UiGamepadNavigator
{
    private UiGamepadSnapshot _previous;
    private DirectionRepeater _upRepeater;
    private DirectionRepeater _downRepeater;
    private DirectionRepeater _leftRepeater;
    private DirectionRepeater _rightRepeater;

    public bool UpPressed { get; private set; }

    public bool DownPressed { get; private set; }

    public bool LeftPressed { get; private set; }

    public bool RightPressed { get; private set; }

    public bool ConfirmPressed { get; private set; }

    public bool CancelPressed { get; private set; }

    public bool PreviousTabPressed { get; private set; }

    public bool NextTabPressed { get; private set; }

    public bool AnyNavigationPressed =>
        UpPressed || DownPressed || LeftPressed || RightPressed ||
        ConfirmPressed || CancelPressed || PreviousTabPressed || NextTabPressed;

    public void Update(double deltaSeconds)
    {
        Update(UiGamepadSnapshot.FromState(GamePad.GetState(PlayerIndex.One)), deltaSeconds);
    }

    public void Update(UiGamepadSnapshot current, double deltaSeconds)
    {
        var elapsed = Math.Max(0, deltaSeconds);
        UpPressed = _upRepeater.Update(current.Up, elapsed);
        DownPressed = _downRepeater.Update(current.Down, elapsed);
        LeftPressed = _leftRepeater.Update(current.Left, elapsed);
        RightPressed = _rightRepeater.Update(current.Right, elapsed);
        ConfirmPressed = current.Confirm && !_previous.Confirm;
        CancelPressed = current.Cancel && !_previous.Cancel;
        PreviousTabPressed = current.PreviousTab && !_previous.PreviousTab;
        NextTabPressed = current.NextTab && !_previous.NextTab;
        _previous = current;
    }

    public void Reset()
    {
        _previous = default;
        _upRepeater = default;
        _downRepeater = default;
        _leftRepeater = default;
        _rightRepeater = default;
        UpPressed = false;
        DownPressed = false;
        LeftPressed = false;
        RightPressed = false;
        ConfirmPressed = false;
        CancelPressed = false;
        PreviousTabPressed = false;
        NextTabPressed = false;
    }

    private struct DirectionRepeater
    {
        private const double InitialDelaySeconds = 0.38;
        private const double RepeatIntervalSeconds = 0.10;

        private bool _wasDown;
        private double _heldSeconds;
        private double _nextRepeatSeconds;

        public bool Update(bool isDown, double deltaSeconds)
        {
            if (!isDown)
            {
                _wasDown = false;
                _heldSeconds = 0;
                _nextRepeatSeconds = InitialDelaySeconds;
                return false;
            }

            if (!_wasDown)
            {
                _wasDown = true;
                _heldSeconds = 0;
                _nextRepeatSeconds = InitialDelaySeconds;
                return true;
            }

            _heldSeconds += deltaSeconds;
            if (_heldSeconds < _nextRepeatSeconds)
            {
                return false;
            }

            do
            {
                _nextRepeatSeconds += RepeatIntervalSeconds;
            }
            while (_heldSeconds >= _nextRepeatSeconds);

            return true;
        }
    }
}

public sealed class UiScrollModel
{
    public int Offset { get; private set; }

    public int ItemCount { get; private set; }

    public int ViewCapacity { get; private set; } = 1;

    public int MaximumOffset => Math.Max(0, ItemCount - ViewCapacity);

    public void Configure(int itemCount, int viewCapacity)
    {
        ItemCount = Math.Max(0, itemCount);
        ViewCapacity = Math.Max(1, viewCapacity);
        Offset = Math.Clamp(Offset, 0, MaximumOffset);
    }

    public void ScrollBy(int amount)
    {
        Offset = Math.Clamp(Offset + amount, 0, MaximumOffset);
    }

    public void EnsureVisible(int index)
    {
        if (ItemCount == 0)
        {
            Offset = 0;
            return;
        }

        var target = Math.Clamp(index, 0, ItemCount - 1);
        if (target < Offset)
        {
            Offset = target;
        }
        else if (target >= Offset + ViewCapacity)
        {
            Offset = target - ViewCapacity + 1;
        }

        Offset = Math.Clamp(Offset, 0, MaximumOffset);
    }
}
